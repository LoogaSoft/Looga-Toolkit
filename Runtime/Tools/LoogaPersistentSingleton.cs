using UnityEngine;

namespace LoogaSoft.Tools.Runtime
{
    public abstract class LoogaPersistentSingleton<T> : MonoBehaviour where T : Component
    {
        public bool dontDestroyOnLoad = true;
        private static T _instance;
        public static T Instance => _instance;
        
        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                
                if (dontDestroyOnLoad)
                    DontDestroyOnLoad(_instance);
            }
            else 
                Destroy(gameObject);
        }
    }
}
