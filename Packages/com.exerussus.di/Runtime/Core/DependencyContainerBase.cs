using System;
using System.Collections.Generic;

namespace Exerussus.DI
{
    /// <summary>
    /// Общая логика всех контейнеров: политики дублей, обход цепочки родителей,
    /// делегирование инъекции. Хранилище абстрагировано пятью примитивами,
    /// поэтому наследник может быть как Dictionary, так и ConcurrentDictionary.
    /// </summary>
    public abstract class DependencyContainerBase : IDependencyContainer
    {
        protected DependencyContainerBase(IDependencyResolver parent, IDiLogger logger, DuplicateRegistrationPolicy policy)
        {
            Parent = parent;
            Logger = logger ?? NullDiLogger.Instance;
            DefaultPolicy = policy;
        }

        public IDependencyResolver Parent { get; }
        public DuplicateRegistrationPolicy DefaultPolicy { get; }
        protected IDiLogger Logger { get; }

        public abstract IReadOnlyDictionary<Type, object> Registrations { get; }
        public abstract IDependencyContainer CreateScope();

        // ------------ Примитивы хранилища ------------

        protected abstract bool TryGetOwn(Type type, out object value);
        protected abstract bool ContainsOwn(Type type);
        protected abstract void SetOwn(Type type, object value);
        protected abstract bool RemoveOwn(Type type);
        protected abstract void ClearOwn();

        // ------------ Регистрация ------------

        public DependencyContainerBase Add<T>(T instance)
        {
            return AddInternal(typeof(T), instance, DefaultPolicy);
        }

        public DependencyContainerBase Add<T>(T instance, DuplicateRegistrationPolicy policy)
        {
            return AddInternal(typeof(T), instance, policy);
        }

        public DependencyContainerBase Add(Type type, object instance)
        {
            return AddInternal(type, instance, DefaultPolicy);
        }

        public DependencyContainerBase Add(Type type, object instance, DuplicateRegistrationPolicy policy)
        {
            return AddInternal(type, instance, policy);
        }

        public DependencyContainerBase AddByRuntimeType(object instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            return AddInternal(instance.GetType(), instance, DefaultPolicy);
        }

        public DependencyContainerBase AddByRuntimeType(object instance, DuplicateRegistrationPolicy policy)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            return AddInternal(instance.GetType(), instance, policy);
        }

        private DependencyContainerBase AddInternal(Type type, object instance, DuplicateRegistrationPolicy policy)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            // Проверка на регистрации, а не на извлечении: инвариант "значение приводимо к ключу"
            // держится всегда, поэтому Get может кастовать без сомнений.
            if (!type.IsInstanceOfType(instance))
                throw new ArgumentException(
                    $"[DI] Экземпляр {TypeNameUtility.PrettyName(instance.GetType())} нельзя зарегистрировать " +
                    $"как {TypeNameUtility.PrettyName(type)}: типы несовместимы.", nameof(instance));

            if (!ShouldWrite(type, policy)) return this;

            SetOwn(type, instance);
            return this;
        }

        private bool ShouldWrite(Type type, DuplicateRegistrationPolicy policy)
        {
            if (!ContainsOwn(type)) return true;

            switch (policy)
            {
                case DuplicateRegistrationPolicy.Ignore:
                    return false;

                case DuplicateRegistrationPolicy.Overwrite:
                    return true;

                case DuplicateRegistrationPolicy.Throw:
                    throw new InvalidOperationException(
                        $"[DI] Тип {TypeNameUtility.PrettyName(type)} уже зарегистрирован в этом контейнере.");

                default:
                    Logger.Warning(
                        $"[DI] Тип {TypeNameUtility.PrettyName(type)} уже зарегистрирован — ссылка перезаписана.");
                    return true;
            }
        }

        public DependencyContainerBase Remove<T>()
        {
            RemoveOwn(typeof(T));
            return this;
        }

        public DependencyContainerBase Remove(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            RemoveOwn(type);
            return this;
        }

        public bool TryRemove<T>()
        {
            return RemoveOwn(typeof(T));
        }

        public bool TryRemove(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return RemoveOwn(type);
        }

        public void Clear()
        {
            ClearOwn();
        }

        public void DisposeAll()
        {
            // Список собираем до очистки: Dispose может дёргать контейнер.
            List<IDisposable> disposables = null;

            foreach (var pair in Registrations)
            {
                if (!(pair.Value is IDisposable disposable)) continue;

                // Один инстанс может быть зарегистрирован под несколькими ключами — Dispose нужен один раз.
                // Регистраций десятки, поэтому линейная проверка дешевле отдельного компаратора.
                disposables ??= new List<IDisposable>();
                var duplicate = false;
                for (var i = 0; i < disposables.Count; i++)
                {
                    if (!ReferenceEquals(disposables[i], disposable)) continue;
                    duplicate = true;
                    break;
                }

                if (!duplicate) disposables.Add(disposable);
            }

            ClearOwn();

            if (disposables == null) return;

            for (var i = 0; i < disposables.Count; i++)
            {
                try
                {
                    disposables[i].Dispose();
                }
                catch (Exception exception)
                {
                    Logger.Error($"[DI] Ошибка в Dispose у {TypeNameUtility.PrettyName(disposables[i].GetType())}: {exception}");
                }
            }
        }

        public void Merge(IDependencyResolver other)
        {
            Merge(other, DefaultPolicy);
        }

        public void Merge(IDependencyResolver other, DuplicateRegistrationPolicy policy)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            if (ReferenceEquals(other, this)) return;

            foreach (var pair in other.Registrations) AddInternal(pair.Key, pair.Value, policy);
        }

        // ------------ Поиск ------------

        public T Get<T>()
        {
            if (TryResolve(typeof(T), out var value)) return (T)value;
            throw NotRegistered(typeof(T));
        }

        public object Get(Type type)
        {
            if (TryResolve(type, out var value)) return value;
            throw NotRegistered(type);
        }

        public bool TryGet<T>(out T value)
        {
            if (TryResolve(typeof(T), out var raw))
            {
                // Прямой каст, а не "is T": инвариант гарантирован регистрацией,
                // и если он всё же нарушен — лучше громкое падение, чем тихое false.
                value = (T)raw;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGet(Type type, out object value)
        {
            return TryResolve(type, out value);
        }

        public bool Has<T>()
        {
            return TryResolve(typeof(T), out _);
        }

        public bool Has(Type type)
        {
            return TryResolve(type, out _);
        }

        public bool HasOwn<T>()
        {
            return ContainsOwn(typeof(T));
        }

        public bool HasOwn(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return ContainsOwn(type);
        }

        private bool TryResolve(Type type, out object value)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (TryGetOwn(type, out value)) return true;

            // Родитель рекурсивно обходит уже свою цепочку.
            if (Parent != null && Parent.TryGet(type, out value)) return true;

            value = null;
            return false;
        }

        private static InvalidOperationException NotRegistered(Type type)
        {
            return new InvalidOperationException(
                $"[DI] Тип {TypeNameUtility.PrettyName(type)} не найден ни в контейнере, ни у родителей.");
        }

        // ------------ Inject / Provide ------------

        public void Inject(object target)
        {
            Injector.Inject(this, target);
        }

        public bool TryInject(object target, out MissingDependency missing)
        {
            return Injector.TryInject(this, target, out missing);
        }

        public void Inject(object target, Func<Type, MissingDependencyResult> onMissing)
        {
            Injector.Inject(this, target, onMissing);
        }

        public void Provide(object target)
        {
            Injector.Provide(this, target, DefaultPolicy);
        }

        public void Provide(object target, DuplicateRegistrationPolicy policy)
        {
            Injector.Provide(this, target, policy);
        }

        public void ValidateProvide(object target)
        {
            Injector.ValidateProvide(this, target);
        }

        public bool TryValidateProvide(object target, out ProvideConflict conflict)
        {
            return Injector.TryValidateProvide(this, target, out conflict);
        }

        // ------------ Явные реализации ------------
        // Публичные методы возвращают контейнер ради цепочек вызовов,
        // интерфейс объявляет void — поэтому мосты пишем явно.

        void IDependencyRegistry.Add<T>(T instance) => Add(instance);
        void IDependencyRegistry.Add(Type type, object instance) => Add(type, instance);
        void IDependencyRegistry.Add(Type type, object instance, DuplicateRegistrationPolicy policy) => Add(type, instance, policy);
        void IDependencyRegistry.AddByRuntimeType(object instance) => AddByRuntimeType(instance);
        void IDependencyRegistry.Remove<T>() => Remove<T>();
        void IDependencyRegistry.Remove(Type type) => Remove(type);
    }
}
