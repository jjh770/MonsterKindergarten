using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class TutorialManager : MonoBehaviour
{
    private enum TutorialStep
    {
        None,
        Dialogue,
        Click,
        PointHighlight,
        Drag,
        Merge,
        WaitingForUnlock,
        UpgradeButton,
        UpgradePanel,
        SystemUpgradeCarousel,
        HigherGradeSpawn,
        HigherGradeSpawnPoolButton,
        HigherGradeSpawnPoolPopup,
        HigherGradeSpawnCarousel,
        Complete,
    }

    [Header("Guide UI")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private TutorialDialogueView _dialoguePrefab;
    [SerializeField] private TutorialSpotlightView _guidePrefab;
    [SerializeField] private TutorialContent _content;
    [SerializeField] private RectTransform _pointTarget;
    [SerializeField] private RectTransform _spawnGaugeTarget;
    [SerializeField] private SpawnSliderUI _spawnSliderUI;
    [SerializeField] private SystemUpgradePanel _systemUpgradePanel;
    [SerializeField] private UnlockPopupUI _unlockPopupUI;
    [SerializeField] private UpgradeUI _upgradeUI;
    [SerializeField, Min(0f)] private float _mergeSlimeDistance = 1.5f;

    [Header("Input")]
    [SerializeField] private Clicker _clicker;
    [SerializeField] private AutoClicker _autoClicker;

    private TutorialPresentation _presentation;
    private SlimeController _tutorialSlime;
    private SlimeController _mergeTutorialSlime;
    private SlimeController _promotedTutorialSlime;
    private TutorialStep _step;

    private TutorialSpotlightView Spotlight => _presentation?.Spotlight;

    private void Start()
    {
        _clicker.TargetClicked += OnTargetClicked;
        _clicker.TargetDragCompleted += OnTargetDragCompleted;

        if (SpawnManager.Instance == null) return;

        SpawnManager.Instance.OnTutorialSlimeReady += Begin;

        if (_unlockPopupUI != null)
        {
            _unlockPopupUI.PresentationCompleted += OnUnlockPresentationCompleted;
        }

        if (_spawnSliderUI != null)
        {
            _spawnSliderUI.SpawnPoolPopupOpened += OnSpawnPoolPopupOpened;
            _spawnSliderUI.SpawnPoolPopupClosed += OnSpawnPoolPopupClosed;
        }

        if (SpawnManager.Instance.TutorialSlime != null)
        {
            Begin(SpawnManager.Instance.TutorialSlime);
        }
    }

    private void OnDestroy()
    {
        if (_clicker != null)
        {
            _clicker.TargetClicked -= OnTargetClicked;
            _clicker.TargetDragCompleted -= OnTargetDragCompleted;
            _clicker.SetInputMode(true, true);
        }

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnTutorialSlimeReady -= Begin;
            SpawnManager.Instance.SetSpawningPaused(false);
        }

        _autoClicker?.SetPaused(false);
        _upgradeUI?.SetToggleInputEnabled(true);
        if (_unlockPopupUI != null)
        {
            _unlockPopupUI.PresentationCompleted -= OnUnlockPresentationCompleted;
        }

        if (_upgradeUI != null)
        {
            _upgradeUI.Opened -= OnUpgradeOpened;
            _upgradeUI.Closed -= OnUpgradeClosed;
        }

        if (_systemUpgradePanel != null)
        {
            _systemUpgradePanel.RotationCompleted -= OnSystemUpgradeRotationCompleted;
            _systemUpgradePanel.RotationCompleted -= OnHigherGradeSpawnUpgradeFocused;
        }

        if (_spawnSliderUI != null)
        {
            _spawnSliderUI.SpawnPoolPopupOpened -= OnSpawnPoolPopupOpened;
            _spawnSliderUI.SpawnPoolPopupClosed -= OnSpawnPoolPopupClosed;
        }

        UnsubscribeMergeEvents();

        if (_presentation != null)
        {
            _presentation.Spotlight.AdvanceRequested -= OnGuideAdvanceRequested;
            _presentation.Dispose();
            _presentation = null;
        }
    }

    private void Begin(SlimeController tutorialSlime)
    {
        if (tutorialSlime == null || _step != TutorialStep.None) return;
        if (!TutorialProgress.ShouldRun(TutorialIds.Main))
        {
            tutorialSlime.SetMovementLocked(false);
            return;
        }

        if (_canvas == null || _dialoguePrefab == null ||
            _guidePrefab == null || _content == null)
        {
            Debug.LogError("튜토리얼 Canvas, 프리팹 또는 콘텐츠 에셋이 없습니다.");
            Complete(tutorialSlime);
            return;
        }

        _tutorialSlime = tutorialSlime;
        Canvas sortingReference = _upgradeUI != null
            ? _upgradeUI.GetComponentInParent<Canvas>()
            : _canvas;
        if (!EnsurePresentation(sortingReference))
        {
            Complete(tutorialSlime);
            return;
        }

        if (_upgradeUI != null)
        {
            _upgradeUI.Opened += OnUpgradeOpened;
            _upgradeUI.Closed += OnUpgradeClosed;
        }

        SpawnManager.Instance.SetSpawningPaused(true);
        _autoClicker?.SetPaused(true);
        _upgradeUI?.SetToggleInputEnabled(false);
        ShowDialogue(
            _content.GetDialogue(DialogueId.Introduction),
            ShowClickStep);
    }

    private void ShowDialogue(
        IReadOnlyList<DialogueLine> lines,
        Action onComplete,
        bool keepGuideVisible = false,
        TutorialDialoguePlacement placement = TutorialDialoguePlacement.Bottom)
    {
        _step = TutorialStep.Dialogue;
        _clicker.SetInputMode(false, false);
        _presentation.ShowDialogue(
            lines,
            onComplete,
            keepGuideVisible,
            placement);
    }

    private void ShowClickStep()
    {
        _step = TutorialStep.Click;
        _clicker.SetInputMode(true, false, _tutorialSlime);
        Spotlight.Show(_content.ClickMessage, _tutorialSlime.transform);
    }

    private void OnTargetClicked(SlimeController target)
    {
        if (_step != TutorialStep.Click || target != _tutorialSlime) return;

        ShowDialogue(
            _content.GetDialogue(DialogueId.Point),
            ShowPointHighlightStep);
    }

    private void ShowPointHighlightStep()
    {
        if (_pointTarget == null)
        {
            Debug.LogWarning("강조할 포인트 UI가 없어 드래그 단계로 이동합니다.");
            ShowDragStep();
            return;
        }

        _step = TutorialStep.PointHighlight;
        _clicker.SetInputMode(false, false);
        Spotlight.ShowUiTarget(
            _content.PointMessage,
            _pointTarget,
            SpotlightInteractionMode.AdvanceOnPrimaryTap);
    }

    private void OnGuideAdvanceRequested()
    {
        if (_step != TutorialStep.PointHighlight) return;

        ShowDialogue(
            _content.GetDialogue(DialogueId.Movement),
            ShowDragStep);
    }

    private void ShowDragStep()
    {
        _step = TutorialStep.Drag;
        _clicker.SetInputMode(false, true, _tutorialSlime);
        Spotlight.Show(_content.DragMessage, _tutorialSlime.transform);
    }

    private void OnTargetDragCompleted(SlimeController target)
    {
        if (_step != TutorialStep.Drag || target != _tutorialSlime) return;

        ShowMergeStep();
    }

    private void ShowMergeStep()
    {
        _mergeTutorialSlime = SpawnManager.Instance.SpawnTutorialSlimeNear(
            _tutorialSlime,
            _mergeSlimeDistance);

        if (_mergeTutorialSlime == null)
        {
            Debug.LogWarning("합성 튜토리얼 슬라임을 생성하지 못해 튜토리얼을 종료합니다.");
            Complete(_tutorialSlime);
            return;
        }

        _tutorialSlime.OnPromoted += OnPrimaryTutorialSlimePromoted;
        _mergeTutorialSlime.OnPromoted += OnSecondaryTutorialSlimePromoted;

        _step = TutorialStep.Merge;
        _clicker.SetInputMode(
            false,
            true,
            _tutorialSlime,
            _mergeTutorialSlime);
        Spotlight.ShowWorldTargets(
            _content.MergeMessage,
            _tutorialSlime.transform,
            _mergeTutorialSlime.transform);
    }

    private void OnPrimaryTutorialSlimePromoted()
    {
        if (_step == TutorialStep.Merge)
        {
            WaitForUnlockPresentation(_tutorialSlime);
        }
    }

    private void OnSecondaryTutorialSlimePromoted()
    {
        if (_step == TutorialStep.Merge)
        {
            WaitForUnlockPresentation(_mergeTutorialSlime);
        }
    }

    private void WaitForUnlockPresentation(SlimeController survivingSlime)
    {
        UnsubscribeMergeEvents();
        _promotedTutorialSlime = survivingSlime;
        _mergeTutorialSlime = null;
        _step = TutorialStep.WaitingForUnlock;
        Spotlight.Hide();
        _clicker.SetInputMode(false, false);

        bool willUnlockNewGrade = SlimeManager.Instance != null &&
                                  survivingSlime != null &&
                                  survivingSlime.Grade > SlimeManager.Instance.HighestGrade;
        if (!willUnlockNewGrade || _unlockPopupUI == null)
        {
            ShowMergeResultStep();
        }
    }

    private void OnUnlockPresentationCompleted(ESlimeGrade grade)
    {
        if (SlimeManager.Instance != null &&
            SlimeManager.Instance.IsHigherGradeSpawnUnlocked &&
            TutorialProgress.ShouldRun(TutorialIds.HigherGradeSpawn) &&
            (_step == TutorialStep.None || _step == TutorialStep.Complete))
        {
            ShowHigherGradeSpawnTutorial();
            return;
        }

        if (_step != TutorialStep.WaitingForUnlock ||
            _promotedTutorialSlime == null ||
            grade != _promotedTutorialSlime.Grade)
        {
            return;
        }

        ShowMergeResultStep();
    }

    private void ShowHigherGradeSpawnTutorial()
    {
        TutorialProgress.MarkCompleted(TutorialIds.HigherGradeSpawn);

        Canvas sortingReference = _upgradeUI != null
            ? _upgradeUI.GetComponentInParent<Canvas>()
            : _canvas;
        if (!EnsurePresentation(sortingReference)) return;

        _step = TutorialStep.HigherGradeSpawn;
        SpawnManager.Instance?.SetSpawningPaused(true);
        _autoClicker?.SetPaused(true);
        _clicker?.SetInputMode(false, false);

        if (_spawnGaugeTarget != null)
        {
            Spotlight.ShowUiFocus(
                _spawnGaugeTarget,
                useRectangularHole: true);
        }

        ShowDialogue(
            _content.GetDialogue(DialogueId.HigherGradeSpawn),
            ShowSpawnPoolButtonStep,
            keepGuideVisible: _spawnGaugeTarget != null);
    }

    private void ShowSpawnPoolButtonStep()
    {
        RectTransform buttonTarget = _spawnSliderUI?.SpawnPoolButtonTarget;
        if (buttonTarget == null)
        {
            ShowHigherGradeSpawnUpgradeStep();
            return;
        }

        _step = TutorialStep.HigherGradeSpawnPoolButton;
        Spotlight.ShowUiTarget(
            _content.SpawnPoolButtonMessage,
            buttonTarget,
            SpotlightInteractionMode.PassThroughPrimary,
            useRectangularHole: true);
    }

    private void OnSpawnPoolPopupOpened()
    {
        if (_step != TutorialStep.HigherGradeSpawnPoolButton) return;

        _step = TutorialStep.HigherGradeSpawnPoolPopup;
        Spotlight.Hide();
    }

    private void OnSpawnPoolPopupClosed()
    {
        if (_step != TutorialStep.HigherGradeSpawnPoolPopup) return;

        ShowHigherGradeSpawnUpgradeStep();
    }

    private void ShowHigherGradeSpawnUpgradeStep()
    {
        RectTransform carouselTarget = _systemUpgradePanel?.TutorialTarget;
        if (carouselTarget == null)
        {
            CompleteHigherGradeSpawnTutorial();
            return;
        }

        _step = TutorialStep.HigherGradeSpawnCarousel;
        Spotlight.ShowUiFocus(
            carouselTarget,
            useRectangularHole: true);

        _systemUpgradePanel.RotationCompleted -= OnHigherGradeSpawnUpgradeFocused;
        _systemUpgradePanel.RotationCompleted += OnHigherGradeSpawnUpgradeFocused;

        if (!_systemUpgradePanel.TryFocus(
                EUpgradeType.HigherGradeSpawnWeightAdd))
        {
            _systemUpgradePanel.RotationCompleted -= OnHigherGradeSpawnUpgradeFocused;
            CompleteHigherGradeSpawnTutorial();
            return;
        }

        if (_systemUpgradePanel.IsSelected(
                EUpgradeType.HigherGradeSpawnWeightAdd))
        {
            OnHigherGradeSpawnUpgradeFocused();
        }
    }

    private void OnHigherGradeSpawnUpgradeFocused()
    {
        if (_step != TutorialStep.HigherGradeSpawnCarousel ||
            !_systemUpgradePanel.IsSelected(
                EUpgradeType.HigherGradeSpawnWeightAdd))
        {
            return;
        }

        _systemUpgradePanel.RotationCompleted -= OnHigherGradeSpawnUpgradeFocused;
        ShowDialogue(
            _content.GetDialogue(DialogueId.HigherGradeSpawnUpgrade),
            CompleteHigherGradeSpawnTutorial,
            keepGuideVisible: true,
            placement: TutorialDialoguePlacement.Top);
    }

    private void CompleteHigherGradeSpawnTutorial()
    {
        if (_systemUpgradePanel != null)
        {
            _systemUpgradePanel.RotationCompleted -= OnHigherGradeSpawnUpgradeFocused;
        }

        Spotlight?.Hide();
        _step = TutorialStep.Complete;
        _clicker?.SetInputMode(true, true);
        SpawnManager.Instance?.SetSpawningPaused(false);
        _autoClicker?.SetPaused(false);
    }

    private bool EnsurePresentation(Canvas sortingReference)
    {
        if (_presentation != null) return true;

        if (_canvas == null || _dialoguePrefab == null ||
            _guidePrefab == null || _content == null)
        {
            Debug.LogError("튜토리얼 Canvas, 프리팹 또는 콘텐츠 에셋이 없습니다.");
            return false;
        }

        _presentation = new TutorialPresentation(
            _canvas,
            sortingReference,
            _dialoguePrefab,
            _guidePrefab);
        _presentation.Spotlight.AdvanceRequested += OnGuideAdvanceRequested;
        return true;
    }

    private void ShowMergeResultStep()
    {
        if (_promotedTutorialSlime == null)
        {
            Complete(null);
            return;
        }

        Spotlight.ShowFocus(_promotedTutorialSlime.transform);
        ShowDialogue(
            _content.GetDialogue(DialogueId.MergeResult),
            ShowUpgradeStep,
            keepGuideVisible: true);
    }

    private void ShowUpgradeStep()
    {
        RectTransform upgradeTarget = _upgradeUI?.ToggleTarget;
        if (upgradeTarget == null)
        {
            Debug.LogWarning("강조할 업그레이드 버튼이 없어 튜토리얼을 종료합니다.");
            Complete(_promotedTutorialSlime);
            return;
        }

        _step = TutorialStep.UpgradeButton;
        _upgradeUI.SetToggleInputEnabled(true);
        _clicker.SetInputMode(false, false);
        Spotlight.ShowUiTarget(
            _content.UpgradeMessage,
            upgradeTarget,
            SpotlightInteractionMode.PassThroughPrimary);
    }

    private void OnUpgradeOpened()
    {
        if (_step != TutorialStep.UpgradeButton) return;

        RectTransform panelTarget = _upgradeUI.PanelTarget;
        RectTransform closeTarget = _upgradeUI.ToggleTarget;
        if (panelTarget == null || closeTarget == null)
        {
            Debug.LogWarning("강조할 업그레이드 창 또는 닫기 버튼이 없어 마무리 대화로 이동합니다.");
            ShowFinalDialogue();
            return;
        }

        _step = TutorialStep.UpgradePanel;
        Spotlight.ShowUiTargets(
            _content.UpgradePanelMessage,
            closeTarget,
            panelTarget,
            interactionMode: SpotlightInteractionMode.PassThroughPrimary,
            useRectangularSecondaryHole: true,
            useCompactMessage: true);
    }

    private void OnUpgradeClosed()
    {
        if (_step != TutorialStep.UpgradePanel) return;

        _upgradeUI.SetToggleInputEnabled(false);
        ShowSpawnUpgradeStep();
    }

    private void ShowSpawnUpgradeStep()
    {
        RectTransform carouselTarget = _systemUpgradePanel?.TutorialTarget;
        if (carouselTarget == null)
        {
            Debug.LogWarning("강조할 시스템 업그레이드 캐러셀이 없어 게이지 설명으로 이동합니다.");
            ShowSpawnGaugeStep();
            return;
        }

        Spotlight.ShowUiFocus(
            carouselTarget,
            useRectangularHole: true);
        ShowDialogue(
            _content.GetDialogue(DialogueId.SpawnUpgrade),
            ShowSystemUpgradeCarouselStep,
            keepGuideVisible: true,
            placement: TutorialDialoguePlacement.Top);
    }

    private void ShowSystemUpgradeCarouselStep()
    {
        RectTransform carouselTarget = _systemUpgradePanel?.TutorialTarget;
        if (carouselTarget == null)
        {
            ShowSpawnGaugeStep();
            return;
        }

        _step = TutorialStep.SystemUpgradeCarousel;
        _systemUpgradePanel.RotationCompleted -= OnSystemUpgradeRotationCompleted;
        _systemUpgradePanel.RotationCompleted += OnSystemUpgradeRotationCompleted;
        Spotlight.ShowUiTarget(
            _content.SystemUpgradeCarouselMessage,
            carouselTarget,
            SpotlightInteractionMode.PassThroughPrimary,
            useRectangularHole: true);
    }

    private void OnSystemUpgradeRotationCompleted()
    {
        if (_step != TutorialStep.SystemUpgradeCarousel) return;

        _systemUpgradePanel.RotationCompleted -= OnSystemUpgradeRotationCompleted;
        ShowSpawnGaugeStep();
    }

    private void ShowSpawnGaugeStep()
    {
        if (_spawnGaugeTarget == null)
        {
            Debug.LogWarning("강조할 슬라임 스폰 게이지가 없어 마무리 대화로 이동합니다.");
            ShowFinalDialogue();
            return;
        }

        Spotlight.ShowUiFocus(
            _spawnGaugeTarget,
            useRectangularHole: true);
        ShowDialogue(
            _content.GetDialogue(DialogueId.SpawnGauge),
            ShowFinalDialogue,
            keepGuideVisible: true);
    }

    private void ShowFinalDialogue()
    {
        ShowDialogue(
            _content.GetDialogue(DialogueId.Final),
            () => Complete(_promotedTutorialSlime));
    }

    private void Complete(SlimeController survivingSlime)
    {
        CompleteAsync(survivingSlime).Forget();
    }

    private async UniTask CompleteAsync(SlimeController survivingSlime)
    {
        if (_step == TutorialStep.Complete) return;

        UnsubscribeMergeEvents();
        _step = TutorialStep.Complete;
        Spotlight?.Hide();

        if (GameManager.Instance != null)
        {
            await GameManager.Instance.CompleteTutorialAsync();
        }
        else
        {
            Debug.LogError("GameManager가 없어 튜토리얼 완료 상태를 저장하지 못했습니다.");
            GameplaySaveGate.SetSavingEnabled(true);
        }

        survivingSlime?.SetMovementLocked(false);
        _upgradeUI?.SetToggleInputEnabled(true);
        _promotedTutorialSlime = null;
        _mergeTutorialSlime = null;
        _clicker.SetInputMode(true, true);
        SpawnManager.Instance.SetSpawningPaused(false);
        _autoClicker?.SetPaused(false);
    }

    private void UnsubscribeMergeEvents()
    {
        if (_tutorialSlime != null)
        {
            _tutorialSlime.OnPromoted -= OnPrimaryTutorialSlimePromoted;
        }

        if (_mergeTutorialSlime != null)
        {
            _mergeTutorialSlime.OnPromoted -= OnSecondaryTutorialSlimePromoted;
        }
    }
}
