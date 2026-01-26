using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private ClickTarget _clickTargetPrefab;
    [SerializeField] private float _spawnInterval = 3f;
    [SerializeField] private int _maxActiveCount = 10;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 _spawnAreaMin = new Vector2(-3f, -2f);
    [SerializeField] private Vector2 _spawnAreaMax = new Vector2(3f, 2f);

    [Header("Pool Settings")]
    [SerializeField] private int _poolInitialSize = 15;

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

    private void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    private void InitializePool()
    {
        _poolParent = new GameObject("ClickTargetPool").transform;
        _poolParent.SetParent(transform);

        for (int i = 0; i < _poolInitialSize; i++)
        {
            CreatePoolObject();
        }
    }

    private ClickTarget CreatePoolObject()
    {
        ClickTarget target = Instantiate(_clickTargetPrefab, _poolParent);
        target.gameObject.SetActive(false);
        _pool.Enqueue(target);
        return target;
    }

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_spawnInterval);

            if (_activeTargets.Count < _maxActiveCount)
            {
                Spawn();
            }
        }
    }

    public ClickTarget Spawn()
    {
        ClickTarget target;

        if (_pool.Count > 0)
        {
            target = _pool.Dequeue();
        }
        else
        {
            target = Instantiate(_clickTargetPrefab, _poolParent);
        }

        Vector2 randomPos = new Vector2(
            Random.Range(_spawnAreaMin.x, _spawnAreaMax.x),
            Random.Range(_spawnAreaMin.y, _spawnAreaMax.y)
        );

        target.transform.position = randomPos;
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
}
