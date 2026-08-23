#if LOOGA_LOGGER_ZSTRING_SUPPORT
using Cysharp.Text;
using UnityEngine;

namespace LoogaSoft.Logging
{
    public sealed class ZStringLogFormatter : ILoogaLogFormatter
    {
        public string Format<T1>(string format, T1 arg1)
        {
            return ZString.Format(format, arg1);
        }

        public string Format<T1, T2>(string format, T1 arg1, T2 arg2)
        {
            return ZString.Format(format, arg1, arg2);
        }

        public string Format<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3)
        {
            return ZString.Format(format, arg1, arg2, arg3);
        }
    }

    internal static class ZStringLogFormatterBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            LoogaLogger.SetFormatter(new ZStringLogFormatter());
        }
    }
}
#endif
