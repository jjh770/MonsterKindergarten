using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

public readonly struct SpawnProbability
{
    public ESlimeGrade Grade { get; }
    public float Probability { get; }

    public SpawnProbability(ESlimeGrade grade, float probability)
    {
        Grade = grade;
        Probability = probability;
    }
}

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

    [Header("Spawn Weight")]
    [SerializeField] private SpawnWeightTable _spawnWeightTable;

    [Header("Interval Area")]
    [SerializeField] private float _minSpawnInterval = 0.5f;
    private float _baseSpawnInterval;
    private int _baseMaxActiveCount;
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
            _maxActiveCount = Mathf.Max(1, value);
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
            _baseSpawnInterval = _spawnInterval;
            _baseMaxActiveCount = _maxActiveCount;
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
            if (TutorialProgress.ShouldRun(TutorialIds.Main))
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
        ApplySpawnIntervalUpgrade();
        ApplySpawnMaxCountUpgrade();
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
                ApplySpawnIntervalUpgrade();
                break;
            case EUpgradeType.MaxCountAdd:
                ApplySpawnMaxCountUpgrade();
                break;
        }
    }

    private void Update()
    {
        if (!_isInitialized) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsGameplayActive) return;
        if (_isSpawningPaused) return;

#if UNITY_EDITOR
        HandleEditorSpawnShortcuts();
#endif

        if (SlimeSpawner.Instance != null &&
            SlimeSpawner.Instance.GetActiveCount() >= _maxActiveCount)
        {
            return;
        }

        _timer += Time.deltaTime;

        if (_timer >= _spawnInterval)
        {
            _timer = 0f;
            Spawn(PickSpawnGrade());
            OnSpawned?.Invoke();
        }
    }

#if UNITY_EDITOR
    private void HandleEditorSpawnShortcuts()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f1Key.wasPressedThisFrame)
        {
            Spawn(ESlimeGrade.Grade1);
        }
        else if (keyboard.f2Key.wasPressedThisFrame)
        {
            Spawn(ESlimeGrade.Grade2);
        }
        else if (keyboard.f3Key.wasPressedThisFrame)
        {
            Spawn(ESlimeGrade.Grade3);
        }
        else if (keyboard.f4Key.wasPressedThisFrame)
        {
            Spawn(ESlimeGrade.Grade4);
        }
        else if (keyboard.f5Key.wasPressedThisFrame)
        {
            Spawn(ESlimeGrade.Grade5);
        }
    }
#endif

    // 최고 해금 등급에 따라 자연 스폰할 등급을 고른다.
    // 테이블이 비어 있으면 기존 동작대로 Grade1만 스폰한다.
    private ESlimeGrade PickSpawnGrade()
    {
        if (_spawnWeightTable == null || SlimeManager.Instance == null)
        {
            return ESlimeGrade.Grade1;
        }

        return _spawnWeightTable.PickSpawnGrade(
            SlimeManager.Instance.HighestGrade,
            GetSpawnWeightUpgradeLevel());
    }

    public List<SpawnProbability> GetCurrentSpawnProbabilities()
    {
        var probabilities = new List<SpawnProbability>();
        if (_spawnWeightTable == null || SlimeManager.Instance == null)
        {
            probabilities.Add(new SpawnProbability(ESlimeGrade.Grade1, 1f));
            return probabilities;
        }

        int upgradeLevel = GetSpawnWeightUpgradeLevel();
        ESlimeGrade cap = _spawnWeightTable.GetSpawnCap(
            SlimeManager.Instance.HighestGrade,
            upgradeLevel);
        double totalWeight = 0;

        for (int grade = (int)ESlimeGrade.Grade1; grade <= (int)cap; grade++)
        {
            totalWeight += _spawnWeightTable.GetEffectiveWeight(
                (ESlimeGrade)grade,
                upgradeLevel);
        }

        if (totalWeight <= 0)
        {
            probabilities.Add(new SpawnProbability(ESlimeGrade.Grade1, 1f));
            return probabilities;
        }

        for (int grade = (int)ESlimeGrade.Grade1; grade <= (int)cap; grade++)
        {
            ESlimeGrade slimeGrade = (ESlimeGrade)grade;
            double weight = _spawnWeightTable.GetEffectiveWeight(
                slimeGrade,
                upgradeLevel);
            if (weight <= 0) continue;

            probabilities.Add(new SpawnProbability(
                slimeGrade,
                (float)(weight / totalWeight)));
        }

        return probabilities;
    }

    private static int GetSpawnWeightUpgradeLevel()
    {
        Upgrade upgrade = UpgradeManager.Instance?.Get(
            EUpgradeType.HigherGradeSpawnWeightAdd,
            ESlimeGrade.None);
        return upgrade?.Level ?? 0;
    }

    public SlimeController Spawn(ESlimeGrade grade, bool shouldSave = true)
    {
        if (SlimeSpawner.Instance == null) return null;

        Vector2 randomPos = GetRandomSpawnPosition();

        return SlimeSpawner.Instance.Spawn(grade, randomPos, shouldSave);
    }

    public Vector2 GetRandomSpawnPosition()
    {
        return new Vector2(
            UnityEngine.Random.Range(_spawnAreaMin.x, _spawnAreaMax.x),
            UnityEngine.Random.Range(_spawnAreaMin.y, _spawnAreaMax.y));
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

    private void ApplySpawnIntervalUpgrade()
    {
        Upgrade upgrade = UpgradeManager.Instance?.Get(
            EUpgradeType.SpawnTimeSub,
            ESlimeGrade.None);
        if (upgrade == null) return;

        SpawnInterval = _baseSpawnInterval - (float)upgrade.Point;
    }

    private void ApplySpawnMaxCountUpgrade()
    {
        Upgrade upgrade = UpgradeManager.Instance?.Get(
            EUpgradeType.MaxCountAdd,
            ESlimeGrade.None);
        if (upgrade == null) return;

        MaxActiveCount = _baseMaxActiveCount + Mathf.RoundToInt((float)upgrade.Point);
    }

    public void DecreaseInterval()
    {
        ApplySpawnIntervalUpgrade();
    }

    public void IncreaseMaxCount()
    {
        ApplySpawnMaxCountUpgrade();
    }

    public void SetSpawningPaused(bool isPaused)
    {
        _isSpawningPaused = isPaused;
    }

    public int GetActiveCount() => SlimeSpawner.Instance.GetActiveCount();

    public List<SlimeController> GetActiveTargets() => SlimeSpawner.Instance.GetActiveTargets();
}
