using System;

namespace Exerussus.DI
{
    /// <summary>
    /// Помечает поле или свойство для заполнения из контейнера.
    /// readonly-поля поддерживаются и предпочтительны: контейнер пишет их через рефлексию,
    /// а компилятор защищает от случайной перезаписи из пользовательского кода.
    /// Не поддерживаются: статические члены, свойства без сеттера, индексаторы.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class InjectAttribute : Attribute { }
}
