namespace Exerussus.DI
{
    /// <summary> Точка выхода диагностики. Ядро не знает про UnityEngine.Debug. </summary>
    public interface IDiLogger
    {
        void Warning(string message);
        void Error(string message);
    }
}
