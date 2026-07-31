using System;

namespace Exerussus.DI
{
    /// <summary>
    /// Помечает поле или свойство, значение которого публикуется в контейнер вызовом Provide.
    /// Не поддерживаются: статические члены, свойства без геттера, индексаторы.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class ProvideAttribute : Attribute { }
}
