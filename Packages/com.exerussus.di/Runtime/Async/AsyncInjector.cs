using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Exerussus.DI
{
    /// <summary> Инъекция с ожиданием недостающих зависимостей. </summary>
    public static class AsyncInjector
    {
        /// <summary>
        /// Заполняет [Inject]-члены, дожидаясь тех, которых ещё нет в контейнере.
        /// В отличие от синхронного Inject не транзакционна: при отмене часть членов уже заполнена.
        /// </summary>
        public static async UniTask InjectAsync(IAsyncDependencyResolver resolver, object target,
            CancellationToken cancellationToken = default)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (target == null) throw new ArgumentNullException(nameof(target));

            if (target is ValueType)
                throw new ArgumentException(
                    $"[DI] Инъекция в value-type {TypeNameUtility.PrettyName(target.GetType())} невозможна: " +
                    "значение боксируется и изменения теряются.", nameof(target));

            var points = InjectionCache.GetInjectPoints(target.GetType());

            for (var i = 0; i < points.Length; i++)
            {
                // Копия, а не ref readonly: ссылку на элемент массива нельзя держать через await.
                var point = points[i];

                if (resolver.TryGet(point.MemberType, out var value))
                {
                    point.SetValue(target, value);
                    continue;
                }

                value = await resolver.GetAsync(point.MemberType, cancellationToken);
                point.SetValue(target, value);
            }
        }

        /// <summary> Ждёт, пока станут разрешимы все [Inject]-зависимости типа, ничего не заполняя. </summary>
        public static async UniTask WhenInjectableAsync(IAsyncDependencyResolver resolver, Type targetType,
            CancellationToken cancellationToken = default)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));

            var points = InjectionCache.GetInjectPoints(targetType);

            for (var i = 0; i < points.Length; i++)
            {
                var point = points[i];
                if (resolver.Has(point.MemberType)) continue;
                await resolver.WhenRegistered(point.MemberType, cancellationToken);
            }
        }
    }
}
