using System;

namespace LoogaSoft.Logging
{
    public readonly struct LoogaLogChannel
    {
        public LoogaLogChannel(string? name)
        {
            Name = string.IsNullOrWhiteSpace(name) ? LoogaLogger.DefaultChannel : name;
        }

        public string Name { get; }

        public bool IsEnabled(LoogaLogLevel level) => LoogaLogger.IsEnabled(Name, level);

        public void Trace(string message, UnityEngine.Object? context = null) => LoogaLogger.Trace(Name, message, context);
        public void Debug(string message, UnityEngine.Object? context = null) => LoogaLogger.Debug(Name, message, context);
        public void Info(string message, UnityEngine.Object? context = null) => LoogaLogger.Info(Name, message, context);
        public void Warning(string message, UnityEngine.Object? context = null) => LoogaLogger.Warning(Name, message, context);
        public void Error(string message, UnityEngine.Object? context = null) => LoogaLogger.Error(Name, message, context);
        public void Trace(Func<string>? messageFactory, UnityEngine.Object? context = null) => LoogaLogger.Trace(Name, messageFactory, context);
        public void Debug(Func<string>? messageFactory, UnityEngine.Object? context = null) => LoogaLogger.Debug(Name, messageFactory, context);
        public void Info(Func<string>? messageFactory, UnityEngine.Object? context = null) => LoogaLogger.Info(Name, messageFactory, context);
        public void Warning(Func<string>? messageFactory, UnityEngine.Object? context = null) => LoogaLogger.Warning(Name, messageFactory, context);
        public void Error(Func<string>? messageFactory, UnityEngine.Object? context = null) => LoogaLogger.Error(Name, messageFactory, context);
        public void LogFormat<T1>(LoogaLogLevel level, string format, T1 arg1, UnityEngine.Object? context = null) => LoogaLogger.LogFormat(Name, level, format, arg1, context);
        public void LogFormat<T1, T2>(LoogaLogLevel level, string format, T1 arg1, T2 arg2, UnityEngine.Object? context = null) => LoogaLogger.LogFormat(Name, level, format, arg1, arg2, context);
        public void LogFormat<T1, T2, T3>(LoogaLogLevel level, string format, T1 arg1, T2 arg2, T3 arg3, UnityEngine.Object? context = null) => LoogaLogger.LogFormat(Name, level, format, arg1, arg2, arg3, context);
        public void Exception(Exception? exception, UnityEngine.Object? context = null) => LoogaLogger.Exception(Name, exception, context);
    }
}
