namespace Exerussus.DI
{
    /// <summary> Логгер по умолчанию: не делает ничего. Без состояния, поэтому один инстанс на всех. </summary>
    public sealed class NullDiLogger : IDiLogger
    {
        public static readonly NullDiLogger Instance = new NullDiLogger();

        private NullDiLogger() { }

        public void Warning(string message) { }
        public void Error(string message) { }
    }
}
