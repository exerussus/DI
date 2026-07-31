using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Exerussus.DI
{
    /// <summary>
    /// Потокобезопасный контейнер с ожиданием регистраций. Взаимозаменяем с DependenciesContainer
    /// через IDependencyContainer. Политика дублей под параллельной записью работает как best-effort:
    /// проверка и запись не атомарны, регистрация ожидается на фазе бутстрапа.
    /// </summary>
    public sealed class ConcurrentDependenciesContainer : DependencyContainerBase, IAsyncDependencyResolver
    {
        private readonly ConcurrentDictionary<Type, object> _refs = new();
        private readonly ReadOnlyDictionary<Type, object> _view;
        private readonly DependencyPromiseRegistry _promises;
        private readonly bool _resumeOnMainThread;

        public ConcurrentDependenciesContainer(
            IDependencyResolver parent = null,
            IDiLogger logger = null,
            DuplicateRegistrationPolicy policy = DuplicateRegistrationPolicy.Warn,
            bool resumeOnMainThread = true,
            float waitWarningSeconds = 5f)
            : base(parent, logger ?? UnityDiLogger.Instance, policy)
        {
            _view = new ReadOnlyDictionary<Type, object>(_refs);
            _resumeOnMainThread = resumeOnMainThread;
            _promises = new DependencyPromiseRegistry(Logger, waitWarningSeconds);
        }

        private ConcurrentDependenciesContainer(
            IDependencyResolver parent,
            IDiLogger logger,
            DuplicateRegistrationPolicy policy,
            bool resumeOnMainThread,
            DependencyPromiseRegistry sharedPromises)
            : base(parent, logger, policy)
        {
            _view = new ReadOnlyDictionary<Type, object>(_refs);
            _resumeOnMainThread = resumeOnMainThread;
            // Скоупы делят реестр промисов: регистрация в родителе должна будить ждущих в потомке.
            _promises = sharedPromises;
        }

        public override IReadOnlyDictionary<Type, object> Registrations => _view;

        public override IDependencyContainer CreateScope()
        {
            return new ConcurrentDependenciesContainer(this, Logger, DefaultPolicy, _resumeOnMainThread, _promises);
        }

        protected override bool TryGetOwn(Type type, out object value) => _refs.TryGetValue(type, out value);
        protected override bool ContainsOwn(Type type) => _refs.ContainsKey(type);
        protected override bool RemoveOwn(Type type) => _refs.TryRemove(type, out _);
        protected override void ClearOwn() => _refs.Clear();

        protected override void SetOwn(Type type, object value)
        {
            _refs[type] = value;
            // Единственная точка записи на весь контейнер — здесь же будим ждущих.
            _promises.Complete(type, value);
        }

        /// <summary>
        /// Отменяет все незавершённые ожидания. Реестр промисов общий на дерево скоупов,
        /// поэтому вызов из потомка отменит ожидания и у родителя, и у соседей.
        /// </summary>
        public void CancelPendingWaits()
        {
            _promises.CancelAll();
        }

        // ------------ Асинхронное разрешение ------------

        public async UniTask<T> GetAsync<T>(CancellationToken cancellationToken = default)
        {
            if (TryGet<T>(out var ready)) return ready;
            var value = await AwaitRegistrationAsync(typeof(T), cancellationToken);
            return (T)value;
        }

        public async UniTask<object> GetAsync(Type type, CancellationToken cancellationToken = default)
        {
            if (TryGet(type, out var ready)) return ready;
            return await AwaitRegistrationAsync(type, cancellationToken);
        }

        public UniTask WhenRegistered<T>(CancellationToken cancellationToken = default)
        {
            return WhenRegistered(typeof(T), cancellationToken);
        }

        public async UniTask WhenRegistered(Type type, CancellationToken cancellationToken = default)
        {
            if (Has(type)) return;
            await AwaitRegistrationAsync(type, cancellationToken);
        }

        private async UniTask<object> AwaitRegistrationAsync(Type type, CancellationToken cancellationToken)
        {
            while (true)
            {
                // Подписка до проверки — иначе регистрация в этом промежутке потерялась бы.
                var promise = _promises.Subscribe(type);

                if (TryGet(type, out var early)) return await ResumeAsync(early, cancellationToken);

                await _promises.WaitAsync(promise, type, cancellationToken);

                // Промис общий на дерево скоупов: разбудить мог сосед, у которого своя цепочка.
                // Проверяем видимость через собственную цепочку и при промахе ждём дальше.
                if (TryGet(type, out var value)) return await ResumeAsync(value, cancellationToken);
            }
        }

        private async UniTask<object> ResumeAsync(object value, CancellationToken cancellationToken)
        {
            // Продолжение приходит на потоке, который вызвал Add. Для Unity-объектов это недопустимо.
            if (_resumeOnMainThread) await UniTask.SwitchToMainThread(cancellationToken);
            return value;
        }
    }
}
