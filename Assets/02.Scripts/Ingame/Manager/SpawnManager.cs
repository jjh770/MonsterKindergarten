using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

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
    public bool IsInitialized => _isInitialized;
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
    public event Action Initialized;
    public event Action<SlimeController> OnTutorialSlimeReady;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _baseSpawnInterval = _spawnInterval;
        _baseMaxActiveCount = _maxActiveCount;
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
        ApplySavedUpgrades();
        InitSlimeSpawns();

        // 복원할 슬라임이 없을 때 튜토리얼 여부에 맞는 최초 슬라임을 생성한다.
        if (SlimeSpawner.Instance.GetActiveCount(ESlimeLocation.MainStage) == 0)
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

        // 저장된 개체 복원과 최초 생성까지 끝난 뒤 튜토리얼에 알린다.
        _isInitialized = true;
        Initialized?.Invoke();
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

        foreach (SlimeInstance instance in status.ActiveSlimes)
        {
            SlimeController target = SlimeSpawner.Instance.Restore(
                instance,
                GetRandomSpawnPosition());
            if (target != null && instance.Location == ESlimeLocation.DisplayRoom)
            {
                target.SetStagePresentationActive(false);
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

        if (!HasMainStageRoom()) return;

        _timer += Time.deltaTime;

        if (_timer >= _spawnInterval)
        {
            _timer = 0f;
            ESlimeGrade grade = PickSpawnGrade();
            SlimeController spawned = Spawn(grade);
            if (spawned != null)
            {
                SlimeManager.Instance.RecordNaturalSpawn(grade);
            }

            OnSpawned?.Invoke();
        }
    }

#if UNITY_EDITOR
    // F1~F10을 Grade1~Grade10에 대응시킨다.
    // Key 열거형은 F1부터 F12까지만 연속이므로 12를 넘겨서는 안 된다.
    private const int EditorSpawnShortcutCount = 10;

    private void HandleEditorSpawnShortcuts()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        for (int offset = 0; offset < EditorSpawnShortcutCount; ++offset)
        {
            ESlimeGrade grade = ESlimeGrade.Grade1 + offset;
            if (grade >= ESlimeGrade.Count) return;

            if (!keyboard[Key.F1 + offset].wasPressedThisFrame) continue;

            Spawn(grade);
            return;
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
        if (_spawnWeightTable == null || SlimeManager.Instance == null)
        {
            var probabilities = new List<SpawnProbability>();
            probabilities.Add(new SpawnProbability(ESlimeGrade.Grade1, 1));
            return probabilities;
        }

        return _spawnWeightTable.GetSpawnProbabilities(
            SlimeManager.Instance.HighestGrade,
            GetSpawnWeightUpgradeLevel());
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

    public void SetSpawningPaused(bool isPaused)
    {
        _isSpawningPaused = isPaused;
    }

    // 장식장 슬라임은 제외한 메인 필드 개체 수. 최대 개체 수 판정과 짝을 이룬다.
    public int GetMainStageSlimeCount() =>
        SlimeSpawner.Instance.GetActiveCount(ESlimeLocation.MainStage);

    // 메인 필드에 개체를 더 놓을 자리가 있는지 판정한다.
    // 자연 스폰과 장식장 꺼내기(기획서 §7.5)가 같은 기준을 쓰도록 한곳에 둔다.
    public bool HasMainStageRoom()
    {
        return SlimeSpawner.Instance != null &&
               SlimeSpawner.Instance.GetActiveCount(ESlimeLocation.MainStage) <
               _maxActiveCount;
    }

    public List<SlimeController> GetActiveTargets() => SlimeSpawner.Instance.GetActiveTargets();
}
