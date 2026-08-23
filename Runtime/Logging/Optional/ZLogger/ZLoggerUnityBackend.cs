using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ZLogger.Unity;

namespace LoogaSoft.Logging
{
    /// <summary>
    /// Optional ZLogger backend. It is kept in its own assembly so the base logger package
    /// still compiles in projects that do not install ZLogger.
    /// </summary>
    public sealed class ZLoggerUnityBackend : ILoogaLogBackend, IDisposable
    {
        private readonly Dictionary<string, ILogger> _loggers = new(StringComparer.Ordinal);
        private readonly ILoggerFactory _factory;

        public ZLoggerUnityBackend(bool prettyStacktrace)
        {
            _factory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddZLoggerUnityDebug(options =>
                {
                    options.PrettyStacktrace = prettyStacktrace;
                    options.IncludeScopes = false;
                    options.CaptureThreadInfo = false;
                });
            });
        }

        public bool IsEnabled(string channel, LoogaLogLevel level)
        {
            return GetLogger(channel).IsEnabled(ToMicrosoftLevel(level));
        }

        public void Log(string channel, LoogaLogLevel level, string message, UnityEngine.Object? context)
        {
            ILogger logger = GetLogger(channel);
            LogLevel mappedLevel = ToMicrosoftLevel(level);

            if (!logger.IsEnabled(mappedLevel))
                return;

            // ZLogger's Unity provider does not expose a stable public context overload.
            // Use UnityConsole if clickable Unity object context is more important than backend performance.
            logger.Log(mappedLevel, "{Message}", Format(channel, message));
        }

        public void LogException(string channel, Exception exception, UnityEngine.Object? context)
        {
            GetLogger(channel).LogError(exception, "{Message}", Format(channel, exception.Message));
        }

        public void Dispose()
        {
            _factory.Dispose();
            _loggers.Clear();
        }

        private ILogger GetLogger(string channel)
        {
            string category = string.IsNullOrWhiteSpace(channel) ? LoogaLogger.DefaultChannel : channel;
            if (_loggers.TryGetValue(category, out ILogger logger))
                return logger;

            logger = _factory.CreateLogger(category);
            _loggers.Add(category, logger);
            return logger;
        }

        private static LogLevel ToMicrosoftLevel(LoogaLogLevel level)
        {
            return level switch
            {
                LoogaLogLevel.Trace => LogLevel.Trace,
                LoogaLogLevel.Debug => LogLevel.Debug,
                LoogaLogLevel.Info => LogLevel.Information,
                LoogaLogLevel.Warning => LogLevel.Warning,
                LoogaLogLevel.Error => LogLevel.Error,
                LoogaLogLevel.Exception => LogLevel.Error,
                _ => LogLevel.None
            };
        }

        private static string Format(string channel, string message)
        {
            return string.IsNullOrWhiteSpace(channel)
                ? message
                : $"[{channel}] {message}";
        }
    }
}
