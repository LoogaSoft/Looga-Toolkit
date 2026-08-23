using System;
using UnityEngine;

namespace LoogaSoft.Logging
{
    [Serializable]
    public struct LoogaLogChannelLevelOverride
    {
        [SerializeField, Tooltip("Channel name to override, such as Combat, Inventory, or Weapon Security.")]
        private string _channel;

        [SerializeField, Tooltip("Minimum level required for this channel. Use Off to remove this override.")]
        private LoogaLogLevel _minimumLevel;

        public string Channel => _channel;
        public LoogaLogLevel MinimumLevel => _minimumLevel;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("LoogaSoft/Logging/Looga Logger Service")]
    public sealed class LoogaLoggerService : MonoBehaviour
    {
        [SerializeField, Tooltip("Turns all Looga Logger output on or off for this runtime session.")]
        private bool _enabled = true;

        [SerializeField, Tooltip("Logs below this level are ignored before they reach the active backend.")]
        private LoogaLogLevel _minimumLevel = LoogaLogLevel.Info;

        [SerializeField, Tooltip("Selects where Looga Logger sends its output.")]
        private LoogaLogBackendType _backend = LoogaLogBackendType.ZLoggerUnityConsole;

        [SerializeField, Tooltip("Adds cleaned stack traces to ZLogger Unity console output when Unity would normally show a stack trace.")]
        private bool _prettyStacktrace = true;

        [SerializeField, Tooltip("Optional per-channel minimum levels. These let noisy channels run at Warning while important channels stay at Info or Debug.")]
        private LoogaLogChannelLevelOverride[] _channelOverrides = Array.Empty<LoogaLogChannelLevelOverride>();

        private ILoogaLogBackend? _ownedBackend;

        private void Awake()
        {
            Apply();
        }

        private void OnDisable()
        {
            ReleaseBackend();
            LoogaLogger.ClearChannelMinimumLevels();
            LoogaLogger.ResetBackend();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                Apply();
        }

        public void Apply()
        {
            LoogaLogger.Enabled = _enabled;
            LoogaLogger.MinimumLevel = _minimumLevel;
            ApplyChannelOverrides();
            LoogaLogger.SetBackend(CreateBackend());
        }

        private void ApplyChannelOverrides()
        {
            LoogaLogger.ClearChannelMinimumLevels();
            if (_channelOverrides == null)
                return;

            for (int i = 0; i < _channelOverrides.Length; i++)
            {
                LoogaLogChannelLevelOverride channelOverride = _channelOverrides[i];
                if (!string.IsNullOrWhiteSpace(channelOverride.Channel))
                    LoogaLogger.SetChannelMinimumLevel(channelOverride.Channel, channelOverride.MinimumLevel);
            }
        }

        private ILoogaLogBackend CreateBackend()
        {
            ReleaseBackend();
            _ownedBackend = LoogaLogBackendFactory.Create(_backend, _prettyStacktrace);
            return _ownedBackend;
        }

        private void ReleaseBackend()
        {
            if (_ownedBackend is System.IDisposable disposable)
                disposable.Dispose();

            _ownedBackend = null;
        }
    }
}
