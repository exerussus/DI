namespace Exerussus.DI
{
    /// <summary> Что делать, если тип уже зарегистрирован в этом контейнере. </summary>
    public enum DuplicateRegistrationPolicy
    {
        /// <summary> Перезаписать и написать предупреждение в лог. Поведение по умолчанию. </summary>
        Warn = 0,

        /// <summary> Перезаписать молча. Для горячей перезагрузки и намеренных переопределений. </summary>
        Overwrite = 1,

        /// <summary> Бросить исключение. Строгий режим для продакшен-бутстрапа. </summary>
        Throw = 2,

        /// <summary> Оставить существующую регистрацию, новую отбросить. </summary>
        Ignore = 3
    }
}
