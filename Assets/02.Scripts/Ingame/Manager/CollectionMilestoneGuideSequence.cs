using UnityEngine;

// 도감 마일스톤에서 새로 열린 버튼을 한 번씩 알려 준다. 기획서 §19.
//
// 10종 자동 합성과 12종 티켓 회수는 "대화 한 번 + 버튼 가리키기"라는 같은 흐름이라
// 시퀀스를 둘로 나누지 않고 표 하나로 둔다. 다른 점은 실제로 눌러 보게 하는지뿐이다.
// 자동 합성은 누르지 않으면 아무 일도 일어나지 않으므로 한 번 눌러 보게 하고,
// 티켓 회수는 이미 아는 동작(티켓 탭)의 단축이라 위치만 알린다.
//
// 도감 등록은 합성 도중이나 가챠 연출 중에 일어난다. 그 자리에서 띄우면 연출과
// 스포트라이트가 겹치므로, 띄워도 되는 상태가 될 때까지 기다렸다가 낮은 쪽부터
// 하나씩 꺼낸다. 가챠 연출에는 끝을 알리는 이벤트가 없어 이벤트만으로 기다리면 다음
// 실행까지 밀리므로, 남은 안내가 있는 동안에만 도는 Update로 기다린다.
public sealed class CollectionMilestoneGuideSequence : TutorialSequenceBase
{
    private enum Step
    {
        None,
        Dialogue,
        Button,
    }

    private sealed class Milestone
    {
        public string TutorialId { get; }
        public int RequiredCount { get; }
        public DialogueId Dialogue { get; }
        // 버튼을 실제로 눌러야 끝나는가. 아니면 위치만 보여 주고 대화로 끝낸다.
        public bool RequiresPress { get; }

        public Milestone(
            string tutorialId,
            int requiredCount,
            DialogueId dialogue,
            bool requiresPress)
        {
            TutorialId = tutorialId;
            RequiredCount = requiredCount;
            Dialogue = dialogue;
            RequiresPress = requiresPress;
        }
    }

    [SerializeField] private AutoMergeButtonUI _autoMergeButton;
    [SerializeField] private GachaTicketCollectButtonUI _ticketCollectButton;
    [SerializeField] private BottomPanelSwitcher _panelSwitcher;
    [SerializeField] private GachaResultDirector _gachaResultDirector;
    [SerializeField] private UnlockPopupUI _unlockPopupUI;
    [SerializeField] private Clicker _clicker;

    private Milestone[] _milestones;
    private Milestone _active;
    private Step _step;
    private bool _isPressSubscribed;

    // TutorialManager가 시작 시점에 이 값을 읽어 진행 중인 튜토리얼을 기록한다.
    // 그래서 Begin은 TryBeginTutorial 전에 _active부터 정한다.
    public override string TutorialId => _active != null
        ? _active.TutorialId
        : TutorialIds.CollectionAutoMerge;

    protected override void Awake()
    {
        base.Awake();
        if (!enabled) return;

        if (_autoMergeButton == null || _ticketCollectButton == null ||
            _panelSwitcher == null || _clicker == null)
        {
            Debug.LogError("도감 마일스톤 안내의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _milestones = new[]
        {
            new Milestone(
                TutorialIds.CollectionAutoMerge,
                NormalCollectionRules.AutoMergeCount,
                DialogueId.CollectionAutoMerge,
                requiresPress: true),
            new Milestone(
                TutorialIds.CollectionTicketCollect,
                NormalCollectionRules.TicketBulkCollectCount,
                DialogueId.CollectionTicketCollect,
                requiresPress: false),
        };
    }

    private void OnDestroy()
    {
        UnsubscribePress();
        _clicker?.ReleaseMode(this);
    }

    private void Update()
    {
        if (_step != Step.None) return;

        Milestone pending = FindPending();
        if (pending == null)
        {
            // 둘 다 끝났으면 다시 생길 일이 없다. 매 프레임 확인할 이유도 없다.
            if (IsEverythingCompleted()) enabled = false;
            return;
        }

        if (!CanPresent()) return;

        Begin(pending);
    }

    // 도감 수가 모자라거나 이미 본 안내는 건너뛴다. 남은 것 중 낮은 쪽부터 고른다.
    private Milestone FindPending()
    {
        SlimeManager slimeManager = SlimeManager.Instance;
        if (slimeManager == null || !TutorialProgress.IsInitialized) return null;

        foreach (Milestone milestone in _milestones)
        {
            if (slimeManager.NormalCollectionCount >= milestone.RequiredCount &&
                TutorialProgress.CanStart(milestone.TutorialId))
            {
                return milestone;
            }
        }

        return null;
    }

    private bool IsEverythingCompleted()
    {
        // FindPending은 매니저가 아직 없으면 표를 건드리기 전에 빠져나간다. 그래서
        // Update가 곧바로 여기로 넘어오는데, 그 시점에는 표가 없을 수 있다.
        // 초기화 전 첫 프레임과, 플레이 도중 스크립트가 다시 컴파일되어 Awake 없이
        // 살아난 경우가 모두 이 자리를 지난다.
        //
        // 아직 모른다는 뜻이므로 완료로 보지 않는다. 다음 프레임에 다시 묻는다.
        if (_milestones == null) return false;

        foreach (Milestone milestone in _milestones)
        {
            if (!TutorialProgress.IsCompleted(milestone.TutorialId)) return false;
        }

        return true;
    }

    // 가리킬 버튼이 화면에 있고, 다른 전면 연출이 없는 상태인가.
    private bool CanPresent()
    {
        return GameplayGate.IsMainFieldReady &&
               _panelSwitcher.IsAreaVisible &&
               SpawnManager.Instance != null &&
               SpawnManager.Instance.IsInitialized &&
               GameplaySpaceManager.Instance != null &&
               GameplaySpaceManager.Instance.IsMainFieldActive &&
               !GameplaySpaceManager.Instance.IsTransitioning &&
               (_unlockPopupUI == null || !_unlockPopupUI.IsPresenting) &&
               (_gachaResultDirector == null || !_gachaResultDirector.IsPlaying);
    }

    private void Begin(Milestone milestone)
    {
        // 해금 직후에는 버튼이 아직 켜지지 않았을 수 있다. 꺼진 대상을 가리키면
        // 크기가 0인 구멍만 남는다.
        RectTransform target = GetTarget(milestone);
        if (target == null || !target.gameObject.activeInHierarchy) return;

        _active = milestone;
        if (!TryBeginTutorial())
        {
            _active = null;
            return;
        }

        _step = Step.Dialogue;
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);

        if (milestone.RequiresPress)
        {
            Spotlight.Hide();
            ShowDialogue(Content.GetDialogue(milestone.Dialogue), ShowButtonStep);
            return;
        }

        // 누를 필요가 없으면 가리킨 채로 대화만 하고 끝낸다.
        Spotlight.ShowUiFocus(target);
        ShowDialogue(
            Content.GetDialogue(milestone.Dialogue),
            Complete,
            keepGuideVisible: true);
    }

    private void ShowButtonStep()
    {
        if (_active == null) return;

        RectTransform target = GetTarget(_active);
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Complete();
            return;
        }

        _step = Step.Button;
        SubscribePress();
        Spotlight.ShowUiTarget(
            Content.AutoMergeButtonMessage,
            target,
            SpotlightInteractionMode.PassThroughPrimary);
    }

    // 합성에 성공했는지는 보지 않는다. 합성할 쌍이 없는 순간에도 버튼은 안내 문구를
    // 띄우므로, 성공만 기다리면 그 상태에서 안내가 영영 끝나지 않는다.
    private void OnAutoMergePressed(AutoMergeManager.EMergeFailure failure)
    {
        if (_step != Step.Button) return;

        Complete();
    }

    private RectTransform GetTarget(Milestone milestone)
    {
        return milestone.RequiresPress
            ? _autoMergeButton.ButtonTarget
            : _ticketCollectButton.ButtonTarget;
    }

    private void SubscribePress()
    {
        if (_isPressSubscribed) return;

        _autoMergeButton.Pressed += OnAutoMergePressed;
        _isPressSubscribed = true;
    }

    private void UnsubscribePress()
    {
        if (!_isPressSubscribed) return;

        if (_autoMergeButton != null)
        {
            _autoMergeButton.Pressed -= OnAutoMergePressed;
        }

        _isPressSubscribed = false;
    }

    // 남은 안내가 있으면 다음 프레임에 Update가 이어서 고른다.
    private void Complete()
    {
        if (_active == null) return;

        UnsubscribePress();
        TutorialProgress.MarkCompleted(_active.TutorialId);
        _active = null;
        _step = Step.None;
        _clicker.ReleaseMode(this);
        CompleteTutorial();
    }
}
