using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Exerussus.DI
{
    /// <summary> Чтение зависимостей с ожиданием регистрации. Снимает зависимость от порядка бутстрапа. </summary>
    public interface IAsyncDependencyResolver : IDependencyResolver
    {
        /// <summary> Возвращает зависимость сразу или ждёт её регистрации. </summary>
        UniTask<T> GetAsync<T>(CancellationToken cancellationToken = default);

        UniTask<object> GetAsync(Type type, CancellationToken cancellationToken = default);

        /// <summary> Ждёт появления типа, не возвращая значение. </summary>
        UniTask WhenRegistered<T>(CancellationToken cancellationToken = default);

        UniTask WhenRegistered(Type type, CancellationToken cancellationToken = default);
    }
}
