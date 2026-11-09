using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CMGAS
{
    public class CMSingleton<T> : MonoBehaviour where T : Component
    {
        [Header("Singleton")]
        [Tooltip("If this is true, this singleton will auto detach if it finds itself parented on awake")]
        public bool autoMaticallyUnparentOnAwake = true;

        protected static T _instance;
        public static bool HasInstance => _instance != null;
        public static T TryGetInstance() => HasInstance ? _instance : null;
        public static T current => _instance;
        protected bool enable;

        public static T instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<T>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject(typeof(T).Name + "_AutoCreated");
                        _instance = obj.AddComponent<T>();
                    }
                }

                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (autoMaticallyUnparentOnAwake)
            {
                this.transform.SetParent(null);
            }

            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(transform.gameObject);
                enable = true;
            }
            else
            {
                if (this != _instance)
                {
                    Destroy(this.gameObject);
                }
            }
        }
    }
}
