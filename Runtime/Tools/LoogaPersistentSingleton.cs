using UnityEngine;
using UnityEngine.Serialization;

namespace LoogaSoft.Tools.Runtime
{
    /// <summary>
    /// Keeps one component instance and can preserve its GameObject across scene loads.
    /// </summary>
    public abstract class LoogaPersistentSingleton<T> : MonoBehaviour where T : LoogaPersistentSingleton<T>
    {
        [FormerlySerializedAs("dontDestroyOnLoad")]
        [SerializeField]
        private bool _dontDestroyOnLoad = true;

        private static T _instance;

        /// <summary>
        /// Gets the active singleton instance, or null before initialization.
        /// </summary>
        public static T Instance => _instance;

        /// <summary>
        /// Gets or sets whether the singleton survives scene loads.
        /// </summary>
        public bool DontDestroyOnLoadEnabled
        {
            get => _dontDestroyOnLoad;
            set => _dontDestroyOnLoad = value;
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = (T)this;

                if (_dontDestroyOnLoad)
                {
                    DontDestroyOnLoad(gameObject);
                }

                return;
            }

            if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
