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
    private SlimeController _keeper;
    private SlimeController _removed;

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

    // 낮은 등급부터 훑어 처음으로 실제 합성에 성공한 쌍 하나만 처리하고 끝낸다.
    // 저장에 없는 슬라임처럼 합성이 거절되는 쌍이 있어도 그 등급만 건너뛴다.
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

        foreach (List<SlimeController> targets in byGrade.Values)
        {
            if (targets.Count < 2) continue;

            targets.Sort((left, right) => string.CompareOrdinal(
                left.InstanceId,
                right.InstanceId));
            BeginPresentation(targets[0], targets[1]);
            return true;
        }

        return false;
    }

    // 두 슬라임이 스스로 모여 합쳐지는 것처럼 보이게 한다. 모으는 동안에는 두 마리만
    // 잠가 터치와 드래그가 닿지 않게 하고, 합성은 둘이 만난 순간에 한다.
    private void BeginPresentation(SlimeController keeper, SlimeController removed)
    {
        _isPresenting = true;
        _keeper = keeper;
        _removed = removed;
        keeper.SetPresentationLocked(true);
        removed.SetPresentationLocked(true);

        Vector3 center =
            (keeper.transform.position + removed.transform.position) * 0.5f;

        _presentation?.Kill();
        _presentation = DOTween.Sequence()
            .Join(keeper.transform.DOMove(center, _gatherDuration).SetEase(Ease.InQuad))
            .Join(removed.transform.DOMove(center, _gatherDuration).SetEase(Ease.InQuad))
            .OnComplete(() => CompletePresentation(center));
    }

    private void CompletePresentation(Vector3 center)
    {
        _presentation = null;

        bool merged = _keeper != null &&
                      _removed != null &&
                      _keeper.gameObject.activeInHierarchy &&
                      _removed.gameObject.activeInHierarchy &&
                      MergeManager.Instance != null &&
                      MergeManager.Instance.MergeBatch(new[]
                      {
                          new MergeManager.MergeTargetPair(_keeper, _removed),
                      });

        if (merged)
        {
            PlayMergeEffect(center, _keeper.GetComponent<SpriteRenderer>());
        }
        else if (_keeper != null && _removed != null)
        {
            _rejectedIds.Add(_keeper.InstanceId);
            _rejectedIds.Add(_removed.InstanceId);
        }

        // 사라진 쪽도 풀로 돌아가 다시 쓰이므로 잠금을 되돌린다.
        _keeper?.SetPresentationLocked(false);
        _removed?.SetPresentationLocked(false);
        _keeper = null;
        _removed = null;
        _isPresenting = false;

        // 다음 주기는 합쳐진 슬라임이 나온 뒤부터 센다.
        _elapsed = merged ? 0f : _interval;
        _isPending = !merged;
        _pendingRetryTimer = 0f;
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
