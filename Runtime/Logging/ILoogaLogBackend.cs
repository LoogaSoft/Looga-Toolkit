namespace LoogaSoft.Logging
{
    public interface ILoogaLogBackend
    {
        bool IsEnabled(string channel, LoogaLogLevel level);
        void Log(string channel, LoogaLogLevel level, string message, UnityEngine.Object? context);
        void LogException(string channel, System.Exception exception, UnityEngine.Object? context);
    }

    public interface ILoogaLogFormatter
    {
        string Format<T1>(string format, T1 arg1);
        string Format<T1, T2>(string format, T1 arg1, T2 arg2);
        string Format<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3);
    }
}
