using UnityEngine;

namespace LoogaSoft.Tools.Runtime
{
    /// <summary>
    /// Keeps one active component instance for the current scene lifetime.
    /// </summary>
    public abstract class LoogaSingleton<T> : MonoBehaviour where T : LoogaSingleton<T>
    {
        protected static T _instance;

        /// <summary>
        /// Gets the active singleton instance, or null before initialization.
        /// </summary>
        public static T Instance => _instance;

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = (T)this;
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
