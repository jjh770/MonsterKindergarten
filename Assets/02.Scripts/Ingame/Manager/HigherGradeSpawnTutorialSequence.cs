using UnityEngine;

public sealed class HigherGradeSpawnTutorialSequence : TutorialSequenceBase
{
    public override string TutorialId => TutorialIds.HigherGradeSpawn;

    private enum Step
    {
        None,
        Dialogue,
        PoolButton,
        ScholarMenu,
        Carousel,
        Complete,
    }

    [SerializeField] private SpawnSliderUI _spawnSliderUI;
    [SerializeField] private SystemUpgradePanel _systemUpgradePanel;
    [SerializeField] private BottomPanelSwitcher _panelSwitcher;
    [SerializeField] private UnlockPopupUI _unlockPopupUI;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private AutoClicker _autoClicker;
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private SpawnManager _spawnManager;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private GameplaySpaceManager _gameplaySpaceManager;

    private Step _step;

    private void Start()
    {
        if (_gameManager == null || _spawnManager == null || _slimeManager == null ||
            _gameplaySpaceManager == null)
        {
            Debug.LogError("상위 슬라임 등장 튜토리얼의 GameScene 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        TutorialManager.Finished += TryStart;

        if (_unlockPopupUI != null)
        {
            _unlockPopupUI.PresentationCompleted += OnUnlockPresentationCompleted;
        }

        if (_spawnSliderUI != null)
        {
            _spawnSliderUI.ScholarGuideMenuOpened += OnScholarGuideMenuOpened;
            _spawnSliderUI.SpawnPoolPopupOpened += OnSpawnPoolPopupOpened;
        }

        _gameManager.OnGameplayActivated += TryStart;
        _spawnManager.Initialized += TryStart;

        TryStart();
    }

    private void OnDestroy()
    {
        TutorialManager.Finished -= TryStart;

        if (_unlockPopupUI != null)
        {
            _unlockPopupUI.PresentationCompleted -= OnUnlockPresentationCompleted;
        }

        if (_spawnSliderUI != null)
        {
            _spawnSliderUI.ScholarGuideMenuOpened -= OnScholarGuideMenuOpened;
            _spawnSliderUI.SpawnPoolPopupOpened -= OnSpawnPoolPopupOpened;
        }

        if (_gameManager != null) _gameManager.OnGameplayActivated -= TryStart;
        if (_spawnManager != null) _spawnManager.Initialized -= TryStart;

        if (_systemUpgradePanel != null)
        {
            _systemUpgradePanel.RotationCompleted -= OnUpgradeFocused;
        }

        _clicker?.ReleaseMode(this);
        if (_step != Step.None && _step != Step.Complete)
        {
            _spawnManager?.SetSpawningPaused(false);
            _autoClicker?.SetPaused(false);
        }
    }

    private void OnUnlockPresentationCompleted(ESlimeGrade grade)
    {
        TryStart();
    }

    private void TryStart()
    {
        if (_step != Step.None ||
            !GameplayGate.IsActive ||
            _spawnManager == null ||
            !_spawnManager.IsInitialized ||
            _slimeManager == null ||
            !_slimeManager.IsHigherGradeSpawnUnlocked ||
            !TutorialProgress.CanStart(TutorialIds.HigherGradeSpawn) ||
            _gameplaySpaceManager == null ||
            !_gameplaySpaceManager.IsMainFieldActive ||
            _gameplaySpaceManager.IsTransitioning ||
            (_unlockPopupUI != null && _unlockPopupUI.IsPresenting))
        {
            return;
        }

        Begin();
    }

    private void Begin()
    {
        if (!TryBeginTutorial()) return;

        _step = Step.Dialogue;
        _spawnManager?.SetSpawningPaused(true);
        _autoClicker?.SetPaused(true);
        _clicker?.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);
        Spotlight.Hide();

        ShowDialogue(
            Content.GetDialogue(DialogueId.HigherGradeSpawn),
            ShowPoolButtonStep);
    }

    private void ShowPoolButtonStep()
    {
        RectTransform buttonTarget = _spawnSliderUI?.SpawnPoolButtonTarget;
        if (buttonTarget == null)
        {
            ShowUpgradeStep();
            return;
        }

        _step = Step.PoolButton;
        Spotlight.ShowUiTarget(
            Content.SpawnPoolButtonMessage,
            buttonTarget,
            SpotlightInteractionMode.PassThroughPrimary);
    }

    private void OnSpawnPoolPopupOpened()
    {
        if (_step != Step.ScholarMenu) return;

        RectTransform popupTarget = _spawnSliderUI.SpawnPoolPopupTarget;
        if (popupTarget != null)
        {
            Spotlight.ShowUiFocus(popupTarget);
        }

        _step = Step.Dialogue;
        ShowDialogue(
            Content.GetDialogue(DialogueId.HigherGradeSpawnPool),
            ClosePoolPopupAndShowUpgrade,
            keepGuideVisible: popupTarget != null);
    }

    private void OnScholarGuideMenuOpened()
    {
        if (_step != Step.PoolButton) return;

        RectTransform choiceTarget = _spawnSliderUI.SpawnPoolChoiceTarget;
        if (choiceTarget == null)
        {
            ClosePoolPopupAndShowUpgrade();
            return;
        }

        _step = Step.ScholarMenu;
        Spotlight.ShowUiTarget(
            Content.SpawnPoolButtonMessage,
            choiceTarget,
            SpotlightInteractionMode.PassThroughPrimary);
    }

    private void ClosePoolPopupAndShowUpgrade()
    {
        _spawnSliderUI.CloseSpawnPoolPopup();
        ShowUpgradeStep();
    }

    private void ShowUpgradeStep()
    {
        RectTransform carouselTarget = _systemUpgradePanel?.TutorialTarget;
        if (carouselTarget == null ||
            _panelSwitcher == null ||
            !_panelSwitcher.TryShowSystemUpgradePanel())
        {
            Complete();
            return;
        }

        _step = Step.Carousel;
        Spotlight.ShowUiFocus(carouselTarget);
        _systemUpgradePanel.RotationCompleted -= OnUpgradeFocused;
        _systemUpgradePanel.RotationCompleted += OnUpgradeFocused;

        if (!_systemUpgradePanel.TryFocus(EUpgradeType.HigherGradeSpawnWeightAdd))
        {
            _systemUpgradePanel.RotationCompleted -= OnUpgradeFocused;
            Complete();
            return;
        }

        if (_systemUpgradePanel.IsSelected(EUpgradeType.HigherGradeSpawnWeightAdd))
        {
            OnUpgradeFocused();
        }
    }

    private void OnUpgradeFocused()
    {
        if (_step != Step.Carousel)
        {
            return;
        }

        if (!_systemUpgradePanel.IsSelected(EUpgradeType.HigherGradeSpawnWeightAdd))
        {
            // TryFocus는 한 번에 한 칸만 이동한다. 목표 카드가 여러 칸 떨어져
            // 있으면 다음 회전을 이어 가지 않는 한 중간 카드에서 멈춘다.
            if (_systemUpgradePanel.TryFocus(EUpgradeType.HigherGradeSpawnWeightAdd))
            {
                return;
            }

            _systemUpgradePanel.RotationCompleted -= OnUpgradeFocused;
            Complete();
            return;
        }

        _systemUpgradePanel.RotationCompleted -= OnUpgradeFocused;
        _step = Step.Dialogue;
        ShowDialogue(
            Content.GetDialogue(DialogueId.HigherGradeSpawnUpgrade),
            Complete,
            keepGuideVisible: true,
            placement: DialoguePlacement.Top);
    }

    private void Complete()
    {
        if (_step == Step.Complete) return;

        if (_systemUpgradePanel != null)
        {
            _systemUpgradePanel.RotationCompleted -= OnUpgradeFocused;
        }

        TutorialProgress.MarkCompleted(TutorialIds.HigherGradeSpawn);
        _step = Step.Complete;
        _spawnManager?.SetSpawningPaused(false);
        _autoClicker?.SetPaused(false);
        _clicker?.ReleaseMode(this);
        CompleteTutorial();
        _gameplaySpaceManager?.RefreshInteraction();
    }
}
