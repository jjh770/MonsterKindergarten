using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class MainTutorialSequence : TutorialSequenceBase
{
    private enum Step
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
        Complete,
    }

    [Header("Guide UI")]
    [SerializeField] private RectTransform _pointTarget;
    [SerializeField] private RectTransform _spawnGaugeTarget;
    [SerializeField] private SystemUpgradePanel _systemUpgradePanel;
    [SerializeField] private UnlockPopupUI _unlockPopupUI;
    [SerializeField] private UpgradeUI _upgradeUI;
    [SerializeField, Min(0f)] private float _mergeSlimeDistance = 1.5f;

    [Header("Input")]
    [SerializeField] private Clicker _clicker;
    [SerializeField] private AutoClicker _autoClicker;

    private SlimeController _tutorialSlime;
    private SlimeController _mergeTutorialSlime;
    private SlimeController _promotedTutorialSlime;
    private Step _step;
    private bool _isGuideSubscribed;

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
            _clicker.ReleaseMode(this);
        }

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnTutorialSlimeReady -= Begin;
            SpawnManager.Instance.SetSpawningPaused(false);
        }

        if (_unlockPopupUI != null)
        {
            _unlockPopupUI.PresentationCompleted -= OnUnlockPresentationCompleted;
        }

        if (_upgradeUI != null)
        {
            _upgradeUI.Opened -= OnUpgradeOpened;
            _upgradeUI.Closed -= OnUpgradeClosed;
            _upgradeUI.SetToggleInputEnabled(true);
        }

        if (_systemUpgradePanel != null)
        {
            _systemUpgradePanel.RotationCompleted -= OnSystemUpgradeRotationCompleted;
        }

        UnsubscribeGuide();
        UnsubscribeMergeEvents();
        _autoClicker?.SetPaused(false);
    }

    private void Begin(SlimeController tutorialSlime)
    {
        if (tutorialSlime == null || _step != Step.None) return;
        if (!TutorialProgress.ShouldRun(TutorialIds.Main))
        {
            tutorialSlime.SetMovementLocked(false);
            return;
        }

        if (!TryBeginTutorial())
        {
            Debug.LogError("메인 튜토리얼 프레젠테이션을 시작할 수 없습니다.", this);
            Complete(tutorialSlime);
            return;
        }

        _tutorialSlime = tutorialSlime;
        Spotlight.AdvanceRequested += OnGuideAdvanceRequested;
        _isGuideSubscribed = true;

        if (_upgradeUI != null)
        {
            _upgradeUI.Opened += OnUpgradeOpened;
            _upgradeUI.Closed += OnUpgradeClosed;
        }

        SpawnManager.Instance.SetSpawningPaused(true);
        _autoClicker?.SetPaused(true);
        _upgradeUI?.SetToggleInputEnabled(false);
        ShowStepDialogue(
            Content.GetDialogue(DialogueId.Introduction),
            ShowClickStep);
    }

    private void ShowStepDialogue(
        System.Collections.Generic.IReadOnlyList<DialogueLine> lines,
        System.Action onComplete,
        bool keepGuideVisible = false,
        DialoguePlacement placement = DialoguePlacement.Bottom)
    {
        _step = Step.Dialogue;
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);
        ShowDialogue(lines, onComplete, keepGuideVisible, placement);
    }

    private void ShowClickStep()
    {
        _step = Step.Click;
        _clicker.PushMode(this, ClickerInputMode.ClickOnly(_tutorialSlime), ClickerInputPriority.Tutorial);
        Spotlight.Show(Content.ClickMessage, _tutorialSlime.transform);
    }

    private void OnTargetClicked(SlimeController target)
    {
        if (_step != Step.Click || target != _tutorialSlime) return;

        ShowStepDialogue(
            Content.GetDialogue(DialogueId.Point),
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

        _step = Step.PointHighlight;
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);
        Spotlight.ShowUiTarget(
            Content.PointMessage,
            _pointTarget,
            SpotlightInteractionMode.AdvanceOnPrimaryTap);
    }

    private void OnGuideAdvanceRequested()
    {
        if (_step != Step.PointHighlight) return;

        ShowStepDialogue(
            Content.GetDialogue(DialogueId.Movement),
            ShowDragStep);
    }

    private void ShowDragStep()
    {
        _step = Step.Drag;
        _clicker.PushMode(this, ClickerInputMode.DragOnly(_tutorialSlime), ClickerInputPriority.Tutorial);
        Spotlight.Show(Content.DragMessage, _tutorialSlime.transform);
    }

    private void OnTargetDragCompleted(SlimeController target)
    {
        if (_step != Step.Drag || target != _tutorialSlime) return;

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

        _step = Step.Merge;
        _clicker.PushMode(this, ClickerInputMode.DragOnly(
            _tutorialSlime,
            _mergeTutorialSlime), ClickerInputPriority.Tutorial);
        Spotlight.ShowWorldTargets(
            Content.MergeMessage,
            _tutorialSlime.transform,
            _mergeTutorialSlime.transform);
    }

    private void OnPrimaryTutorialSlimePromoted()
    {
        if (_step == Step.Merge)
        {
            WaitForUnlockPresentation(_tutorialSlime);
        }
    }

    private void OnSecondaryTutorialSlimePromoted()
    {
        if (_step == Step.Merge)
        {
            WaitForUnlockPresentation(_mergeTutorialSlime);
        }
    }

    private void WaitForUnlockPresentation(SlimeController survivingSlime)
    {
        UnsubscribeMergeEvents();
        _promotedTutorialSlime = survivingSlime;
        _mergeTutorialSlime = null;
        _step = Step.WaitingForUnlock;
        Spotlight.Hide();
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);

        if (_unlockPopupUI == null || !_unlockPopupUI.IsPresenting)
        {
            ShowMergeResultStep();
        }
    }

    private void OnUnlockPresentationCompleted(ESlimeGrade grade)
    {
        if (_step != Step.WaitingForUnlock ||
            _promotedTutorialSlime == null ||
            grade != _promotedTutorialSlime.Grade)
        {
            return;
        }

        ShowMergeResultStep();
    }

    private void ShowMergeResultStep()
    {
        if (_promotedTutorialSlime == null)
        {
            Complete(null);
            return;
        }

        Spotlight.ShowFocus(_promotedTutorialSlime.transform);
        ShowStepDialogue(
            Content.GetDialogue(DialogueId.MergeResult),
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

        _step = Step.UpgradeButton;
        _upgradeUI.SetToggleInputEnabled(true);
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);
        Spotlight.ShowUiTarget(
            Content.UpgradeMessage,
            upgradeTarget,
            SpotlightInteractionMode.PassThroughPrimary);
    }

    private void OnUpgradeOpened()
    {
        if (_step != Step.UpgradeButton) return;

        RectTransform panelTarget = _upgradeUI.PanelTarget;
        RectTransform closeTarget = _upgradeUI.ToggleTarget;
        if (panelTarget == null || closeTarget == null)
        {
            Debug.LogWarning("강조할 업그레이드 창 또는 닫기 버튼이 없어 마무리 대화로 이동합니다.");
            ShowFinalDialogue();
            return;
        }

        _step = Step.UpgradePanel;
        Spotlight.ShowUiTargets(
            Content.UpgradePanelMessage,
            closeTarget,
            panelTarget,
            interactionMode: SpotlightInteractionMode.PassThroughPrimary,
            useRectangularSecondaryHole: true,
            useCompactMessage: true);
    }

    private void OnUpgradeClosed()
    {
        if (_step != Step.UpgradePanel) return;

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

        Spotlight.ShowUiFocus(carouselTarget, useRectangularHole: true);
        ShowStepDialogue(
            Content.GetDialogue(DialogueId.SpawnUpgrade),
            ShowSystemUpgradeCarouselStep,
            keepGuideVisible: true,
            placement: DialoguePlacement.Top);
    }

    private void ShowSystemUpgradeCarouselStep()
    {
        RectTransform carouselTarget = _systemUpgradePanel?.TutorialTarget;
        if (carouselTarget == null)
        {
            ShowSpawnGaugeStep();
            return;
        }

        _step = Step.SystemUpgradeCarousel;
        _systemUpgradePanel.RotationCompleted -= OnSystemUpgradeRotationCompleted;
        _systemUpgradePanel.RotationCompleted += OnSystemUpgradeRotationCompleted;
        Spotlight.ShowUiTarget(
            Content.SystemUpgradeCarouselMessage,
            carouselTarget,
            SpotlightInteractionMode.PassThroughPrimary,
            useRectangularHole: true);
    }

    private void OnSystemUpgradeRotationCompleted()
    {
        if (_step != Step.SystemUpgradeCarousel) return;

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

        Spotlight.ShowUiFocus(_spawnGaugeTarget, useRectangularHole: true);
        ShowStepDialogue(
            Content.GetDialogue(DialogueId.SpawnGauge),
            ShowFinalDialogue,
            keepGuideVisible: true);
    }

    private void ShowFinalDialogue()
    {
        ShowStepDialogue(
            Content.GetDialogue(DialogueId.Final),
            () => Complete(_promotedTutorialSlime));
    }

    private void Complete(SlimeController survivingSlime)
    {
        CompleteAsync(survivingSlime).Forget();
    }

    private async UniTask CompleteAsync(SlimeController survivingSlime)
    {
        if (_step == Step.Complete) return;

        UnsubscribeMergeEvents();
        UnsubscribeGuide();
        _step = Step.Complete;

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
        SpawnManager.Instance?.SetSpawningPaused(false);
        _autoClicker?.SetPaused(false);
        _clicker.ReleaseMode(this);
        CompleteTutorial();
        StageManager.Instance?.RefreshInteraction();
    }

    private void UnsubscribeGuide()
    {
        if (!_isGuideSubscribed || Spotlight == null) return;

        Spotlight.AdvanceRequested -= OnGuideAdvanceRequested;
        _isGuideSubscribed = false;
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
