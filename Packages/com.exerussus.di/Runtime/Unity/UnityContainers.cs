namespace Exerussus.DI
{
    /// <summary> Фабрика контейнеров с подключённым логгером Unity. Без состояния. </summary>
    public static class UnityContainers
    {
        public static DependenciesContainer Create(DuplicateRegistrationPolicy policy = DuplicateRegistrationPolicy.Warn)
        {
            return new DependenciesContainer(null, UnityDiLogger.Instance, policy);
        }

        public static DependenciesContainer Create(IDependencyResolver parent, DuplicateRegistrationPolicy policy = DuplicateRegistrationPolicy.Warn)
        {
            return new DependenciesContainer(parent, UnityDiLogger.Instance, policy);
        }
    }
}
