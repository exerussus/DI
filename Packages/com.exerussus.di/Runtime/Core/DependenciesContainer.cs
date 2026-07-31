using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Exerussus.DI
{
    /// <summary>
    /// Однопоточный контейнер на Dictionary. Рассчитан на главный поток Unity.
    /// Для доступа с фоновых потоков или ожидания регистраций — ConcurrentDependenciesContainer.
    /// </summary>
    public sealed class DependenciesContainer : DependencyContainerBase
    {
        private readonly Dictionary<Type, object> _refs = new();
        private readonly ReadOnlyDictionary<Type, object> _view;

        public DependenciesContainer(
            IDependencyResolver parent = null,
            IDiLogger logger = null,
            DuplicateRegistrationPolicy policy = DuplicateRegistrationPolicy.Warn)
            : base(parent, logger, policy)
        {
            // Обёртка создаётся один раз: свойство Registrations не должно аллоцировать.
            _view = new ReadOnlyDictionary<Type, object>(_refs);
        }

        public override IReadOnlyDictionary<Type, object> Registrations => _view;

        public override IDependencyContainer CreateScope()
        {
            return new DependenciesContainer(this, Logger, DefaultPolicy);
        }

        protected override bool TryGetOwn(Type type, out object value) => _refs.TryGetValue(type, out value);
        protected override bool ContainsOwn(Type type) => _refs.ContainsKey(type);
        protected override void SetOwn(Type type, object value) => _refs[type] = value;
        protected override bool RemoveOwn(Type type) => _refs.Remove(type);
        protected override void ClearOwn() => _refs.Clear();
    }
}
