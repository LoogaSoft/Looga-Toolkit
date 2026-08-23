using System;
using System.Collections.Generic;

namespace LoogaSoft.Logging
{
    public static class LoogaLogger
    {
        public const string DefaultChannel = "General";

        private static readonly UnityLogBackend UnityBackend = new();
        private static readonly DefaultLogFormatter DefaultFormatter = new();
        private static readonly Dictionary<string, LoogaLogLevel> ChannelMinimumLevels = new(StringComparer.Ordinal);

        private static ILoogaLogBackend _backend = UnityBackend;
        private static ILoogaLogFormatter _formatter = DefaultFormatter;

        public static bool Enabled { get; set; } = true;
        public static LoogaLogLevel MinimumLevel { get; set; } = LoogaLogLevel.Info;

        public static LoogaLogChannel Channel(string? name) => new(name);

        public static void SetBackend(ILoogaLogBackend? backend)
        {
            _backend = backend ?? UnityBackend;
        }

        public static void ResetBackend()
        {
            _backend = UnityBackend;
        }

        public static void SetFormatter(ILoogaLogFormatter? formatter)
        {
            _formatter = formatter ?? DefaultFormatter;
        }

        public static void ResetFormatter()
        {
            _formatter = DefaultFormatter;
        }

        public static void SetChannelMinimumLevel(string? channel, LoogaLogLevel level)
        {
            string normalized = NormalizeChannel(channel);
            if (level >= LoogaLogLevel.Off)
            {
                ChannelMinimumLevels.Remove(normalized);
                return;
            }

            ChannelMinimumLevels[normalized] = level;
        }

        public static bool TryGetChannelMinimumLevel(string? channel, out LoogaLogLevel level)
        {
            return ChannelMinimumLevels.TryGetValue(NormalizeChannel(channel), out level);
        }

        public static void ClearChannelMinimumLevel(string? channel)
        {
            ChannelMinimumLevels.Remove(NormalizeChannel(channel));
        }

        public static void ClearChannelMinimumLevels()
        {
            ChannelMinimumLevels.Clear();
        }

        public static bool IsEnabled(string? channel, LoogaLogLevel level)
        {
            string normalized = NormalizeChannel(channel);
            LoogaLogLevel minimumLevel = ChannelMinimumLevels.TryGetValue(normalized, out LoogaLogLevel channelLevel)
                ? channelLevel
                : MinimumLevel;

            return Enabled
                && level >= minimumLevel
                && level < LoogaLogLevel.Off
                && _backend != null
                && _backend.IsEnabled(normalized, level);
        }

        public static void Trace(string? channel, string message, UnityEngine.Object? context = null) => Log(channel, LoogaLogLevel.Trace, message, context);
        public static void Debug(string? channel, string message, UnityEngine.Object? context = null) => Log(channel, LoogaLogLevel.Debug, message, context);
        public static void Info(string? channel, string message, UnityEngine.Object? context = null) => Log(channel, LoogaLogLevel.Info, message, context);
        public static void Warning(string? channel, string message, UnityEngine.Object? context = null) => Log(channel, LoogaLogLevel.Warning, message, context);
        public static void Error(string? channel, string message, UnityEngine.Object? context = null) => Log(channel, LoogaLogLevel.Error, message, context);

        public static void Trace(string? channel, Func<string>? messageFactory, UnityEngine.Object? context = null) => Log(channel, LoogaLogLevel.Trace, messageFactory, context);
        public static void Debug(string? channel, Func<string>? messageFactory, UnityEngine.Object? context = null) => Log(channel, LoogaLogLevel.Debug, messageFactory, context);
        public static void Info(string? channel, Func<string>? messageFactory, UnityEngine.Object? context = null) => Log(channel, LoogaLogLevel.Info, messageFactory, context);
        public static void Warning(string? channel, Func<string>? messageFactory, UnityEngine.Object? context = null) => Log(channel, LoogaLogLevel.Warning, messageFactory, context);
        public static void Error(string? channel, Func<string>? messageFactory, UnityEngine.Object? context = null) => Log(channel, LoogaLogLevel.Error, messageFactory, context);

        public static void Log(string? channel, LoogaLogLevel level, string message, UnityEngine.Object? context = null)
        {
            if (!IsEnabled(channel, level))
                return;

            _backend.Log(NormalizeChannel(channel), level, message, context);
        }

        public static void Log(string? channel, LoogaLogLevel level, Func<string>? messageFactory, UnityEngine.Object? context = null)
        {
            if (messageFactory == null || !IsEnabled(channel, level))
                return;

            _backend.Log(NormalizeChannel(channel), level, messageFactory(), context);
        }

        public static void LogFormat<T1>(string? channel, LoogaLogLevel level, string format, T1 arg1, UnityEngine.Object? context = null)
        {
            if (!IsEnabled(channel, level))
                return;

            _backend.Log(NormalizeChannel(channel), level, _formatter.Format(format, arg1), context);
        }

        public static void LogFormat<T1, T2>(string? channel, LoogaLogLevel level, string format, T1 arg1, T2 arg2, UnityEngine.Object? context = null)
        {
            if (!IsEnabled(channel, level))
                return;

            _backend.Log(NormalizeChannel(channel), level, _formatter.Format(format, arg1, arg2), context);
        }

        public static void LogFormat<T1, T2, T3>(string? channel, LoogaLogLevel level, string format, T1 arg1, T2 arg2, T3 arg3, UnityEngine.Object? context = null)
        {
            if (!IsEnabled(channel, level))
                return;

            _backend.Log(NormalizeChannel(channel), level, _formatter.Format(format, arg1, arg2, arg3), context);
        }

        public static void Exception(string? channel, Exception? exception, UnityEngine.Object? context = null)
        {
            if (exception == null || !IsEnabled(channel, LoogaLogLevel.Exception))
                return;

            _backend.LogException(NormalizeChannel(channel), exception, context);
        }

        private static string NormalizeChannel(string? channel)
        {
            return string.IsNullOrWhiteSpace(channel) ? DefaultChannel : channel;
        }
    }
}
