using UnityEngine;

// 가챠 해금(최고 Lv.7) 안내와 첫 1회 체험. 기획서 §11.1.
//
// 자동 스폰을 끄고, 자리가 가득 찼으면 합성으로 한 칸을 만든 뒤, 지급한 티켓으로
// 직접 가챠를 실행해 결과 슬라임까지 확인한다. 일반 티켓 드랍은 이 흐름이 끝난 뒤
// GachaTicketDropper에서 시작한다.
public sealed class GachaTutorialSequence : TutorialSequenceBase
{
    private enum Step
    {
        None,
        Dialogue,
        AutoSpawn,
        MakeRoom,
        GachaButton,
        Result,
        Complete,
    }

    [SerializeField] private AutoSpawnToggleUI _autoSpawnToggle;
    [SerializeField] private GachaButtonUI _gachaButton;
    [SerializeField] private UnlockPopupUI _unlockPopupUI;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private AutoClicker _autoClicker;

    private Step _step;
    private bool _isGuideSubscribed;
    private bool _isMergeSubscribed;

    private void Start()
    {
        if (_unlockPopupUI != null)
        {
            _unlockPopupUI.PresentationCompleted += OnUnlockPresentationCompleted;
        }

        if (_autoSpawnToggle != null)
        {
            _autoSpawnToggle.StateChanged += OnAutoSpawnStateChanged;
        }

        if (_gachaButton != null)
        {
            _gachaButton.PullSucceeded += OnGachaPullSucceeded;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameplayActivated += TryStart;
        }

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.Initialized += TryStart;
        }

        TryStart();
    }

    private void OnDestroy()
    {
        if (_unlockPopupUI != null)
        {
            _unlockPopupUI.PresentationCompleted -= OnUnlockPresentationCompleted;
        }

        if (_autoSpawnToggle != null)
        {
            _autoSpawnToggle.StateChanged -= OnAutoSpawnStateChanged;
        }

        if (_gachaButton != null)
        {
            _gachaButton.PullSucceeded -= OnGachaPullSucceeded;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameplayActivated -= TryStart;
        }

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.Initialized -= TryStart;
        }

        UnsubscribeGuide();
        UnsubscribeMerge();
        _clicker?.ReleaseMode(this);
        if (_step != Step.None && _step != Step.Complete)
        {
            SpawnManager.Instance?.SetSpawningPaused(false);
            _autoClicker?.SetPaused(false);
        }
    }

    private void OnUnlockPresentationCompleted(ESlimeGrade grade)
    {
        if (_step == Step.MakeRoom &&
            SpawnManager.Instance != null &&
            SpawnManager.Instance.HasMainStageRoom())
        {
            ShowGachaButtonStep();
            return;
        }

        TryStart();
    }

    private void TryStart()
    {
        if (_step != Step.None ||
            GameManager.Instance == null ||
            !GameManager.Instance.IsGameplayActive ||
            SpawnManager.Instance == null ||
            !SpawnManager.Instance.IsInitialized ||
            TutorialProgress.ShouldRun(TutorialIds.Main) ||
            !TutorialProgress.ShouldRun(TutorialIds.Gacha) ||
            SlimeManager.Instance == null ||
            !SlimeManager.Instance.IsGachaUnlocked ||
            CurrencyManager.Instance == null ||
            StageManager.Instance == null ||
            !StageManager.Instance.IsMainStageActive ||
            StageManager.Instance.IsTransitioning ||
            (_unlockPopupUI != null && _unlockPopupUI.IsPresenting))
        {
            return;
        }

        if (_autoSpawnToggle == null ||
            _gachaButton == null ||
            _clicker == null ||
            _autoClicker == null)
        {
            Debug.LogError("가챠 튜토리얼의 필수 참조가 비어 있습니다.", this);
            return;
        }

        if (!TryBeginTutorial()) return;

        _step = Step.Dialogue;
        SpawnManager.Instance.SetSpawningPaused(true);
        _autoClicker.SetPaused(true);
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);
        Spotlight.Hide();

        if ((double)CurrencyManager.Instance.Get(ECurrencyType.GachaTicket) <= 0d)
        {
            CurrencyManager.Instance.Add(ECurrencyType.GachaTicket, 1d);
        }

        ShowDialogue(Content.GetDialogue(DialogueId.Gacha), ShowAutoSpawnStep);
    }

    private void ShowAutoSpawnStep()
    {
        RectTransform target = _autoSpawnToggle.ButtonTarget;
        if (target == null)
        {
            Abort("자동 스폰 버튼 참조가 없습니다.");
            return;
        }

        _step = Step.AutoSpawn;
        SubscribeGuide();

        bool isEnabled = SlimeManager.Instance.IsAutoSpawnEnabled;
        Spotlight.ShowUiTarget(
            Content.AutoSpawnToggleMessage,
            target,
            isEnabled
                ? SpotlightInteractionMode.PassThroughPrimary
                : SpotlightInteractionMode.AdvanceOnPrimaryTap,
            useRectangularHole: true);
    }

    private void OnAutoSpawnStateChanged(bool isEnabled)
    {
        if (_step != Step.AutoSpawn || isEnabled) return;

        ShowMakeRoomStep();
    }

    private void ShowMakeRoomStep()
    {
        UnsubscribeGuide();
        if (SpawnManager.Instance.HasMainStageRoom())
        {
            ShowGachaButtonStep();
            return;
        }

        if (!TryFindMergePair(out SlimeController first, out SlimeController second))
        {
            Abort("가챠 슬롯을 만들 합성 가능한 슬라임 두 마리를 찾지 못했습니다.");
            return;
        }

        _step = Step.MakeRoom;
        SubscribeMerge();
        _clicker.PushMode(
            this,
            ClickerInputMode.DragOnly(first, second),
            ClickerInputPriority.Tutorial);
        Spotlight.ShowWorldTargets(
            Content.GachaMakeRoomMessage,
            first.transform,
            second.transform);
    }

    private void OnMerged(
        SlimeController keeper,
        ESlimeGrade fromGrade,
        ESlimeGrade toGrade)
    {
        if (_step != Step.MakeRoom ||
            SpawnManager.Instance == null ||
            !SpawnManager.Instance.HasMainStageRoom())
        {
            return;
        }

        UnsubscribeMerge();
        Spotlight.Hide();
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);

        if (_unlockPopupUI != null && _unlockPopupUI.IsPresenting) return;

        ShowGachaButtonStep();
    }

    private void ShowGachaButtonStep()
    {
        UnsubscribeMerge();
        if (SpawnManager.Instance == null || !SpawnManager.Instance.HasMainStageRoom())
        {
            ShowMakeRoomStep();
            return;
        }

        RectTransform target = _gachaButton.ButtonTarget;
        if (target == null)
        {
            Abort("가챠 버튼 참조가 없습니다.");
            return;
        }

        _step = Step.GachaButton;
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);
        Spotlight.ShowUiTarget(
            Content.GachaButtonMessage,
            target,
            SpotlightInteractionMode.PassThroughPrimary,
            useRectangularHole: true);
    }

    private void OnGachaPullSucceeded(SlimeController spawned)
    {
        if (_step != Step.GachaButton || spawned == null) return;

        _step = Step.Result;
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);
        Spotlight.ShowFocus(spawned.transform);
        ShowDialogue(
            Content.GetDialogue(DialogueId.GachaResult),
            Complete,
            keepGuideVisible: true);
    }

    private void OnGuideAdvanceRequested()
    {
        if (_step != Step.AutoSpawn ||
            SlimeManager.Instance == null ||
            SlimeManager.Instance.IsAutoSpawnEnabled)
        {
            return;
        }

        ShowMakeRoomStep();
    }

    private void SubscribeGuide()
    {
        if (_isGuideSubscribed) return;

        Spotlight.AdvanceRequested += OnGuideAdvanceRequested;
        _isGuideSubscribed = true;
    }

    private void UnsubscribeGuide()
    {
        if (!_isGuideSubscribed) return;

        _isGuideSubscribed = false;
        if (Spotlight != null)
        {
            Spotlight.AdvanceRequested -= OnGuideAdvanceRequested;
        }
    }

    private void SubscribeMerge()
    {
        if (_isMergeSubscribed) return;

        MergeManager.Merged += OnMerged;
        _isMergeSubscribed = true;
    }

    private void UnsubscribeMerge()
    {
        if (!_isMergeSubscribed) return;

        MergeManager.Merged -= OnMerged;
        _isMergeSubscribed = false;
    }

    private void Complete()
    {
        if (_step == Step.Complete) return;

        UnsubscribeGuide();
        UnsubscribeMerge();
        TutorialProgress.MarkCompleted(TutorialIds.Gacha);
        _step = Step.Complete;
        SpawnManager.Instance?.SetSpawningPaused(false);
        _autoClicker?.SetPaused(false);
        _clicker?.ReleaseMode(this);
        CompleteTutorial();
        StageManager.Instance?.RefreshInteraction();
    }

    private void Abort(string message)
    {
        Debug.LogWarning(message, this);
        Spotlight?.Hide();
        UnsubscribeGuide();
        UnsubscribeMerge();
        _step = Step.Complete;
        SpawnManager.Instance?.SetSpawningPaused(false);
        _autoClicker?.SetPaused(false);
        _clicker?.ReleaseMode(this);
        CompleteTutorial();
        StageManager.Instance?.RefreshInteraction();
    }

    private static bool TryFindMergePair(
        out SlimeController first,
        out SlimeController second)
    {
        first = null;
        second = null;
        if (SpawnManager.Instance == null) return false;

        ESlimeGrade bestGrade = ESlimeGrade.Count;
        var targets = SpawnManager.Instance.GetActiveTargets();
        for (int i = 0; i < targets.Count; i++)
        {
            SlimeController candidate = targets[i];
            if (candidate == null ||
                !candidate.IsCurrentStageActive ||
                candidate.IsSpecial)
            {
                continue;
            }

            for (int j = i + 1; j < targets.Count; j++)
            {
                SlimeController other = targets[j];
                if (other == null ||
                    !other.IsCurrentStageActive ||
                    other.IsSpecial ||
                    candidate.Grade >= bestGrade ||
                    !candidate.CanMergeWith(other))
                {
                    continue;
                }

                first = candidate;
                second = other;
                bestGrade = candidate.Grade;
            }
        }

        return first != null && second != null;
    }
}
