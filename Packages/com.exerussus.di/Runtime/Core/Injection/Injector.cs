using System;

namespace Exerussus.DI
{
    /// <summary>
    /// Вся логика инъекции. Stateless-статика поверх интерфейсов, поэтому
    /// синхронный и конкурентный контейнеры используют один и тот же код.
    /// </summary>
    public static class Injector
    {
        /// <summary>
        /// Заполняет [Inject]-члены. Транзакционно: сначала проверяем все точки,
        /// пишем только если разрешились все — иначе объект остался бы наполовину заполненным.
        /// </summary>
        public static void Inject(IDependencyResolver resolver, object target)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            ValidateTarget(target);

            var points = InjectionCache.GetInjectPoints(target.GetType());

            for (var i = 0; i < points.Length; i++)
            {
                ref readonly var point = ref points[i];
                if (resolver.TryGet(point.MemberType, out _)) continue;
                throw new InvalidOperationException(new MissingDependency(target.GetType(), point).ToString());
            }

            for (var i = 0; i < points.Length; i++)
            {
                ref readonly var point = ref points[i];
                resolver.TryGet(point.MemberType, out var value);
                point.SetValue(target, value);
            }
        }

        /// <summary> Как Inject, но вместо исключения возвращает первую неразрешённую зависимость. </summary>
        public static bool TryInject(IDependencyResolver resolver, object target, out MissingDependency missing)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            ValidateTarget(target);

            var points = InjectionCache.GetInjectPoints(target.GetType());

            for (var i = 0; i < points.Length; i++)
            {
                ref readonly var point = ref points[i];
                if (resolver.TryGet(point.MemberType, out _)) continue;
                missing = new MissingDependency(target.GetType(), point);
                return false;
            }

            for (var i = 0; i < points.Length; i++)
            {
                ref readonly var point = ref points[i];
                resolver.TryGet(point.MemberType, out var value);
                point.SetValue(target, value);
            }

            missing = default;
            return true;
        }

        /// <summary>
        /// Инъекция с обработчиком отсутствующих типов. Не транзакционна: обработчик может
        /// создавать инстансы, откатить такие побочные эффекты нельзя.
        /// </summary>
        public static void Inject(IDependencyContainer container, object target, Func<Type, MissingDependencyResult> onMissing)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (onMissing == null) throw new ArgumentNullException(nameof(onMissing));
            ValidateTarget(target);

            var points = InjectionCache.GetInjectPoints(target.GetType());

            for (var i = 0; i < points.Length; i++)
            {
                ref readonly var point = ref points[i];

                if (container.TryGet(point.MemberType, out var value))
                {
                    point.SetValue(target, value);
                    continue;
                }

                var result = onMissing(point.MemberType);

                switch (result.Action)
                {
                    case MissingDependencyAction.Skip:
                        continue;

                    case MissingDependencyAction.Use:
                        point.SetValue(target, result.Instance);
                        continue;

                    case MissingDependencyAction.Register:
                        container.Add(point.MemberType, result.Instance);
                        point.SetValue(target, result.Instance);
                        continue;

                    default:
                        throw new InvalidOperationException(new MissingDependency(target.GetType(), point).ToString());
                }
            }
        }

        /// <summary> Публикует значения [Provide]-членов в реестр. </summary>
        public static void Provide(IDependencyRegistry registry, object target, DuplicateRegistrationPolicy policy)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            ValidateTarget(target);

            var points = InjectionCache.GetProvidePoints(target.GetType());

            for (var i = 0; i < points.Length; i++)
            {
                ref readonly var point = ref points[i];
                var value = point.GetValue(target);

                if (value == null)
                    throw new InvalidOperationException(
                        $"[DI] {TypeNameUtility.PrettyName(target.GetType())}.{point.Name} помечено [Provide], " +
                        "но содержит null.");

                registry.Add(point.MemberType, value, policy);
            }
        }

        /// <summary>
        /// Проверяет, не конфликтуют ли [Provide]-члены с уже зарегистрированными типами.
        /// Смотрим только собственные регистрации: перекрытие родительского типа в скоупе — легальный сценарий.
        /// </summary>
        public static bool TryValidateProvide(IDependencyResolver resolver, object target, out ProvideConflict conflict)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            ValidateTarget(target);

            var points = InjectionCache.GetProvidePoints(target.GetType());

            for (var i = 0; i < points.Length; i++)
            {
                ref readonly var point = ref points[i];
                if (!resolver.HasOwn(point.MemberType)) continue;
                conflict = new ProvideConflict(target.GetType(), point);
                return false;
            }

            conflict = default;
            return true;
        }

        public static void ValidateProvide(IDependencyResolver resolver, object target)
        {
            if (TryValidateProvide(resolver, target, out var conflict)) return;
            throw new InvalidOperationException(conflict.ToString());
        }

        private static void ValidateTarget(object target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            // SetValue пишет в бокс, а вызывающий держит копию — изменения потерялись бы молча.
            if (target is ValueType)
                throw new ArgumentException(
                    $"[DI] Инъекция в value-type {TypeNameUtility.PrettyName(target.GetType())} невозможна: " +
                    "значение боксируется и изменения теряются.", nameof(target));
        }
    }
}
