using UnityEngine;
using System.Collections.Generic;

public class ObjectPooler : MonoBehaviour
{
    public static ObjectPooler Instance { get; private set; }

    [System.Serializable]
    public class Pool { public string tag; public GameObject prefab; public int size; }

    public List<Pool> pools;
    private Dictionary<string, Queue<GameObject>> _poolDict;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _poolDict = new Dictionary<string, Queue<GameObject>>();
        foreach (var pool in pools)
        {
            var queue = new Queue<GameObject>();
            for (int i = 0; i < pool.size; i++)
            {
                var obj = Instantiate(pool.prefab);
                obj.SetActive(false);
                queue.Enqueue(obj);
            }
            _poolDict[pool.tag] = queue;
        }
    }

    public GameObject Spawn(string tag, Vector3 position, Quaternion rotation)
    {
        if (!_poolDict.TryGetValue(tag, out var queue)) return null;
        var obj = queue.Dequeue();
        obj.SetActive(true);
        obj.transform.SetPositionAndRotation(position, rotation);
        queue.Enqueue(obj);
        return obj;
    }
}
