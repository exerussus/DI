using System;

namespace Exerussus.DI
{
    /// <summary>
    /// Ответ обработчика отсутствующей зависимости.
    /// Замена кортежу (bool isError, object instance): "подставить" и "зарегистрировать" разведены явно.
    /// </summary>
    public readonly struct MissingDependencyResult
    {
        public readonly MissingDependencyAction Action;
        public readonly object Instance;

        private MissingDependencyResult(MissingDependencyAction action, object instance)
        {
            Action = action;
            Instance = instance;
        }

        /// <summary> Ошибка: бросить исключение об отсутствующей зависимости. </summary>
        public static MissingDependencyResult Fail()
        {
            return new MissingDependencyResult(MissingDependencyAction.Fail, null);
        }

        /// <summary> Не ошибка: оставить член пустым. </summary>
        public static MissingDependencyResult Skip()
        {
            return new MissingDependencyResult(MissingDependencyAction.Skip, null);
        }

        /// <summary> Подставить инстанс только в этот объект, контейнер не трогать. </summary>
        public static MissingDependencyResult Use(object instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            return new MissingDependencyResult(MissingDependencyAction.Use, instance);
        }

        /// <summary> Подставить инстанс и закэшировать его в контейнере. </summary>
        public static MissingDependencyResult Register(object instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            return new MissingDependencyResult(MissingDependencyAction.Register, instance);
        }
    }
}
