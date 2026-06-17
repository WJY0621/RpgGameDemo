using UnityEngine;

public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;
    private static bool isApplicationQuitting;

    public static T Instance
    {
        get
        {
            if (isApplicationQuitting)
            {
                return null;
            }

            if (instance == null)
            {
                instance = Object.FindObjectOfType<T>();
                if (instance == null)
                {
                    GameObject gameObject = new GameObject(typeof(T).Name);
                    instance = gameObject.AddComponent<T>();
                }
            }

            return instance;
        }
    }

    protected virtual void Awake()
    {
        if (instance == null)
        {
            instance = this as T;
            DontDestroyOnLoad(gameObject);
            return;
        }

        if (instance != this as T)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    protected virtual void OnDestroy()
    {
        if (instance == this as T)
        {
            instance = null;
        }
    }
}
