using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private float _spawnInterval = 3f;
    [SerializeField] private int _maxActiveCount = 10;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 _spawnAreaMin = new Vector2(-3f, -2f);
    [SerializeField] private Vector2 _spawnAreaMax = new Vector2(3f, 2f);

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
    }

    private void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_spawnInterval);

            if (ClickTargetPool.Instance != null &&
                ClickTargetPool.Instance.GetActiveCount() < _maxActiveCount)
            {
                Spawn();
            }
        }
    }

    public ClickTarget Spawn()
    {
        if (ClickTargetPool.Instance == null) return null;

        Vector2 randomPos = new Vector2(
            Random.Range(_spawnAreaMin.x, _spawnAreaMax.x),
            Random.Range(_spawnAreaMin.y, _spawnAreaMax.y)
        );

        return ClickTargetPool.Instance.Spawn(randomPos);
    }

    public void Despawn(ClickTarget target)
    {
        if (ClickTargetPool.Instance == null) return;

        ClickTargetPool.Instance.Despawn(target);
    }

    public int GetActiveCount() => ClickTargetPool.Instance.GetActiveCount();

    public List<ClickTarget> GetActiveTargets() => ClickTargetPool.Instance.GetActiveTargets();
}
