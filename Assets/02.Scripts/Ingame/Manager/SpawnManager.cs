using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private float _spawnInterval = 3f;
    [SerializeField] private int _maxActiveCount = 10;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 _spawnAreaMin = new Vector2(-3f, -2f);
    [SerializeField] private Vector2 _spawnAreaMax = new Vector2(3f, 2f);

    [Header("Tutorial Slime")]
    [SerializeField] private Vector2 _tutorialSlimePosition = Vector2.zero;

    [Header("Interval Area")]
    [SerializeField] private float _spawnIntervalDecreaseValue = 0.1f;
    [SerializeField] private int _spawnMaxIncreaseValue = 1;
    [SerializeField] private float _minSpawnInterval = 0.5f;
    private float _timer;

    private bool _isInitialized;
    private bool _isSpawningPaused;

    public float SpawnProgress => Mathf.Clamp01(_timer / _spawnInterval);
    public float RemainingTime => Mathf.Max(0f, _spawnInterval - _timer);
    public float MinSpawnInterval => _minSpawnInterval;
    public SlimeController TutorialSlime { get; private set; }
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
    public event Action<SlimeController> OnTutorialSlimeReady;

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
        GameManager.OnAllDataInitialized += OnAllDataInitialized;
        UpgradeManager.OnUpgraded += OnUpgraded;

        // 이미 초기화가 완료된 경우
        if (GameManager.Instance.IsAllDataInitialized)
        {
            OnAllDataInitialized();
        }
    }

    private void OnDestroy()
    {
        GameManager.OnAllDataInitialized -= OnAllDataInitialized;
        UpgradeManager.OnUpgraded -= OnUpgraded;
    }

    private void OnAllDataInitialized()
    {
        _isInitialized = true;
        ApplySavedUpgrades();
        InitSlimeSpawns();

        // 복원할 슬라임이 없을 때 튜토리얼 여부에 맞는 최초 슬라임을 생성한다.
        if (SlimeSpawner.Instance.GetActiveCount() == 0)
        {
            if (TutorialProgress.ShouldRun)
            {
                SpawnTutorialSlime();
            }
            else
            {
                Spawn(ESlimeGrade.Grade1);
            }
        }
    }

    private void SpawnTutorialSlime()
    {
        TutorialSlime = SpawnTutorialSlimeAt(_tutorialSlimePosition);

        if (TutorialSlime == null) return;

        OnTutorialSlimeReady?.Invoke(TutorialSlime);
    }

    public SlimeController SpawnTutorialSlimeNear(
        SlimeController source,
        float horizontalDistance)
    {
        if (source == null) return null;

        Vector2 sourcePosition = source.transform.position;
        float centerX = (_spawnAreaMin.x + _spawnAreaMax.x) * 0.5f;
        float direction = sourcePosition.x <= centerX ? 1f : -1f;
        Vector2 position = sourcePosition +
                           Vector2.right * Mathf.Abs(horizontalDistance) * direction;
        return SpawnTutorialSlimeAt(position);
    }

    private SlimeController SpawnTutorialSlimeAt(Vector2 position)
    {
        if (SlimeSpawner.Instance == null) return null;

        SlimeController target = SlimeSpawner.Instance.Spawn(
            ESlimeGrade.Grade1,
            position);
        target?.SetMovementLocked(true);
        return target;
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
                Spawn(item.Key, shouldSave: false);
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

    private void Update()
    {
        if (!_isInitialized) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsGameplayActive) return;
        if (_isSpawningPaused) return;

        if (SlimeSpawner.Instance != null &&
            SlimeSpawner.Instance.GetActiveCount() >= _maxActiveCount)
        {
            return;
        }

        _timer += Time.deltaTime;

        if (_timer >= _spawnInterval)
        {
            _timer = 0f;
            Spawn(ESlimeGrade.Grade1);
            OnSpawned?.Invoke();
        }

        if (Keyboard.current?.f1Key.wasPressedThisFrame != true) return;
        Spawn(ESlimeGrade.Grade1);
    }

    public SlimeController Spawn(ESlimeGrade grade, bool shouldSave = true)
    {
        if (SlimeSpawner.Instance == null) return null;

        Vector2 randomPos = new Vector2(
            UnityEngine.Random.Range(_spawnAreaMin.x, _spawnAreaMax.x),
            UnityEngine.Random.Range(_spawnAreaMin.y, _spawnAreaMax.y)
        );

        return SlimeSpawner.Instance.Spawn(grade, randomPos, shouldSave);
    }

    public void Despawn(SlimeController target)
    {
        if (SlimeSpawner.Instance == null) return;

        if (target == TutorialSlime)
        {
            TutorialSlime = null;
        }

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

    public void SetSpawningPaused(bool isPaused)
    {
        _isSpawningPaused = isPaused;
    }

    public int GetActiveCount() => SlimeSpawner.Instance.GetActiveCount();

    public List<SlimeController> GetActiveTargets() => SlimeSpawner.Instance.GetActiveTargets();
}
