using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Exerussus.DI
{
    /// <summary>
    /// Промисы ожидания регистраций. Промис живёт до первой регистрации типа и удаляется —
    /// поэтому повторное ожидание того же типа создаёт новый промис и не залипает на старом результате.
    /// </summary>
    public sealed class DependencyPromiseRegistry
    {
        private static readonly Func<Type, UniTaskCompletionSource<object>> PromiseFactory =
            _ => new UniTaskCompletionSource<object>();

        private readonly ConcurrentDictionary<Type, UniTaskCompletionSource<object>> _promises = new();
        private readonly IDiLogger _logger;

        public DependencyPromiseRegistry(IDiLogger logger, float warningSeconds)
        {
            _logger = logger ?? NullDiLogger.Instance;
            WarningSeconds = warningSeconds;
        }

        /// <summary> Через сколько секунд ожидания писать предупреждение. 0 и меньше — сторож выключен. </summary>
        public float WarningSeconds { get; }

        /// <summary>
        /// Резервирует промис под тип. Вызывается ДО повторной проверки контейнера,
        /// иначе регистрация между проверкой и подпиской была бы потеряна.
        /// </summary>
        public UniTaskCompletionSource<object> Subscribe(Type type)
        {
            return _promises.GetOrAdd(type, PromiseFactory);
        }

        /// <summary> Будит всех, кто ждал этот тип. Вызывается из единственной точки записи контейнера. </summary>
        public void Complete(Type type, object value)
        {
            if (_promises.TryRemove(type, out var promise)) promise.TrySetResult(value);
        }

        /// <summary> Отменяет все незавершённые ожидания. Нужен при выгрузке скоупа. </summary>
        public void CancelAll()
        {
            foreach (var pair in _promises) pair.Value.TrySetCanceled();
            _promises.Clear();
        }

        public async UniTask<object> WaitAsync(UniTaskCompletionSource<object> promise, Type type, CancellationToken cancellationToken)
        {
            if (WarningSeconds <= 0f) return await promise.Task.AttachExternalCancellation(cancellationToken);

            using var watchdog = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            WatchAsync(type, watchdog.Token).Forget();

            try
            {
                return await promise.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                watchdog.Cancel();
            }
        }

        /// <summary>
        /// Сторож дедлоков: если ожидание затянулось, пишет в лог, кого ждут прямо сейчас.
        /// Иначе взаимное ожидание двух сервисов выглядит как молчаливое зависание бутстрапа.
        /// </summary>
        private async UniTaskVoid WatchAsync(Type type, CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(WarningSeconds), cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _logger.Warning(
                $"[DI] Ожидание {TypeNameUtility.PrettyName(type)} длится дольше {WarningSeconds:0.#} с. " +
                $"Сейчас ждут: {DescribePending()}");
        }

        private string DescribePending()
        {
            var builder = new StringBuilder();

            foreach (var pair in _promises)
            {
                if (builder.Length > 0) builder.Append(", ");
                builder.Append(TypeNameUtility.PrettyName(pair.Key));
            }

            return builder.Length == 0 ? "никого" : builder.ToString();
        }
    }
}
