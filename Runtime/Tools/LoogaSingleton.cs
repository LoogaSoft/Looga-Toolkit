using UnityEngine;

namespace LoogaSoft.Tools.Runtime
{
    public abstract class LoogaSingleton<T> : MonoBehaviour where T : Component
    {
        protected static T _instance;
        public static T Instance => _instance;

        protected virtual void Awake()
        {
            if (_instance == null)
                _instance = this as T;
        }
    }
}