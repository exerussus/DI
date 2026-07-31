#if !UNITY_2020_3_OR_NEWER

// Заглушки UnityEngine для сборки пакета вне редактора.
// Нужны ровно для Runtime/Unity: ядро движок не тянет.

namespace UnityEngine
{
    public static class Debug
    {
        public static void LogWarning(object message) => System.Console.WriteLine($"[warn] {message}");
        public static void LogError(object message) => System.Console.WriteLine($"[error] {message}");
    }
}
#endif
