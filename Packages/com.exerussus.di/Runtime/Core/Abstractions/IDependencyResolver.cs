using System;
using System.Collections.Generic;

namespace Exerussus.DI
{
    /// <summary>
    /// Чтение зависимостей. Разрешение идёт вверх по цепочке родителей:
    /// сначала собственные регистрации, затем родитель и его предки.
    /// </summary>
    public interface IDependencyResolver
    {
        /// <summary> Родительский скоуп или null для корневого контейнера. </summary>
        IDependencyResolver Parent { get; }

        /// <summary> Собственные регистрации контейнера, без родителя. Только чтение. </summary>
        IReadOnlyDictionary<Type, object> Registrations { get; }

        T Get<T>();
        object Get(Type type);

        bool TryGet<T>(out T value);
        bool TryGet(Type type, out object value);

        /// <summary> Разрешим ли тип с учётом цепочки родителей. </summary>
        bool Has<T>();
        bool Has(Type type);

        /// <summary> Зарегистрирован ли тип в этом контейнере, без учёта родителей. </summary>
        bool HasOwn<T>();
        bool HasOwn(Type type);
    }
}
