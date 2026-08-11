using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Exerussus.DI
{
    /// <summary>
    /// Мемоизация результатов рефлексии по типу. Данные неизменяемы и не зависят от контейнера,
    /// поэтому кэш общий на процесс. ConcurrentDictionary — потому что кэш разделяют
    /// синхронный и асинхронный контейнеры, а второй читает его с пула потоков.
    /// </summary>
    public static class InjectionCache
    {
        private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.NonPublic
                                                 | BindingFlags.Instance | BindingFlags.Static
                                                 | BindingFlags.DeclaredOnly;

        private static readonly ConcurrentDictionary<Type, InjectionPoint[]> InjectPointsCache = new();
        private static readonly ConcurrentDictionary<Type, InjectionPoint[]> ProvidePointsCache = new();

        // Делегаты в статических полях: иначе GetOrAdd аллоцировал бы новый на каждый вызов.
        private static readonly Func<Type, InjectionPoint[]> InjectFactory = BuildInjectPoints;
        private static readonly Func<Type, InjectionPoint[]> ProvideFactory = BuildProvidePoints;

        /// <summary> Точки [Inject] для типа. Массив кэширован — не мутировать. </summary>
        public static InjectionPoint[] GetInjectPoints(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return InjectPointsCache.GetOrAdd(type, InjectFactory);
        }

        /// <summary> Точки [Provide] для типа. Массив кэширован — не мутировать. </summary>
        public static InjectionPoint[] GetProvidePoints(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return ProvidePointsCache.GetOrAdd(type, ProvideFactory);
        }

        private static InjectionPoint[] BuildInjectPoints(Type type)
        {
            return Build(type, typeof(InjectAttribute), needWrite: true, needRead: false);
        }

        private static InjectionPoint[] BuildProvidePoints(Type type)
        {
            return Build(type, typeof(ProvideAttribute), needWrite: false, needRead: true);
        }

        private static InjectionPoint[] Build(Type type, Type attributeType, bool needWrite, bool needRead)
        {
            List<InjectionPoint> points = null;
            HashSet<MethodInfo> seenAccessors = null;
            var attributeName = attributeType == typeof(InjectAttribute) ? "[Inject]" : "[Provide]";

            // Идём вверх по иерархии: с DeclaredOnly иначе теряются приватные поля базовых классов.
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (var field in current.GetFields(MemberFlags))
                {
                    // inherit: false — иерархию мы обходим сами, дублей быть не должно.
                    if (!Attribute.IsDefined(field, attributeType, false)) continue;

                    if (field.IsStatic) throw StaticMemberError(attributeName, current, field.Name);

                    // readonly на [Inject]-поле — ожидаемый и рекомендуемый стиль: контейнер пишет
                    // через рефлексию, а компилятор запрещает перезапись из пользовательского кода.
                    // Статические initonly отсекаются проверкой выше — там рефлексия действительно запрещена.

                    (points ??= new List<InjectionPoint>()).Add(new InjectionPoint(field));
                }

                foreach (var property in current.GetProperties(MemberFlags))
                {
                    if (property.GetIndexParameters().Length > 0) continue; // индексаторы пропускаем
                    if (!Attribute.IsDefined(property, attributeType, false)) continue;

                    var accessor = property.GetMethod ?? property.SetMethod;
                    if (accessor == null) continue;
                    if (accessor.IsStatic) throw StaticMemberError(attributeName, current, property.Name);

                    // Переопределённое свойство встречается и в наследнике, и в базе. Без дедупликации
                    // сеттер вызывался бы дважды — это и был баг двойной инъекции.
                    seenAccessors ??= new HashSet<MethodInfo>();
                    if (!seenAccessors.Add(accessor.GetBaseDefinition())) continue;

                    if (needWrite && !property.CanWrite)
                        throw new InvalidOperationException(
                            $"[DI] Свойство {TypeNameUtility.PrettyName(current)}.{property.Name} помечено " +
                            $"{attributeName}, но не имеет сеттера.");

                    if (needRead && !property.CanRead)
                        throw new InvalidOperationException(
                            $"[DI] Свойство {TypeNameUtility.PrettyName(current)}.{property.Name} помечено " +
                            $"{attributeName}, но не имеет геттера.");

                    (points ??= new List<InjectionPoint>()).Add(new InjectionPoint(property));
                }
            }

            return points == null ? Array.Empty<InjectionPoint>() : points.ToArray();
        }

        private static InvalidOperationException StaticMemberError(string attributeName, Type owner, string memberName)
        {
            return new InvalidOperationException(
                $"[DI] {TypeNameUtility.PrettyName(owner)}.{memberName} помечено {attributeName}, " +
                "но объявлено static. Контейнер работает только с членами экземпляра.");
        }
    }
}
