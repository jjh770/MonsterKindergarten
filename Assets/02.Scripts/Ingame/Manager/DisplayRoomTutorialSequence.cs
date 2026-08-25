using UnityEngine;

public sealed class DisplayRoomTutorialSequence : TutorialSequenceBase
{
    private enum Step
    {
        None,
        Dialogue,
        MenuButton,
        SendButton,
        SlimeSelection,
        EnterButton,
        EnterTransition,
        SlimeInfo,
        InfoSummary,
        ObserveButton,
        TakeOutButton,
        CloseButton,
        Complete,
    }

    [SerializeField] private UnlockPopupUI _unlockPopupUI;
    [SerializeField] private UpgradeUI _upgradeUI;
    [SerializeField] private DisplayRoomUI _displayRoomUI;
    [SerializeField] private DisplayRoomInfoUI _displayRoomInfoUI;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private AutoClicker _autoClicker;

    private SlimeController _tutorialSlime;
    private Step _step;
    private bool _isGuideSubscribed;

    private void Start()
    {
        if (_unlockPopupUI != null)
        {
            _unlockPopupUI.PresentationCompleted += OnUnlockPresentationCompleted;
        }

        if (_displayRoomUI != null)
        {
            _displayRoomUI.MovePanelOpened += OnMovePanelOpened;
            _displayRoomUI.SendModeStarted += OnSendModeStarted;
            _displayRoomUI.SendModeEnded += OnSendModeEnded;
            _displayRoomUI.SlimeTransferred += OnSlimeTransferred;
        }

        if (_displayRoomInfoUI != null)
        {
            _displayRoomInfoUI.InfoOpening += OnInfoOpening;
            _displayRoomInfoUI.InfoOpened += OnInfoOpened;
            _displayRoomInfoUI.InfoClosed += OnInfoClosed;
        }

        if (StageManager.Instance != null)
        {
            StageManager.Instance.SpaceChanged += OnSpaceChanged;
            StageManager.Instance.SpaceTransitionCompleted += OnSpaceTransitionCompleted;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameplayActivated += TryStart;
        }

        TryStart();
    }

    private void OnDestroy()
    {
        if (_unlockPopupUI != null)
        {
            _unlockPopupUI.PresentationCompleted -= OnUnlockPresentationCompleted;
        }

        if (_displayRoomUI != null)
        {
            _displayRoomUI.MovePanelOpened -= OnMovePanelOpened;
            _displayRoomUI.SendModeStarted -= OnSendModeStarted;
            _displayRoomUI.SendModeEnded -= OnSendModeEnded;
            _displayRoomUI.SlimeTransferred -= OnSlimeTransferred;
        }

        if (_displayRoomInfoUI != null)
        {
            _displayRoomInfoUI.InfoOpening -= OnInfoOpening;
            _displayRoomInfoUI.InfoOpened -= OnInfoOpened;
            _displayRoomInfoUI.InfoClosed -= OnInfoClosed;
        }

        if (StageManager.Instance != null)
        {
            StageManager.Instance.SpaceChanged -= OnSpaceChanged;
            StageManager.Instance.SpaceTransitionCompleted -= OnSpaceTransitionCompleted;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameplayActivated -= TryStart;
        }

        UnsubscribeGuide();
        if (_step != Step.None && _step != Step.Complete)
        {
            SpawnManager.Instance?.SetSpawningPaused(false);
            _autoClicker?.SetPaused(false);
        }
    }

    private void OnUnlockPresentationCompleted(ESlimeGrade grade)
    {
        TryStart();
    }

    private void TryStart()
    {
        if ((_step != Step.None && _step != Step.Complete) ||
            GameManager.Instance == null ||
            !GameManager.Instance.IsGameplayActive ||
            TutorialProgress.ShouldRun(TutorialIds.Main) ||
            !TutorialProgress.ShouldRun(TutorialIds.DisplayRoom) ||
            SlimeManager.Instance == null ||
            !SlimeManager.Instance.IsDisplayRoomUnlocked ||
            StageManager.Instance == null ||
            !StageManager.Instance.IsMainStageActive ||
            StageManager.Instance.IsTransitioning ||
            (_unlockPopupUI != null && _unlockPopupUI.IsPresenting))
        {
            return;
        }

        if (_displayRoomUI == null ||
            _displayRoomInfoUI == null ||
            _clicker == null ||
            _upgradeUI == null)
        {
            Debug.LogError("장식장 튜토리얼의 필수 참조가 비어 있습니다.", this);
            return;
        }

        if (!TryBeginTutorial()) return;

        _tutorialSlime = FindFirstDisplayRoomSlime();
        Spotlight.AdvanceRequested += OnGuideAdvanceRequested;
        _isGuideSubscribed = true;
        SpawnManager.Instance?.SetSpawningPaused(true);
        _autoClicker?.SetPaused(true);
        _upgradeUI.SetToggleInputEnabled(false);
        ShowStepDialogue(
            Content.GetDialogue(DialogueId.DisplayRoomUnlocked),
            ShowDisplayRoomButtonStep);
    }

    private void ShowStepDialogue(
        System.Collections.Generic.IReadOnlyList<DialogueLine> lines,
        System.Action onComplete)
    {
        _step = Step.Dialogue;
        _clicker.SetInputMode(false, false);
        ShowDialogue(lines, onComplete);
    }

    private void ShowDisplayRoomButtonStep()
    {
        if (_displayRoomUI.IsMovePanelOpen)
        {
            if (_tutorialSlime != null)
            {
                ShowEnterButtonStep();
            }
            else
            {
                ShowSendButtonStep();
            }
            return;
        }

        RectTransform target = _displayRoomUI.PanelSwitchButtonTarget;
        if (target == null)
        {
            Abort("강조할 하단 패널 전환 버튼이 없습니다.");
            return;
        }

        _step = Step.MenuButton;
        Spotlight.ShowUiTarget(
            Content.DisplayRoomButtonMessage,
            target,
            SpotlightInteractionMode.PassThroughPrimary,
            useRectangularHole: true);
    }

    private void OnGuideAdvanceRequested()
    {
        if (_step == Step.ObserveButton)
        {
            ShowTakeOutButtonStep();
        }
        else if (_step == Step.InfoSummary)
        {
            ShowObserveButtonStep();
        }
        else if (_step == Step.TakeOutButton)
        {
            ShowCloseButtonStep();
        }
    }

    private void OnMovePanelOpened()
    {
        if (_step == Step.MenuButton)
        {
            if (_tutorialSlime != null)
            {
                ShowEnterButtonStep();
            }
            else
            {
                ShowSendButtonStep();
            }
        }
    }

    private void ShowSendButtonStep()
    {
        RectTransform target = _displayRoomUI.SendButtonTarget;
        if (target == null)
        {
            Abort("강조할 장식장 입고 버튼이 없습니다.");
            return;
        }

        _step = Step.SendButton;
        Spotlight.ShowUiTarget(
            Content.DisplayRoomSendButtonMessage,
            target,
            SpotlightInteractionMode.PassThroughPrimary,
            useRectangularHole: true);
    }

    private void OnSendModeStarted()
    {
        if (_step != Step.SendButton) return;

        _tutorialSlime = FindTransferCandidate();
        if (_tutorialSlime == null)
        {
            _displayRoomUI.CancelSendMode();
            Abort("장식장으로 보낼 수 있는 슬라임이 없습니다.");
            return;
        }

        _step = Step.SlimeSelection;
        _clicker.SetInputMode(
            true,
            false,
            _tutorialSlime,
            invokeClickAction: false);
        Spotlight.Show(Content.DisplayRoomSelectSlimeMessage, _tutorialSlime.transform);
    }

    private void OnSendModeEnded()
    {
        if (_step == Step.SlimeSelection)
        {
            ShowSendButtonStep();
        }
    }

    private void OnSlimeTransferred(SlimeController target)
    {
        if (_step != Step.SlimeSelection || target != _tutorialSlime) return;

        _step = Step.EnterButton;
        Spotlight.Hide();
        _displayRoomUI.CancelSendMode();
        ShowEnterButtonStep();
    }

    private void ShowEnterButtonStep()
    {
        RectTransform target = _displayRoomUI.SpaceButtonTarget;
        if (target == null)
        {
            Abort("강조할 장식장 이동 버튼이 없습니다.");
            return;
        }

        _step = Step.EnterButton;
        _clicker.SetInputMode(false, false);
        Spotlight.ShowUiTarget(
            Content.DisplayRoomEnterMessage,
            target,
            SpotlightInteractionMode.PassThroughPrimary,
            useRectangularHole: true);
    }

    private void OnSpaceChanged(EGameplaySpace space)
    {
        if (_step == Step.EnterButton && space == EGameplaySpace.DisplayRoom)
        {
            _step = Step.EnterTransition;
            Spotlight.Hide();
            _clicker.SetInputMode(false, false);
            return;
        }

        // 장식장 단계 도중 공간을 벗어나면 안내할 대상이 화면에서 사라진다.
        if (space != EGameplaySpace.DisplayRoom && IsDisplayRoomStep(_step))
        {
            Abort("장식장 튜토리얼 도중 공간을 벗어났습니다.");
        }
    }

    private static bool IsDisplayRoomStep(Step step)
    {
        return step == Step.EnterTransition ||
               step == Step.SlimeInfo ||
               step == Step.InfoSummary ||
               step == Step.ObserveButton ||
               step == Step.TakeOutButton ||
               step == Step.CloseButton;
    }

    private void OnSpaceTransitionCompleted()
    {
        if (_step != Step.EnterTransition ||
            StageManager.Instance == null ||
            StageManager.Instance.CurrentSpace != EGameplaySpace.DisplayRoom)
        {
            return;
        }

        if (_tutorialSlime == null ||
            _tutorialSlime.Location != ESlimeLocation.DisplayRoom)
        {
            _tutorialSlime = FindFirstDisplayRoomSlime();
        }

        if (_tutorialSlime == null)
        {
            Abort("정보를 확인할 장식장 슬라임이 없습니다.");
            return;
        }

        _step = Step.SlimeInfo;
        _clicker.SetInputMode(
            true,
            false,
            _tutorialSlime,
            invokeClickAction: false);
        Spotlight.Show(Content.DisplayRoomInfoMessage, _tutorialSlime.transform);
    }

    // 확대 연출 동안에는 직전 안내 말풍선을 띄워 두지 않는다.
    private void OnInfoOpening(SlimeController target)
    {
        if (_step != Step.SlimeInfo || target != _tutorialSlime) return;

        Spotlight.Hide();
    }

    private void OnInfoOpened(SlimeController target)
    {
        if (_step != Step.SlimeInfo || target != _tutorialSlime) return;

        ShowInfoSummaryStep();
    }

    private void ShowInfoSummaryStep()
    {
        RectTransform target = _displayRoomInfoUI.InfoSummaryTarget;
        if (target == null)
        {
            ShowObserveButtonStep();
            return;
        }

        _step = Step.InfoSummary;
        Spotlight.ShowUiTarget(
            Content.DisplayRoomInfoSummaryMessage,
            target,
            SpotlightInteractionMode.AdvanceOnPrimaryTap,
            useRectangularHole: true);
    }

    private void ShowObserveButtonStep()
    {
        RectTransform target = _displayRoomInfoUI.ObserveButtonTarget;
        if (target == null)
        {
            ShowTakeOutButtonStep();
            return;
        }

        _step = Step.ObserveButton;
        Spotlight.ShowUiTarget(
            Content.DisplayRoomObserveMessage,
            target,
            SpotlightInteractionMode.AdvanceOnPrimaryTap,
            useRectangularHole: true);
    }

    private void ShowTakeOutButtonStep()
    {
        RectTransform target = _displayRoomInfoUI.TakeOutButtonTarget;
        if (target == null)
        {
            ShowCloseButtonStep();
            return;
        }

        _step = Step.TakeOutButton;
        Spotlight.ShowUiTarget(
            Content.DisplayRoomTakeOutMessage,
            target,
            SpotlightInteractionMode.AdvanceOnPrimaryTap,
            useRectangularHole: true);
    }

    private void ShowCloseButtonStep()
    {
        RectTransform target = _displayRoomInfoUI.CloseButtonTarget;
        if (target == null)
        {
            ShowFinalDialogue();
            return;
        }

        _step = Step.CloseButton;
        Spotlight.ShowUiTarget(
            Content.DisplayRoomCloseMessage,
            target,
            SpotlightInteractionMode.PassThroughPrimary,
            useRectangularHole: true);
    }

    // 닫기 버튼뿐 아니라 뒤로가기로 닫아도 안내가 멈추지 않게 한다.
    // 패널이 사라지면 남은 버튼 단계는 가리킬 대상이 없으므로 마무리로 넘어간다.
    private void OnInfoClosed()
    {
        if (_step != Step.InfoSummary &&
            _step != Step.ObserveButton &&
            _step != Step.TakeOutButton &&
            _step != Step.CloseButton)
        {
            return;
        }

        ShowFinalDialogue();
    }

    private void ShowFinalDialogue()
    {
        Spotlight.Hide();
        ShowStepDialogue(
            Content.GetDialogue(DialogueId.DisplayRoomFinal),
            Complete);
    }

    private void Complete()
    {
        TutorialProgress.MarkCompleted(TutorialIds.DisplayRoom);
        UnsubscribeGuide();
        _step = Step.Complete;
        _tutorialSlime = null;
        SpawnManager.Instance?.SetSpawningPaused(false);
        _autoClicker?.SetPaused(false);
        // 소유권을 먼저 반납해야 RefreshInteraction이 입력을 되돌린다.
        CompleteTutorial();
        StageManager.Instance?.RefreshInteraction();
    }

    private void Abort(string message)
    {
        Debug.LogWarning(message, this);
        Spotlight?.Hide();
        UnsubscribeGuide();
        _step = Step.Complete;
        _tutorialSlime = null;
        SpawnManager.Instance?.SetSpawningPaused(false);
        _autoClicker?.SetPaused(false);
        CompleteTutorial();
        StageManager.Instance?.RefreshInteraction();
    }

    private static SlimeController FindTransferCandidate()
    {
        if (SlimeSpawner.Instance == null || SlimeManager.Instance == null)
        {
            return null;
        }

        foreach (SlimeController target in SlimeSpawner.Instance.GetActiveTargets())
        {
            if (target != null &&
                target.Location == ESlimeLocation.MainStage &&
                target.IsCurrentStageActive &&
                SlimeManager.Instance.CanMoveToDisplayRoom(
                    target.Grade,
                    target.IsSpecial))
            {
                return target;
            }
        }

        return null;
    }

    private static SlimeController FindFirstDisplayRoomSlime()
    {
        if (SlimeSpawner.Instance == null) return null;

        foreach (SlimeController target in SlimeSpawner.Instance.GetActiveTargets())
        {
            if (target != null && target.Location == ESlimeLocation.DisplayRoom)
            {
                return target;
            }
        }

        return null;
    }

    private void UnsubscribeGuide()
    {
        if (!_isGuideSubscribed || Spotlight == null) return;

        Spotlight.AdvanceRequested -= OnGuideAdvanceRequested;
        _isGuideSubscribed = false;
    }
}
