using System;
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

    [Header("Interval Area")]
    [SerializeField] private float _spawnIntervalDecreaseValue = 0.1f;
    [SerializeField] private int _spawnMaxIncreaseValue = 1;
    [SerializeField] private float _minSpawnInterval = 0.5f;
    private float _timer;

    public float SpawnProgress => Mathf.Clamp01(_timer / _spawnInterval);
    public float RemainingTime => Mathf.Max(0f, _spawnInterval - _timer);
    public float MinSpawnInterval => _minSpawnInterval;
    public int MaxActiveCount
    {
        get => _maxActiveCount;
        set
        {
            _maxActiveCount = Mathf.Max(value, _maxActiveCount);
            OnSpawnMaxChanged?.Invoke(_maxActiveCount);
        }
    }
    public float SpawnInterval
    {
        get => _spawnInterval;
        set
        {
            _spawnInterval = Mathf.Max(value, _minSpawnInterval);
            OnSpawnIntervalChanged?.Invoke(_spawnInterval, _minSpawnInterval);
        }
    }
    public event Action<float, float> OnSpawnIntervalChanged;
    public event Action<int> OnSpawnMaxChanged;
    public event Action OnSpawned;

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
        UpgradeManager.OnUpgraded += OnUpgraded;
        UpgradeManager.OnDataInitialized += OnDataInitialized;
        SlimeManager.OnDataInitialized += OnDataInitialized;
    }

    private void OnDestroy()
    {
        UpgradeManager.OnUpgraded -= OnUpgraded;
        UpgradeManager.OnDataInitialized -= OnDataInitialized;
        SlimeManager.OnDataInitialized -= OnDataInitialized;
    }

    private void ApplySavedUpgrades()
    {
        var intervalUpgrade = UpgradeManager.Instance.Get(EUpgradeType.SpawnTimeSub, ESlimeGrade.None);
        if (intervalUpgrade != null)
        {
            SpawnInterval -= _spawnIntervalDecreaseValue * intervalUpgrade.Level;
        }

        var maxCountUpgrade = UpgradeManager.Instance.Get(EUpgradeType.MaxCountAdd, ESlimeGrade.None);
        if (maxCountUpgrade != null)
        {
            MaxActiveCount += _spawnMaxIncreaseValue * maxCountUpgrade.Level;
        }
    }


    private void InitSlimeSpawns()
    {
        SlimeStatus status = SlimeManager.Instance.Status;

        foreach (var item in status.ActiveSlimes)
        {
            int count = item.Value;

            for (int i = 0; i < count; ++i)
            {
                Spawn(item.Key);
            }
        }
    }

    private void OnUpgraded(EUpgradeType type, ESlimeGrade grade)
    {
        switch (type)
        {
            case EUpgradeType.SpawnTimeSub:
                DecreaseInterval();
                break;
            case EUpgradeType.MaxCountAdd:
                IncreaseMaxCount();
                break;
        }
    }

    private void OnDataInitialized()
    {
        ApplySavedUpgrades(); // 저장된 레벨 반영
        InitSlimeSpawns();
        Spawn(ESlimeGrade.Grade1);
    }
    private void Update()
    {
        if (SlimeSpawner.Instance != null &&
            SlimeSpawner.Instance.GetActiveCount() >= _maxActiveCount)
        {
            // 최대 수에 도달하면 타이머 멈춤
            return;
        }

        _timer += Time.deltaTime;

        if (_timer >= _spawnInterval)
        {
            _timer = 0f;
            Spawn(ESlimeGrade.Grade1);
            OnSpawned?.Invoke();
        }

        if (!Input.GetKeyDown(KeyCode.F1)) return;
        Spawn(ESlimeGrade.Grade1);
    }

    public SlimeController Spawn(ESlimeGrade grade)
    {
        if (SlimeSpawner.Instance == null) return null;

        Vector2 randomPos = new Vector2(
            UnityEngine.Random.Range(_spawnAreaMin.x, _spawnAreaMax.x),
            UnityEngine.Random.Range(_spawnAreaMin.y, _spawnAreaMax.y)
        );

        return SlimeSpawner.Instance.Spawn(grade, randomPos);
    }

    public void Despawn(SlimeController target)
    {
        if (SlimeSpawner.Instance == null) return;

        SlimeSpawner.Instance.Despawn(target);
    }

    public void DecreaseInterval()
    {
        SpawnInterval -= _spawnIntervalDecreaseValue;
    }

    public void IncreaseMaxCount()
    {
        MaxActiveCount += _spawnMaxIncreaseValue;
    }
    public int GetActiveCount() => SlimeSpawner.Instance.GetActiveCount();

    public List<SlimeController> GetActiveTargets() => SlimeSpawner.Instance.GetActiveTargets();
}
