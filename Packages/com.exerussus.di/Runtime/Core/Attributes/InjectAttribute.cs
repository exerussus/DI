using System;

namespace Exerussus.DI
{
    /// <summary>
    /// Помечает поле или свойство для заполнения из контейнера.
    /// Не поддерживаются: статические члены, readonly-поля, свойства без сеттера, индексаторы.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class InjectAttribute : Attribute { }
}
