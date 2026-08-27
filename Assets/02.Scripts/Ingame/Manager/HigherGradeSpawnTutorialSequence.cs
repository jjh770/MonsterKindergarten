using UnityEngine;

public sealed class HigherGradeSpawnTutorialSequence : TutorialSequenceBase
{
    private enum Step
    {
        None,
        Dialogue,
        PoolButton,
        Carousel,
        Complete,
    }

    [SerializeField] private SpawnSliderUI _spawnSliderUI;
    [SerializeField] private SystemUpgradePanel _systemUpgradePanel;
    [SerializeField] private BottomPanelSwitcher _panelSwitcher;
    [SerializeField] private UnlockPopupUI _unlockPopupUI;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private AutoClicker _autoClicker;

    private Step _step;

    private void Start()
    {
        if (_unlockPopupUI != null)
        {
            _unlockPopupUI.PresentationCompleted += OnUnlockPresentationCompleted;
        }

        if (_spawnSliderUI != null)
        {
            _spawnSliderUI.SpawnPoolPopupOpened += OnSpawnPoolPopupOpened;
        }
    }

    private void OnDestroy()
    {
        if (_unlockPopupUI != null)
        {
            _unlockPopupUI.PresentationCompleted -= OnUnlockPresentationCompleted;
        }

        if (_spawnSliderUI != null)
        {
            _spawnSliderUI.SpawnPoolPopupOpened -= OnSpawnPoolPopupOpened;
        }

        if (_systemUpgradePanel != null)
        {
            _systemUpgradePanel.RotationCompleted -= OnUpgradeFocused;
        }

        _clicker?.ReleaseMode(this);
        if (_step != Step.None && _step != Step.Complete)
        {
            SpawnManager.Instance?.SetSpawningPaused(false);
            _autoClicker?.SetPaused(false);
        }
    }

    private void OnUnlockPresentationCompleted(ESlimeGrade grade)
    {
        if (_step != Step.None ||
            SlimeManager.Instance == null ||
            !SlimeManager.Instance.IsHigherGradeSpawnUnlocked ||
            !TutorialProgress.ShouldRun(TutorialIds.HigherGradeSpawn))
        {
            return;
        }

        Begin();
    }

    private void Begin()
    {
        if (!TryBeginTutorial()) return;

        TutorialProgress.MarkCompleted(TutorialIds.HigherGradeSpawn);
        _step = Step.Dialogue;
        SpawnManager.Instance?.SetSpawningPaused(true);
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
            SpotlightInteractionMode.PassThroughPrimary,
            useRectangularHole: true);
    }

    private void OnSpawnPoolPopupOpened()
    {
        if (_step != Step.PoolButton) return;

        RectTransform popupTarget = _spawnSliderUI.SpawnPoolPopupTarget;
        if (popupTarget != null)
        {
            Spotlight.ShowUiFocus(popupTarget, useRectangularHole: true);
        }

        _step = Step.Dialogue;
        ShowDialogue(
            Content.GetDialogue(DialogueId.HigherGradeSpawnPool),
            ClosePoolPopupAndShowUpgrade,
            keepGuideVisible: popupTarget != null);
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
        Spotlight.ShowUiFocus(carouselTarget, useRectangularHole: true);
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
        if (_step != Step.Carousel ||
            !_systemUpgradePanel.IsSelected(EUpgradeType.HigherGradeSpawnWeightAdd))
        {
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

        _step = Step.Complete;
        SpawnManager.Instance?.SetSpawningPaused(false);
        _autoClicker?.SetPaused(false);
        _clicker?.ReleaseMode(this);
        CompleteTutorial();
        StageManager.Instance?.RefreshInteraction();
    }
}
