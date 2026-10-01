using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// 도감 10종에서 열리는 합성 버튼. 플레이어가 누를 때마다 필드에서 낮은 등급부터
// 업그레이드 레벨만큼의 쌍을 한 번에 합친다.
//
// 처음에는 주기가 다 차면 알아서 발동했지만, 누르는 재미가 없고 화면을 보지 않는
// 동안에도 필드가 바뀌어 버렸다. 지금은 발동 시점을 플레이어가 정하고, 누른 순간부터
// 짧은 쿨타임을 센다.
public sealed class AutoMergeManager : MonoBehaviour
{
    private const float AutoMergeSoundInterval = 0.055f;
    private const float AutoMergeMinPitch = 0.96f;
    private const float AutoMergeMaxPitch = 1.06f;

    public enum EMergeFailure
    {
        None,
        // 해금 전이거나 튜토리얼·장식장 선택·가챠 연출처럼 지금 눌러선 안 되는 상황.
        Unavailable,
        // 연출 중이거나 쿨타임이 남았다.
        Cooldown,
        // 합성할 수 있는 쌍이 없다.
        NoPair,
    }

    public static AutoMergeManager Instance { get; private set; }

    [SerializeField] private DisplayRoomUI _displayRoomUI;
    [SerializeField] private GachaResultDirector _gachaResultDirector;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private SpawnManager _spawnManager;
    [SerializeField] private MergeManager _mergeManager;
    [SerializeField] private UpgradeManager _upgradeManager;

    [Tooltip("다시 누를 수 있기까지의 시간입니다. 누른 순간부터 흐릅니다.")]
    [SerializeField, Min(0f)] private float _cooldown = 0.5f;

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

    // 다시 누를 수 있기까지 남은 시간과 그 전체 길이. 누른 순간부터 흐른다.
    // 연출 중에는 어차피 누를 수 없으므로 쿨타임을 연출과 같이 흘려보내고, 연출이
    // 쿨타임보다 길면 연출이 끝나는 시점에 맞춘다.
    private float _remainingWait;
    private float _waitDuration;
    // 이번 발동에서 저장 검증에 거절된 개체. 다음 발동 시작 때 비워 일시적으로만
    // 제외한다. 영구 제외하면 일시적인 런타임 불일치가 정상 슬라임을 막아 버린다.
    private readonly HashSet<string> _rejectedIds = new();
    // 두 슬라임이 모이는 연출 중. 이때는 대기 시간이 끝나도 누를 수 없다.
    private bool _isPresenting;
    private Sequence _presentation;
    private readonly List<PresentationPair> _presentationPairs = new();
    // 자동합성이 SetPresentationLocked(true)로 실제 잠근 슬라임을 추적한다. 캐시된
    // IsPresented가 아니라 이 목록을 해제 근거로 삼아, 연출이 어떤 경로로 끝나든
    // 잠금이 남지 않게 한다.
    private readonly List<SlimeController> _lockedSlimes = new();

    private sealed class PresentationPair
    {
        public SlimeController Keeper { get; }
        public SlimeController Removed { get; }
        public ESlimeGrade FromGrade { get; }
        public Vector3 Center { get; }
        // 지금 화면에 보이는 쌍인지. 아니면 연출 없이 합성만 한다.
        public bool IsPresented { get; }

        public PresentationPair(SlimeController keeper, SlimeController removed)
        {
            Keeper = keeper;
            Removed = removed;
            FromGrade = keeper.Grade;
            Center = (keeper.transform.position + removed.transform.position) * 0.5f;
            IsPresented = keeper.IsMainFieldActive && removed.IsMainFieldActive;
        }
    }

    // 다시 누를 수 있기까지의 진행도. 1이면 준비된 상태다. 버튼 테두리 게이지가 쓴다.
    public float Progress01 => _waitDuration > 0f
        ? Mathf.Clamp01(1f - _remainingWait / _waitDuration)
        : 1f;
    public bool IsReady => !_isPresenting && _remainingWait <= 0f;
    // 두 슬라임이 모이는 연출이 도는 중인가. 도감 마일스톤 안내가 연출이 끝나기를 기다릴 때 쓴다.
    public bool IsPresenting => _isPresenting;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_slimeManager == null || _spawnManager == null ||
            _mergeManager == null || _upgradeManager == null)
        {
            Debug.LogError("자동 합성 매니저의 필수 씬 참조가 비어 있습니다.", this);
            enabled = false;
        }
    }

    private void OnDestroy()
    {
        _presentation?.Kill();
        _presentation = null;
        // Kill()은 OnComplete(CompletePresentation)를 부르지 않으므로, 연출 중단·파괴
        // 경로에서는 여기서 명시적으로 잠금을 해제해야 한다.
        ReleaseAllPresentationLocks();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_remainingWait <= 0f) return;

        _remainingWait = Mathf.Max(0f, _remainingWait - Time.deltaTime);
    }

    // 버튼이 부른다. 실패하면 이유를 돌려주어 호출부가 안내 문구를 고르게 한다.
    public EMergeFailure TryMerge()
    {
        if (!IsAvailable()) return EMergeFailure.Unavailable;
        if (!IsReady) return EMergeFailure.Cooldown;
        if (!ExecuteMerge()) return EMergeFailure.NoPair;

        return EMergeFailure.None;
    }

    // 지금 눌러도 되는 상황인가. 쿨타임과 연출은 따로 본다.
    public bool IsAvailable()
    {
        return GameplayGate.IsMainFieldReady &&
               // 도감 10종 안내는 이 버튼을 직접 눌러 보게 한다. 다른 튜토리얼은
               // 여전히 막는다.
               (!TutorialManager.IsRunning ||
                TutorialManager.IsActive(TutorialIds.CollectionAutoMerge)) &&
               _slimeManager != null &&
               _slimeManager.IsAutoMergeUnlocked &&
               (_displayRoomUI == null || !_displayRoomUI.IsSendMode) &&
               (_gachaResultDirector == null || !_gachaResultDirector.IsPlaying);
    }

    // 낮은 등급부터 훑어 현재 업그레이드 레벨이 허용하는 수만큼 쌍을 고른다.
    // 한 슬라임은 한 번만 고르므로 이번 결과가 같은 발동에서 다시 합성되지 않는다.
    private bool ExecuteMerge()
    {
        if (_spawnManager == null || _mergeManager == null) return false;

        // 이전 발동의 거부 결과는 다음 발동에서 다시 검증한다. 저장 상태가 화면과
        // 잠깐 어긋났다가 회복된 경우에도 정상 슬라임이 계속 막히지 않게 한다.
        _rejectedIds.Clear();

        var byGrade = new SortedDictionary<ESlimeGrade, List<SlimeController>>();
        var seenInstanceIds = new HashSet<string>();
        foreach (SlimeController target in _spawnManager.GetActiveTargets())
        {
            if (target == null || target.IsDragging || target.IsSpecial ||
                !target.HasLanded ||
                target.Location != ESlimeLocation.MainField ||
                target.Grade >= ESlimeGrade.Count - 1 ||
                string.IsNullOrEmpty(target.InstanceId) ||
                _rejectedIds.Contains(target.InstanceId))
            {
                continue;
            }

            // 풀링·복원 과정에서 같은 저장 ID를 가진 화면 객체가 잠깐 겹쳐도 한
            // 발동의 합성 요청에는 같은 ID를 한 번만 넣는다.
            if (!seenInstanceIds.Add(target.InstanceId)) continue;

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
    //
    // 장식장을 보고 있는 동안에는 메인 필드가 화면에 없다. 그 사이 고른 쌍에
    // 연출을 붙이면 이펙트만 엉뚱한 화면에 나타나므로, 그 쌍은 잠그지도 움직이지도 않고
    // 같은 저장에 묶어 합성만 한다.
    private void BeginPresentation(IReadOnlyList<MergeManager.MergeTargetPair> pairs)
    {
        _isPresenting = true;
        _presentationPairs.Clear();
        // 이전 연출이 어떤 이유로 목록을 비우지 못했다면 새로 잠그기 전에 정리한다.
        // 정상 경로에서는 목록이 비어 있어 무해하다.
        ReleaseAllPresentationLocks();

        _presentation?.Kill();
        _presentation = DOTween.Sequence();
        bool hasPresentedPair = false;
        foreach (MergeManager.MergeTargetPair pair in pairs)
        {
            var presentationPair = new PresentationPair(pair.Keeper, pair.Removed);
            _presentationPairs.Add(presentationPair);
            if (!presentationPair.IsPresented) continue;

            hasPresentedPair = true;
            pair.Keeper.SetPresentationLocked(true);
            pair.Removed.SetPresentationLocked(true);
            // 잠근 시점과 등록 시점을 동일 지점으로 묶는다. IsPresented가 false라
            // 잠그지 않는 쌍은 목록에도 넣지 않아 화면 밖 쌍을 보존한다.
            _lockedSlimes.Add(pair.Keeper);
            _lockedSlimes.Add(pair.Removed);
            _presentation
                .Join(pair.Keeper.transform
                    .DOMove(presentationPair.Center, _gatherDuration)
                    .SetEase(Ease.InQuad))
                .Join(pair.Removed.transform
                    .DOMove(presentationPair.Center, _gatherDuration)
                    .SetEase(Ease.InQuad));
        }

        // 대기 시간은 누른 지금부터 센다. 연출이 더 길면 그 길이에 맞춰야 게이지가 다
        // 찬 뒤에도 눌리지 않는 구간이 생기지 않는다.
        _waitDuration = hasPresentedPair
            ? Mathf.Max(_cooldown, _gatherDuration)
            : _cooldown;
        _remainingWait = _waitDuration;

        if (!hasPresentedPair)
        {
            _presentation.Kill();
            CompletePresentation();
            return;
        }

        _presentation.OnComplete(CompletePresentation);
    }

    private void CompletePresentation()
    {
        _presentation = null;

        // 모으는 동안 필드가 바뀐다. 한쪽이 손으로 합성되거나 장식장으로 갔으면
        // 그 쌍은 이번에 시도하지 않는다.
        var attempted = new List<PresentationPair>(_presentationPairs.Count);
        var attemptedTargets =
            new List<MergeManager.MergeTargetPair>(_presentationPairs.Count);
        foreach (PresentationPair pair in _presentationPairs)
        {
            if (pair.Keeper == null || pair.Removed == null ||
                !pair.Keeper.gameObject.activeInHierarchy ||
                !pair.Removed.gameObject.activeInHierarchy)
            {
                continue;
            }

            attempted.Add(pair);
            attemptedTargets.Add(
                new MergeManager.MergeTargetPair(pair.Keeper, pair.Removed));
        }

        bool canMerge = _mergeManager != null && attempted.Count > 0;
        int mergedPairCount = 0;
        bool merged = canMerge &&
                      _mergeManager.MergeBatch(
                          attemptedTargets,
                          out mergedPairCount);

        // 시도한 쌍마다의 결과. 저장이 실제로 거절한 쌍만 참이 된다.
        //
        // 오브젝트 상태로 짐작하면 안 된다. 시도조차 못 한 쌍도 "합성되지 않은" 상태로
        // 보이는데, 그것까지 거절로 세면 이번 발동 뒤에도 멀쩡히 남은 짝이 자동
        // 합성에서 빠진다. 제외 목록은 다음 발동 시작 때 비워 다시 검증한다.
        var isRefused = new bool[attempted.Count];
        if (!merged && canMerge)
        {
            if (attempted.Count == 1)
            {
                // 한 쌍뿐이었으면 방금 그 시도가 곧 그 쌍의 결과다.
                isRefused[0] = true;
            }
            else
            {
                // 묶음 저장은 한 쌍이라도 거절되면 전부 되돌린다. 한 쌍씩 다시 시도해
                // 저장이 실제로 거절한 쌍만 가려낸다. 드문 경로라 쌍마다 저장해도 된다.
                for (int i = 0; i < attempted.Count; i++)
                {
                    bool pairMerged = _mergeManager.MergeBatch(
                        new[] { attemptedTargets[i] },
                        out int singleMergedPairCount);
                    isRefused[i] = !pairMerged;
                    merged |= pairMerged;
                    mergedPairCount += singleMergedPairCount;
                }
            }
        }

        if (mergedPairCount > 0)
        {
            _slimeManager.RecordAutoMergeUse();
            AudioManager.Instance?.PlaySFXRandomPitchSequence(
                EAudioSfx.SlimePromote,
                mergedPairCount,
                AutoMergeSoundInterval,
                AutoMergeMinPitch,
                AutoMergeMaxPitch);
        }

        foreach (PresentationPair pair in _presentationPairs)
        {
            int index = attempted.IndexOf(pair);
            bool wasAttempted = index >= 0;
            bool pairMerged = wasAttempted && !isRefused[index];

            // 모이는 동안 장식장으로 넘어갔으면 그 쌍도 이펙트를 띄우지 않는다.
            if (pairMerged && pair.IsPresented &&
                pair.Keeper != null && pair.Keeper.IsMainFieldActive)
            {
                PlayMergeEffect(
                    pair.Center,
                    pair.Keeper.GetComponent<SpriteRenderer>());
            }
            else if (wasAttempted && isRefused[index] &&
                     pair.Keeper != null && pair.Removed != null)
            {
                _rejectedIds.Add(pair.Keeper.InstanceId);
                _rejectedIds.Add(pair.Removed.InstanceId);
            }
        }

        _presentationPairs.Clear();
        // 실제로 잠근 대상(_lockedSlimes)을 근거로 한 번에 해제한다. 정상 완료 경로가
        // 이 지점을 지나므로, 잠근 쌍이 없어 즉시 완료되는 경로도 여기서 함께 커버된다.
        ReleaseAllPresentationLocks();
        _isPresenting = false;

        // 저장이 모든 쌍을 거절해 아무것도 합쳐지지 않았다면 기다리게 할 이유가 없으므로
        // 남은 대기 시간을 지워 바로 다시 누를 수 있게 둔다.
        if (!merged)
        {
            _remainingWait = 0f;
        }
    }

    // 자동합성이 SetPresentationLocked(true)로 잠근 슬라임을 여기서 한 번에 되돌린다.
    // 캐시된 IsPresented가 아니라 실제로 잠근 목록을 근거로 해제해, 연출이 어떤
    // 경로로 끝나든(정상 완료·합성으로 despawn·연출 중단·오브젝트 파괴) 잠금이 남지 않게 한다.
    // 이미 despawn되어 다른 슬라임으로 재사용된 오브젝트는 잠금이 풀려 있으므로 가드로 건너뛴다.
    private void ReleaseAllPresentationLocks()
    {
        foreach (SlimeController slime in _lockedSlimes)
        {
            if (slime != null && slime.IsPresentationLocked)
            {
                slime.SetPresentationLocked(false);
            }
        }

        _lockedSlimes.Clear();
    }

    // 레벨 하나에 한 쌍씩 늘어난다. 레벨 0이 1쌍이고 최대 레벨이 10쌍이다.
    public static int GetPairCountForLevel(int level)
    {
        return Mathf.Max(1, level + 1);
    }

    private int GetUpgradeLevel()
    {
        Upgrade upgrade = _upgradeManager != null
            ? _upgradeManager.Get(EUpgradeType.AutoMergePairAdd, ESlimeGrade.None)
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

}
