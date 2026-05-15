using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Exerussus.DI
{
    public class DependenciesContainer
    {
        private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.NonPublic
                                                 | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        // Кэш рефлексии. Сбрасывается на reload домена вместе со статиками.
        private static readonly Dictionary<Type, MemberInfo[]> InjectCache = new();
        private static readonly Dictionary<Type, MemberInfo[]> ProvideCache = new();

        private readonly Dictionary<Type, object> _refs = new();

        public DependenciesContainer() { }

        public DependenciesContainer(DependenciesContainer other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            foreach (var (key, value) in other._refs) _refs[key] = value;
        }

        // ------------ Регистрация ------------

        /// <summary> Регистрирует объект по его фактическому типу. </summary>
        public DependenciesContainer Add(object reference)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            RegisterInternal(reference.GetType(), reference);
            return this;
        }

        /// <summary> Регистрирует объект по явно указанному типу (например, по интерфейсу). </summary>
        public DependenciesContainer Add(Type type, object reference)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            RegisterInternal(type, reference);
            return this;
        }

        public DependenciesContainer Remove<T>()
        {
            _refs.Remove(typeof(T));
            return this;
        }

        public DependenciesContainer Remove(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            _refs.Remove(type);
            return this;
        }

        public void Clear() => _refs.Clear();

        public void Merge(DependenciesContainer other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            foreach (var (key, value) in other._refs) _refs[key] = value;
        }

        private void RegisterInternal(Type type, object value)
        {
            if (_refs.ContainsKey(type))
                Debug.LogWarning($"[DependenciesContainer] Тип {type} уже зарегистрирован, ссылка будет перезаписана.");
            _refs[type] = value;
        }

        // ------------ Поиск ------------

        public T Get<T>()
        {
            var type = typeof(T);
            if (!_refs.TryGetValue(type, out var raw))
                throw new InvalidOperationException(
                    $"[DependenciesContainer] Тип {type} не найден в контейнере. Был ли он добавлен?");

            if (raw is not T value)
                throw new InvalidCastException(
                    $"[DependenciesContainer] Объект типа {raw.GetType()} нельзя привести к {type}.");

            return value;
        }

        public bool TryGet<T>(out T value)
        {
            if (_refs.TryGetValue(typeof(T), out var obj) && obj is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        public bool Has<T>() => _refs.ContainsKey(typeof(T));
        public bool Has(Type type) => type != null && _refs.ContainsKey(type);

        public object[] GetAllRefs() => _refs.Values.ToArray();

        private bool TryGet(Type type, out object value) => _refs.TryGetValue(type, out value);

        // ------------ Inject / Provide ------------

        public void TryInjectFields(object target, DependenciesContainer fallback = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            foreach (var member in GetInjectMembers(target.GetType()))
            {
                var memberType = GetMemberType(member);

                if (TryGet(memberType, out var obj) ||
                    (fallback != null && fallback.TryGet(memberType, out obj)))
                {
                    SetMemberValue(member, target, obj);
                    continue;
                }

                ThrowInjectMissing(target, member, memberType);
            }
        }

        public void TryInjectFields(object target,
            Func<Type, (bool isError, object instance)> onMissing,
            DependenciesContainer fallback = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (onMissing == null) throw new ArgumentNullException(nameof(onMissing));

            foreach (var member in GetInjectMembers(target.GetType()))
            {
                var memberType = GetMemberType(member);

                if (TryGet(memberType, out var obj) ||
                    (fallback != null && fallback.TryGet(memberType, out obj)))
                {
                    SetMemberValue(member, target, obj);
                    continue;
                }

                var (isError, instance) = onMissing(memberType);
                if (isError) ThrowInjectMissing(target, member, memberType);

                if (instance != null)
                {
                    SetMemberValue(member, target, instance);
                    _refs[memberType] = instance;
                }
            }
        }

        public void TryProvideFields(object target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            foreach (var member in GetProvideMembers(target.GetType()))
            {
                var value = GetMemberValue(member, target);
                var memberType = GetMemberType(member);

                if (value == null)
                    throw new InvalidOperationException(
                        $"[DependenciesContainer] {CleanTypeName(target.GetType())}.\"{member.Name}\" " +
                        $"помечено [Provide], но содержит null.");

                if (_refs.ContainsKey(memberType))
                    Debug.LogWarning($"[DependenciesContainer] Тип {memberType} уже в контейнере, [Provide] перезапишет значение.");

                _refs[memberType] = value;
            }
        }

        /// <summary> Бросает исключение, если у target есть [Provide]-поле, чей тип уже зарегистрирован в контейнере. </summary>
        public void ThrowIfHasProvideFields(object target, string message)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            foreach (var member in GetProvideMembers(target.GetType()))
            {
                var memberType = GetMemberType(member);
                if (_refs.ContainsKey(memberType))
                {
                    throw new InvalidOperationException(
                        $"[DependenciesContainer] {message}\n" +
                        $"Объект: {CleanTypeName(target.GetType())}\n" +
                        $"Поле: {member.Name}\n" +
                        $"Тип: {memberType}");
                }
            }
        }

        // ------------ Кэш рефлексии ------------

        private static MemberInfo[] GetInjectMembers(Type type) =>
            GetCachedMembers(type, InjectCache, typeof(InjectAttribute));

        private static MemberInfo[] GetProvideMembers(Type type) =>
            GetCachedMembers(type, ProvideCache, typeof(ProvideAttribute));

        private static MemberInfo[] GetCachedMembers(Type type, Dictionary<Type, MemberInfo[]> cache, Type attributeType)
        {
            if (cache.TryGetValue(type, out var cached)) return cached;

            var result = new List<MemberInfo>();
            // Идём вверх по иерархии: без этого приватные поля базовых классов теряются.
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (var fi in t.GetFields(MemberFlags))
                {
                    if (Attribute.IsDefined(fi, attributeType)) result.Add(fi);
                }

                foreach (var pi in t.GetProperties(MemberFlags))
                {
                    if (pi.GetIndexParameters().Length > 0) continue; // индексеры пропускаем
                    if (Attribute.IsDefined(pi, attributeType)) result.Add(pi);
                }
            }

            var array = result.ToArray();
            cache[type] = array;
            return array;
        }

        // ------------ Хелперы по членам ------------

        private static Type GetMemberType(MemberInfo member) => member switch
        {
            FieldInfo fi => fi.FieldType,
            PropertyInfo pi => pi.PropertyType,
            _ => throw new InvalidOperationException($"Unsupported member kind: {member.MemberType}")
        };

        private static void SetMemberValue(MemberInfo member, object target, object value)
        {
            switch (member)
            {
                case FieldInfo fi:
                    fi.SetValue(target, value);
                    break;
                case PropertyInfo pi:
                    if (!pi.CanWrite)
                        throw new InvalidOperationException(
                            $"[DependenciesContainer] Свойство {pi.DeclaringType?.Name}.{pi.Name} " +
                            $"помечено [Inject], но не имеет сеттера.");
                    pi.SetValue(target, value);
                    break;
            }
        }

        private static object GetMemberValue(MemberInfo member, object target) => member switch
        {
            FieldInfo fi => fi.GetValue(target),
            PropertyInfo pi => pi.GetValue(target),
            _ => null
        };

        private static void ThrowInjectMissing(object target, MemberInfo member, Type missingType)
        {
            throw new InvalidOperationException(
                $"[DependenciesContainer] Ошибка инъекции в {CleanTypeName(target.GetType())}: " +
                $"тип {missingType.Name} (\"{member.Name}\") отсутствует в контейнере зависимостей.");
        }

        private static string CleanTypeName(Type type)
        {
            if (!type.IsGenericType) return type.Name;

            var sb = new StringBuilder();
            foreach (var arg in type.GetGenericArguments())
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(CleanTypeName(arg));
            }

            var genericIndex = type.Name.LastIndexOf('`');
            var typeName = genericIndex == -1 ? type.Name : type.Name.Substring(0, genericIndex);
            return $"{typeName}<{sb}>";
        }
    }
}