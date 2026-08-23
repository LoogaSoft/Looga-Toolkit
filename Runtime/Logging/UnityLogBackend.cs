using System;

namespace LoogaSoft.Logging
{
    public enum LoogaLogBackendType
    {
        UnityConsole = 0,
        ZLoggerUnityConsole = 1
    }

    public sealed class UnityLogBackend : ILoogaLogBackend
    {
        public bool IsEnabled(string channel, LoogaLogLevel level) => true;

        public void Log(string channel, LoogaLogLevel level, string message, UnityEngine.Object? context)
        {
            string formatted = Format(channel, message);
            switch (level)
            {
                case LoogaLogLevel.Warning:
                    UnityEngine.Debug.LogWarning(formatted, context);
                    break;
                case LoogaLogLevel.Error:
                case LoogaLogLevel.Exception:
                    UnityEngine.Debug.LogError(formatted, context);
                    break;
                default:
                    UnityEngine.Debug.Log(formatted, context);
                    break;
            }
        }

        public void LogException(string channel, Exception exception, UnityEngine.Object? context)
        {
            UnityEngine.Debug.LogError(Format(channel, exception.Message), context);
            UnityEngine.Debug.LogException(exception, context);
        }

        private static string Format(string channel, string message)
        {
            return string.IsNullOrWhiteSpace(channel)
                ? message
                : $"[{channel}] {message}";
        }
    }

    internal static class LoogaLogBackendFactory
    {
        private const string ZLoggerBackendTypeName = "LoogaSoft.Logging.ZLoggerUnityBackend, LoogaSoft.Logger.ZLogger";

        public static ILoogaLogBackend Create(LoogaLogBackendType backendType, bool prettyStacktrace)
        {
            if (backendType == LoogaLogBackendType.ZLoggerUnityConsole)
            {
                ILoogaLogBackend? zLoggerBackend = CreateZLoggerBackend(prettyStacktrace);
                if (zLoggerBackend != null)
                    return zLoggerBackend;
            }

            return new UnityLogBackend();
        }

        private static ILoogaLogBackend? CreateZLoggerBackend(bool prettyStacktrace)
        {
            Type backendType = Type.GetType(ZLoggerBackendTypeName);
            if (backendType == null)
                return null;

            return Activator.CreateInstance(backendType, new object[] { prettyStacktrace }) as ILoogaLogBackend;
        }
    }

    internal sealed class DefaultLogFormatter : ILoogaLogFormatter
    {
        public string Format<T1>(string format, T1 arg1)
        {
            return string.Format(format, arg1);
        }

        public string Format<T1, T2>(string format, T1 arg1, T2 arg2)
        {
            return string.Format(format, arg1, arg2);
        }

        public string Format<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3)
        {
            return string.Format(format, arg1, arg2, arg3);
        }
    }
}
