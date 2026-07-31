using System;

namespace Exerussus.DI
{
    /// <summary> Запись зависимостей. Регистрация всегда локальная, в родителя не поднимается. </summary>
    public interface IDependencyRegistry
    {
        /// <summary> Политика по умолчанию при повторной регистрации типа. </summary>
        DuplicateRegistrationPolicy DefaultPolicy { get; }

        /// <summary> Регистрирует по typeof(T). Для интерфейса указывайте T явно: Add&lt;IFoo&gt;(foo). </summary>
        void Add<T>(T instance);

        void Add(Type type, object instance);
        void Add(Type type, object instance, DuplicateRegistrationPolicy policy);

        /// <summary> Регистрирует по фактическому типу инстанса. Явное имя вместо перегрузки Add(object). </summary>
        void AddByRuntimeType(object instance);

        void Remove<T>();
        void Remove(Type type);

        /// <summary> true, если запись существовала и была удалена. </summary>
        bool TryRemove<T>();
        bool TryRemove(Type type);

        /// <summary> Очищает регистрации, не вызывая Dispose. </summary>
        void Clear();

        /// <summary> Очищает регистрации и вызывает Dispose у всех IDisposable ровно один раз. </summary>
        void DisposeAll();
    }
}
