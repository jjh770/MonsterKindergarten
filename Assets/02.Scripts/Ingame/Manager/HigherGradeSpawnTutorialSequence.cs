using UnityEngine;

public sealed class HigherGradeSpawnTutorialSequence : TutorialSequenceBase
{
    private enum Step
    {
        None,
        Dialogue,
        PoolButton,
        PoolPopup,
        Carousel,
        Complete,
    }

    [SerializeField] private SpawnSliderUI _spawnSliderUI;
    [SerializeField] private SystemUpgradePanel _systemUpgradePanel;
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
            _spawnSliderUI.SpawnPoolPopupClosed += OnSpawnPoolPopupClosed;
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
            _spawnSliderUI.SpawnPoolPopupClosed -= OnSpawnPoolPopupClosed;
        }

        if (_systemUpgradePanel != null)
        {
            _systemUpgradePanel.RotationCompleted -= OnUpgradeFocused;
        }

        // 파괴 경로에서는 CompleteTutorial을 거치지 않아 RefreshInteraction이
        // 튜토리얼 소유권 게이트에 막힌다. 최소 복구만 직접 수행한다.
        if (_step != Step.None && _step != Step.Complete)
        {
            _clicker?.SetInputMode(true, true);
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
        _clicker?.SetInputMode(false, false);
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

        Spotlight.Hide();
        _step = Step.Dialogue;
        ShowDialogue(
            Content.GetDialogue(DialogueId.HigherGradeSpawnPool),
            WaitForPoolPopupClose);
    }

    private void WaitForPoolPopupClose()
    {
        _step = Step.PoolPopup;
    }

    private void OnSpawnPoolPopupClosed()
    {
        if (_step == Step.PoolPopup)
        {
            ShowUpgradeStep();
        }
    }

    private void ShowUpgradeStep()
    {
        RectTransform carouselTarget = _systemUpgradePanel?.TutorialTarget;
        if (carouselTarget == null)
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
        // 소유권을 먼저 반납해야 RefreshInteraction이 입력을 되돌린다.
        CompleteTutorial();
        StageManager.Instance?.RefreshInteraction();
    }
}
