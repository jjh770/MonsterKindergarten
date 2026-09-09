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

    // 자리를 만들 수 없어 위치만 알리는 중인지. 이때는 버튼을 누르게 하지 않는다.
    private bool _isGachaButtonInfoOnly;

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
            !GameplayGate.IsActive ||
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
            // 합칠 짝이 없으면 자리를 만드는 법을 보여 줄 수 없다. 여기서 중단하면
            // 완료 표시가 서지 않아 티켓 드랍까지 함께 멎으므로, 뽑기 없이 버튼
            // 위치만 알리고 끝낸다.
            //
            // 해금 직후에는 닿지 않는 경로다. Lv.7이면 등급이 일곱뿐이고 최대 개체
            // 수는 그보다 많아 자리가 찼다는 것이 곧 같은 등급이 있다는 뜻이다.
            // 등급이 넓게 벌어진 뒤에 처음 이 안내를 받는 기존 계정에서만 닿는다.
            ShowGachaButtonInfoStep();
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
        _isGachaButtonInfoOnly = false;
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);
        Spotlight.ShowUiTarget(
            Content.GachaButtonMessage,
            target,
            SpotlightInteractionMode.PassThroughPrimary,
            useRectangularHole: true);
    }

    // 자리가 없는 채로 끝내는 경로. ShowGachaButtonStep은 자리가 없으면
    // ShowMakeRoomStep으로 되돌아가므로, 그쪽에서 부르면 둘이 무한히 오간다.
    private void ShowGachaButtonInfoStep()
    {
        RectTransform target = _gachaButton.ButtonTarget;
        if (target == null)
        {
            Abort("가챠 버튼 참조가 없습니다.");
            return;
        }

        _step = Step.GachaButton;
        _isGachaButtonInfoOnly = true;
        SubscribeGuide();
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);

        // 실제로 누르면 자리가 없어 실패한다. 구멍 안을 탭하면 버튼을 누르지 않고
        // 넘어가는 모드로 위치만 알린다.
        Spotlight.ShowUiTarget(
            Content.GachaButtonMessage,
            target,
            SpotlightInteractionMode.AdvanceOnPrimaryTap,
            useRectangularHole: true);
    }

    private void OnGachaPullSucceeded(SlimeController spawned)
    {
        if (_step != Step.GachaButton || spawned == null) return;

        // 완료 표시를 결과 대화가 아니라 여기서 한다. 되돌릴 수 없는 지점이 뽑기라,
        // 대화 도중에 앱이 내려가면 티켓은 이미 쓰였는데 플래그가 없어 다음 실행에서
        // 한 장을 다시 받는다. 그 반복이 티켓을 무한히 늘린다.
        //
        // 뽑기 전에 끊긴 경우는 여전히 처음부터 다시 안내한다. 그때는 준 티켓이
        // 그대로 남아 있어 TryStart의 지급 가드가 두 장이 되는 것을 막는다.
        TutorialProgress.MarkCompleted(TutorialIds.Gacha);

        _step = Step.Result;
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);

        // 결과가 다른 스테이지 소속이면 지금 화면에 없다. 연출이 어디로 갔는지 이미
        // 보여 주었으므로 가리키지 않고 대화만 잇는다. 등급이 벌어진 계정에서 닿는다.
        bool isOnCurrentStage = StageManager.Instance != null &&
                                StageManager.Instance.CurrentStage ==
                                GameStageRules.GetStage(spawned.Grade);
        if (isOnCurrentStage)
        {
            Spotlight.ShowFocus(spawned.transform);
        }

        ShowDialogue(
            Content.GetDialogue(DialogueId.GachaResult),
            Complete,
            keepGuideVisible: isOnCurrentStage);
    }

    private void OnGuideAdvanceRequested()
    {
        if (_step == Step.GachaButton)
        {
            if (_isGachaButtonInfoOnly) Complete();
            return;
        }

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
