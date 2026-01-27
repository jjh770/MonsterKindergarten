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

            if (SlimeSpawner.Instance != null &&
                SlimeSpawner.Instance.GetActiveCount() < _maxActiveCount)
            {
                Spawn();
            }
        }
    }

    public Slime Spawn()
    {
        if (SlimeSpawner.Instance == null) return null;

        Vector2 randomPos = new Vector2(
            Random.Range(_spawnAreaMin.x, _spawnAreaMax.x),
            Random.Range(_spawnAreaMin.y, _spawnAreaMax.y)
        );

        return SlimeSpawner.Instance.Spawn(randomPos);
    }

    public void Despawn(Slime target)
    {
        if (SlimeSpawner.Instance == null) return;

        SlimeSpawner.Instance.Despawn(target);
    }

    public int GetActiveCount() => SlimeSpawner.Instance.GetActiveCount();

    public List<Slime> GetActiveTargets() => SlimeSpawner.Instance.GetActiveTargets();
}
