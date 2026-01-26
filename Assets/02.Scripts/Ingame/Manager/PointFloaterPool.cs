using System.Collections.Generic;
using UnityEngine;

public class PointFloaterPool : MonoBehaviour
{
    public static PointFloaterPool Instance { get; private set; }

    [SerializeField] private PointFloater _prefab;
    [SerializeField] private int _initialSize = 20;

    private Queue<PointFloater> _pool = new Queue<PointFloater>();
    private Transform _poolParent;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializePool();
    }

    private void InitializePool()
    {
        _poolParent = new GameObject("PointFloaterPool").transform;
        _poolParent.SetParent(transform);

        for (int i = 0; i < _initialSize; i++)
        {
            CreatePoolObject();
        }
    }

    private PointFloater CreatePoolObject()
    {
        PointFloater floater = Instantiate(_prefab, _poolParent);
        floater.gameObject.SetActive(false);
        floater.SetPool(this);
        _pool.Enqueue(floater);
        return floater;
    }

    public PointFloater Spawn(Vector3 position)
    {
        PointFloater floater;

        if (_pool.Count > 0)
        {
            floater = _pool.Dequeue();
        }
        else
        {
            floater = Instantiate(_prefab, _poolParent);
            floater.SetPool(this);
        }

        floater.transform.position = position;
        floater.gameObject.SetActive(true);
        return floater;
    }

    public void Despawn(PointFloater floater)
    {
        if (floater == null) return;

        floater.gameObject.SetActive(false);
        _pool.Enqueue(floater);
    }
}
