using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class AutoMergeManager : MonoBehaviour
{
    public const float BaseInterval = 20f;
    public const float MinimumInterval = 10f;

    public static AutoMergeManager Instance { get; private set; }

    [SerializeField] private DisplayRoomUI _displayRoomUI;
    [SerializeField] private GachaResultDirector _gachaResultDirector;

    [Tooltip("합성할 쌍이 없어 대기 중일 때 다시 살펴보는 간격입니다.")]
    [SerializeField, Min(0.05f)] private float _pendingRetryInterval = 0.25f;

    [Header("Presentation")]
    [Tooltip("두 슬라임이 서로에게 모이는 시간입니다.")]
    [SerializeField, Min(0.05f)] private float _gatherDuration = 1f;

    [Tooltip("합쳐지는 순간 터지는 이펙트입니다. 비워 두면 움직임만 나옵니다.")]
    [SerializeField] private GameObject _mergeEffectPrefab;
    [SerializeField, Min(0f)] private float _mergeEffectScale = 0.3f;
    [SerializeField, Min(0f)] private float _mergeEffectLifetime = 1.5f;

    [Tooltip("이펙트를 화면 쪽으로 눕히는 각도입니다. 합성 후보 연출과 같은 값을 씁니다.")]
    [SerializeField] private Vector3 _mergeEffectEulerAngles = new Vector3(90f, 0f, 0f);

    [Tooltip("슬라임보다 앞에 그리기 위해 더하는 정렬 순서입니다.")]
    [SerializeField] private int _mergeEffectSortingOrderOffset = 5;

    private float _elapsed;
    private float _interval = BaseInterval;
    // 주기를 채웠는데 합성할 쌍이 없어 발동을 미룬 상태.
    private bool _isPending;
    private float _pendingRetryTimer;
    // 합성이 거절된 개체. 대기 중에는 짧은 간격으로 다시 살펴보므로, 기억해 두지 않으면
    // 같은 쌍을 계속 집어 경고만 쌓는다.
    private readonly HashSet<string> _rejectedIds = new();
    // 두 슬라임이 모이는 연출 중. 다음 주기는 연출이 끝나고 다시 시작한다.
    private bool _isPresenting;
    private Sequence _presentation;
    private readonly List<PresentationPair> _presentationPairs = new();

    private sealed class PresentationPair
    {
        public SlimeController Keeper { get; }
        public SlimeController Removed { get; }
        public ESlimeGrade FromGrade { get; }
        public Vector3 Center { get; }

        public PresentationPair(SlimeController keeper, SlimeController removed)
        {
            Keeper = keeper;
            Removed = removed;
            FromGrade = keeper.Grade;
            Center = (keeper.transform.position + removed.transform.position) * 0.5f;
        }
    }

    public float Interval => _interval;
    public float Progress01 => _interval > 0f
        ? Mathf.Clamp01(_elapsed / _interval)
        : 0f;

    public event Action<float> IntervalChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        GameManager.OnAllDataInitialized += RefreshInterval;
        UpgradeManager.OnUpgraded += OnUpgraded;

        if (GameManager.Instance != null && GameManager.Instance.IsAllDataInitialized)
        {
            RefreshInterval();
        }
    }

    private void OnDestroy()
    {
        GameManager.OnAllDataInitialized -= RefreshInterval;
        UpgradeManager.OnUpgraded -= OnUpgraded;
        _presentation?.Kill();
        _presentation = null;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!CanAdvance()) return;

        // 합성할 쌍이 없으면 주기를 채운 채로 기다린다. 스폰이 끝나 슬라임이 내려앉거나
        // 가챠 결과가 필드에 놓이면 그때 바로 합성한다. 기다리는 동안 매 프레임 필드를
        // 훑을 이유는 없으므로 짧은 간격으로만 다시 살펴본다.
        if (_isPending)
        {
            _pendingRetryTimer += Time.deltaTime;
            if (_pendingRetryTimer < _pendingRetryInterval) return;

            _pendingRetryTimer = 0f;
            _isPending = !ExecuteTick();
            return;
        }

        _elapsed += Time.deltaTime;
        if (_elapsed < _interval) return;

        if (ExecuteTick()) return;

        _elapsed = _interval;
        _isPending = true;
        _pendingRetryTimer = 0f;
    }

    private bool CanAdvance()
    {
        SlimeManager slimeManager = SlimeManager.Instance;
        return !_isPresenting &&
               GameplayGate.IsMainStageReady &&
               !TutorialManager.IsRunning &&
               slimeManager != null &&
               slimeManager.IsAutoMergeUnlocked &&
               slimeManager.IsAutoMergeEnabled &&
               (_displayRoomUI == null || !_displayRoomUI.IsSendMode) &&
               (_gachaResultDirector == null || !_gachaResultDirector.IsPlaying);
    }

    // 낮은 등급부터 훑어 현재 업그레이드 레벨이 허용하는 수만큼 쌍을 고른다.
    // 한 슬라임은 한 번만 고르므로 이번 결과가 같은 Tick에서 다시 합성되지 않는다.
    private bool ExecuteTick()
    {
        if (SpawnManager.Instance == null || MergeManager.Instance == null) return false;

        var byGrade = new SortedDictionary<ESlimeGrade, List<SlimeController>>();
        foreach (SlimeController target in SpawnManager.Instance.GetActiveTargets())
        {
            if (target == null || target.IsDragging || target.IsSpecial ||
                !target.HasLanded ||
                target.Location != ESlimeLocation.MainStage ||
                target.Grade >= ESlimeGrade.Count - 1 ||
                string.IsNullOrEmpty(target.InstanceId) ||
                _rejectedIds.Contains(target.InstanceId))
            {
                continue;
            }

            if (!byGrade.TryGetValue(target.Grade, out List<SlimeController> targets))
            {
                targets = new List<SlimeController>();
                byGrade.Add(target.Grade, targets);
            }

            targets.Add(target);
        }

        int pairLimit = GetPairCountForLevel(GetUpgradeLevel());
        var pairs = new List<MergeManager.MergeTargetPair>(pairLimit);
        foreach (List<SlimeController> targets in byGrade.Values)
        {
            if (targets.Count < 2) continue;

            targets.Sort((left, right) => string.CompareOrdinal(
                left.InstanceId,
                right.InstanceId));
            for (int i = 0; i + 1 < targets.Count && pairs.Count < pairLimit; i += 2)
            {
                pairs.Add(new MergeManager.MergeTargetPair(targets[i], targets[i + 1]));
            }

            if (pairs.Count >= pairLimit) break;
        }

        if (pairs.Count == 0) return false;

        BeginPresentation(pairs);
        return true;
    }

    // 각 쌍이 서로 모여 합쳐지는 것처럼 동시에 보여준다. 모으는 동안에는 선택된
    // 슬라임만 잠가 터치와 드래그가 닿지 않게 하고, 모두 모인 순간 한 번에 저장한다.
    private void BeginPresentation(IReadOnlyList<MergeManager.MergeTargetPair> pairs)
    {
        _isPresenting = true;
        _presentationPairs.Clear();

        _presentation?.Kill();
        _presentation = DOTween.Sequence();
        foreach (MergeManager.MergeTargetPair pair in pairs)
        {
            var presentationPair = new PresentationPair(pair.Keeper, pair.Removed);
            _presentationPairs.Add(presentationPair);
            pair.Keeper.SetPresentationLocked(true);
            pair.Removed.SetPresentationLocked(true);
            _presentation
                .Join(pair.Keeper.transform
                    .DOMove(presentationPair.Center, _gatherDuration)
                    .SetEase(Ease.InQuad))
                .Join(pair.Removed.transform
                    .DOMove(presentationPair.Center, _gatherDuration)
                    .SetEase(Ease.InQuad));
        }

        _presentation.OnComplete(CompletePresentation);
    }

    private void CompletePresentation()
    {
        _presentation = null;

        var activePairs = new List<MergeManager.MergeTargetPair>(_presentationPairs.Count);
        foreach (PresentationPair pair in _presentationPairs)
        {
            if (pair.Keeper != null && pair.Removed != null &&
                pair.Keeper.gameObject.activeInHierarchy &&
                pair.Removed.gameObject.activeInHierarchy)
            {
                activePairs.Add(new MergeManager.MergeTargetPair(pair.Keeper, pair.Removed));
            }
        }

        bool merged = MergeManager.Instance != null &&
                      MergeManager.Instance.MergeBatch(activePairs);

        foreach (PresentationPair pair in _presentationPairs)
        {
            bool pairMerged = pair.Keeper != null &&
                              pair.Keeper.gameObject.activeInHierarchy &&
                              pair.Keeper.Grade == pair.FromGrade + 1 &&
                              pair.Removed != null &&
                              !pair.Removed.gameObject.activeInHierarchy;
            if (pairMerged)
            {
                PlayMergeEffect(
                    pair.Center,
                    pair.Keeper.GetComponent<SpriteRenderer>());
            }
            else if (pair.Keeper != null && pair.Removed != null)
            {
                _rejectedIds.Add(pair.Keeper.InstanceId);
                _rejectedIds.Add(pair.Removed.InstanceId);
            }

            // 사라진 쪽도 풀로 돌아가 다시 쓰이므로 잠금을 되돌린다.
            pair.Keeper?.SetPresentationLocked(false);
            pair.Removed?.SetPresentationLocked(false);
        }

        _presentationPairs.Clear();
        _isPresenting = false;

        // 다음 주기는 합쳐진 슬라임이 나온 뒤부터 센다.
        _elapsed = merged ? 0f : _interval;
        _isPending = !merged;
        _pendingRetryTimer = 0f;
    }

    public static int GetPairCountForLevel(int level)
    {
        int pairCount = 1;
        if (level >= 20) pairCount++;
        if (level >= 40) pairCount++;
        if (level >= 50) pairCount++;
        return pairCount;
    }

    private static int GetUpgradeLevel()
    {
        Upgrade upgrade = UpgradeManager.Instance != null
            ? UpgradeManager.Instance.Get(EUpgradeType.AutoMergeTimeSub, ESlimeGrade.None)
            : null;
        return upgrade?.Level ?? 0;
    }

    // 이펙트 프리팹은 평면이라 눕히지 않으면 옆에서 보여 화면에 아무것도 나오지 않는다.
    // 정렬도 슬라임 기준으로 맞춰야 필드 뒤로 숨지 않는다.
    private void PlayMergeEffect(Vector3 center, SpriteRenderer reference)
    {
        if (_mergeEffectPrefab == null) return;

        GameObject effect = Instantiate(
            _mergeEffectPrefab,
            center,
            Quaternion.Euler(_mergeEffectEulerAngles));
        effect.transform.localScale = Vector3.one * _mergeEffectScale;

        if (reference != null)
        {
            foreach (Renderer effectRenderer in
                     effect.GetComponentsInChildren<Renderer>(true))
            {
                effectRenderer.sortingLayerID = reference.sortingLayerID;
                effectRenderer.sortingOrder =
                    reference.sortingOrder + _mergeEffectSortingOrderOffset;
            }
        }

        Destroy(effect, _mergeEffectLifetime);
    }

    private void OnUpgraded(EUpgradeType type, ESlimeGrade grade)
    {
        if (type == EUpgradeType.AutoMergeTimeSub)
        {
            RefreshInterval();
        }
    }

    private void RefreshInterval()
    {
        float previousInterval = _interval;
        float progress = previousInterval > 0f ? _elapsed / previousInterval : 0f;
        Upgrade upgrade = UpgradeManager.Instance != null
            ? UpgradeManager.Instance.Get(EUpgradeType.AutoMergeTimeSub, ESlimeGrade.None)
            : null;
        _interval = Mathf.Max(
            MinimumInterval,
            BaseInterval - (float)(upgrade?.Point ?? 0d));
        _elapsed = Mathf.Clamp01(progress) * _interval;
        IntervalChanged?.Invoke(_interval);
    }
}
