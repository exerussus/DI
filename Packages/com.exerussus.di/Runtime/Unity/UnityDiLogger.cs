using UnityEngine;

namespace Exerussus.DI
{
    /// <summary> Мост диагностики ядра в консоль Unity. Единственное место, где живёт Debug. </summary>
    public sealed class UnityDiLogger : IDiLogger
    {
        public static readonly UnityDiLogger Instance = new UnityDiLogger();

        private UnityDiLogger() { }

        public void Warning(string message) => Debug.LogWarning(message);
        public void Error(string message) => Debug.LogError(message);
    }
}
