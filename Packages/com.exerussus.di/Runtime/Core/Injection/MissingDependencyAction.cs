namespace Exerussus.DI
{
    /// <summary> Что сделать с точкой инъекции, для которой не нашлось зависимости. </summary>
    public enum MissingDependencyAction
    {
        /// <summary> Считать ошибкой и бросить исключение. </summary>
        Fail = 0,

        /// <summary> Пропустить: член останется незаполненным, это не ошибка. </summary>
        Skip = 1,

        /// <summary> Подставить инстанс, но не регистрировать его в контейнере. </summary>
        Use = 2,

        /// <summary> Подставить инстанс и зарегистрировать его в контейнере. </summary>
        Register = 3
    }
}
