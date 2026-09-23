using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class MainTutorialSequence : TutorialSequenceBase
{
    public override string TutorialId => TutorialIds.Main;

    private enum Step
    {
        None,
        Dialogue,
        Click,
        PointHighlight,
        Drag,
        Merge,
        WaitingForUnlock,
        FocusingSlimeUpgrade,
        SlimeUpgrade,
        Complete,
    }

    [Header("Guide UI")]
    [SerializeField] private SpawnSliderUI _spawnSliderUI;
    [SerializeField] private RectTransform _pointTarget;
    [SerializeField] private RectTransform _spawnGaugeTarget;
    [SerializeField] private SystemUpgradePanel _systemUpgradePanel;
    [SerializeField] private UnlockPopupUI _unlockPopupUI;
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

        if (_systemUpgradePanel != null)
        {
            _systemUpgradePanel.RotationCompleted -= OnSlimeUpgradeFocusAdvanced;
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

        SpawnManager.Instance.SetSpawningPaused(true);
        _autoClicker?.SetPaused(true);
        RectTransform scholarSlimeTarget = _spawnSliderUI?.SpawnPoolButtonTarget;
        if (scholarSlimeTarget != null)
        {
            Spotlight.ShowUiFocus(scholarSlimeTarget);
        }

        ShowStepDialogue(
            Content.GetDialogue(DialogueId.Introduction),
            ShowClickStep,
            keepGuideVisible: scholarSlimeTarget != null);
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
        switch (_step)
        {
            case Step.PointHighlight:
                ShowStepDialogue(
                    Content.GetDialogue(DialogueId.Movement),
                    ShowDragStep);
                break;
            case Step.SlimeUpgrade:
                ShowSpawnGaugeStep();
                break;
        }
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
            ShowSlimeUpgradeIntro,
            keepGuideVisible: true);
    }

    private void ShowSlimeUpgradeIntro()
    {
        RectTransform carouselTarget = _systemUpgradePanel?.TutorialTarget;
        if (carouselTarget == null)
        {
            Debug.LogWarning("강조할 시스템 업그레이드 캐러셀이 없어 게이지 설명으로 이동합니다.");
            ShowSpawnGaugeStep();
            return;
        }

        Spotlight.ShowUiFocus(carouselTarget);
        ShowStepDialogue(
            Content.GetDialogue(DialogueId.SpawnUpgrade),
            FocusSlimeUpgrade,
            keepGuideVisible: true,
            placement: DialoguePlacement.Top);
    }

    private void FocusSlimeUpgrade()
    {
        RectTransform carouselTarget = _systemUpgradePanel?.TutorialTarget;
        if (carouselTarget == null)
        {
            ShowSpawnGaugeStep();
            return;
        }

        _step = Step.FocusingSlimeUpgrade;
        _systemUpgradePanel.RotationCompleted -= OnSlimeUpgradeFocusAdvanced;
        _systemUpgradePanel.RotationCompleted += OnSlimeUpgradeFocusAdvanced;

        if (_systemUpgradePanel.IsSelected(EUpgradeType.AllSlimePointPercentAdd))
        {
            ShowSlimeUpgradeGuide();
            return;
        }

        if (!_systemUpgradePanel.TryFocus(EUpgradeType.AllSlimePointPercentAdd))
        {
            _systemUpgradePanel.RotationCompleted -= OnSlimeUpgradeFocusAdvanced;
            Debug.LogWarning("슬라임 업그레이드 항목으로 이동할 수 없어 게이지 설명으로 이동합니다.");
            ShowSpawnGaugeStep();
        }
    }

    private void OnSlimeUpgradeFocusAdvanced()
    {
        if (_step != Step.FocusingSlimeUpgrade) return;

        if (_systemUpgradePanel.IsSelected(EUpgradeType.AllSlimePointPercentAdd))
        {
            ShowSlimeUpgradeGuide();
            return;
        }

        if (!_systemUpgradePanel.TryFocus(EUpgradeType.AllSlimePointPercentAdd))
        {
            _systemUpgradePanel.RotationCompleted -= OnSlimeUpgradeFocusAdvanced;
            ShowSpawnGaugeStep();
        }
    }

    private void ShowSlimeUpgradeGuide()
    {
        _systemUpgradePanel.RotationCompleted -= OnSlimeUpgradeFocusAdvanced;

        RectTransform target = _systemUpgradePanel.SelectedItemTarget ??
                               _systemUpgradePanel.TutorialTarget;
        _step = Step.SlimeUpgrade;
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);
        Spotlight.ShowUiTarget(
            Content.SystemUpgradeCarouselMessage,
            target,
            SpotlightInteractionMode.AdvanceOnPrimaryTap);
    }

    private void ShowSpawnGaugeStep()
    {
        if (_spawnGaugeTarget == null)
        {
            Debug.LogWarning("강조할 슬라임 스폰 게이지가 없어 마무리 대화로 이동합니다.");
            ShowFinalDialogue();
            return;
        }

        Spotlight.ShowUiFocus(_spawnGaugeTarget);
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
        _promotedTutorialSlime = null;
        _mergeTutorialSlime = null;
        SpawnManager.Instance?.SetSpawningPaused(false);
        _autoClicker?.SetPaused(false);
        _clicker.ReleaseMode(this);
        CompleteTutorial();
        GameplaySpaceManager.Instance?.RefreshInteraction();
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
