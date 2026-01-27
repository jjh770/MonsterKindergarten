using System.Collections.Generic;
using UnityEngine;

public class ClickTargetPool : MonoBehaviour
{
    public static ClickTargetPool Instance { get; private set; }

    [SerializeField] private ClickTarget _prefab;
    [SerializeField] private int _initialSize = 15;

    private Queue<ClickTarget> _pool = new Queue<ClickTarget>();
    private List<ClickTarget> _activeTargets = new List<ClickTarget>();
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
        _poolParent = new GameObject("ClickTargetPool").transform;
        _poolParent.SetParent(transform);

        for (int i = 0; i < _initialSize; i++)
        {
            CreatePoolObject();
        }
    }

    private ClickTarget CreatePoolObject()
    {
        ClickTarget target = Instantiate(_prefab, _poolParent);
        target.gameObject.SetActive(false);
        _pool.Enqueue(target);
        return target;
    }

    public ClickTarget Spawn(Vector2 position)
    {
        ClickTarget target;

        if (_pool.Count > 0)
        {
            target = _pool.Dequeue();
        }
        else
        {
            target = Instantiate(_prefab, _poolParent);
        }

        target.transform.position = position;
        target.gameObject.SetActive(true);
        target.OnSpawn();

        _activeTargets.Add(target);
        return target;
    }

    public void Despawn(ClickTarget target)
    {
        if (target == null) return;

        target.OnDespawn();
        target.gameObject.SetActive(false);

        _activeTargets.Remove(target);
        _pool.Enqueue(target);
    }

    public int GetActiveCount() => _activeTargets.Count;

    public List<ClickTarget> GetActiveTargets() => _activeTargets;
}
