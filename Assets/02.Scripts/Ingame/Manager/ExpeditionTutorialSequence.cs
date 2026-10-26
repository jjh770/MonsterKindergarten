using DG.Tweening;
using UnityEngine;

// 슬라임 탐험을 알려 준다. 탐험 버튼은 장식장 메뉴 줄의 끝에 있어 있는 줄도 모르는 일이 많았다.
//
// 시작은 해금 레벨이 아니라 장식장에 들어온 순간이다. 탐험 버튼은 장식장에서만 보이고, 장식장에 슬라임이
// 있어야 보낼 친구가 있다. 장식장에 들어오지 않는 플레이어 때문에 다른 안내가 막히면 안 되므로 순서 줄
// 밖에 두고(isQueued: false), 뽑기·장식장 안내를 마쳤는지는 여기서 직접 본다.
//
// 흐름: 학자 슬라임 대화 -> 메뉴 줄을 탐험 버튼까지 밀고 그 버튼을 누르게 함 -> 탐험 화면에서 탐험지,
// 편성, 보내기 버튼을 차례로 짚으며 설명하고 끝낸다.
//
// 탐험을 보내게 하지는 않는다. 보내면 그 슬라임은 몇 시간 장식장을 비우므로 플레이어가 정할 일이다.
// 탐험 화면도 튜토리얼이 도는 동안은 보내기를 받지 않는다(ExpeditionUI.OnAction). 끝나면 화면은 열어 둔다.
public sealed class ExpeditionTutorialSequence : TutorialSequenceBase
{
    public override string TutorialId => TutorialIds.Expedition;

    private enum Step
    {
        None,
        Intro,
        Scrolling,
        EnterButton,
        Opening,
        Destinations,
        Party,
        Final,
        Complete,
    }

    [SerializeField] private ExpeditionUI _expedition;
    [SerializeField] private HorizontalButtonScroll _menuScroll;
    [SerializeField] private UnlockPopupUI _unlockPopupUI;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private AutoClicker _autoClicker;
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private SpawnManager _spawnManager;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private GameplaySpaceManager _gameplaySpaceManager;

    [Tooltip("탐험 화면이 튀어 오른 뒤 설명을 시작하기까지 기다리는 시간입니다.")]
    [SerializeField, Min(0f)] private float _openSettleSeconds = 0.35f;

    private Step _step;
    private Tween _settleTween;

    private void Start()
    {
        if (_expedition == null || _menuScroll == null || _clicker == null || _gameManager == null ||
            _spawnManager == null || _slimeManager == null || _gameplaySpaceManager == null)
        {
            Debug.LogError("탐험 튜토리얼의 GameScene 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _expedition.Opened += OnExpeditionOpened;
        _gameplaySpaceManager.SpaceTransitionCompleted += TryStart;
        SubscribeStandardStartTriggers(_gameManager, _spawnManager, TryStart);
    }

    private void Update()
    {
        // 설명 도중에 플레이어가 뒤로 가기로 탐험 화면을 닫으면 짚을 것이 없다. 볼 것은 이미 봤으니 끝낸다.
        if (_step >= Step.Destinations && _step < Step.Complete && !_expedition.IsOpen)
        {
            Complete();
            return;
        }

        // 버튼을 누르기 전에 장식장을 떠나면 다음에 다시 들어올 때 처음부터 보여 준다.
        if ((_step == Step.Scrolling || _step == Step.EnterButton) && _gameplaySpaceManager.IsMainFieldActive)
        {
            Abort("장식장을 떠났습니다.");
        }
    }

    private void OnDestroy()
    {
        _settleTween?.Kill();
        if (_expedition != null) _expedition.Opened -= OnExpeditionOpened;
        if (_gameplaySpaceManager != null) _gameplaySpaceManager.SpaceTransitionCompleted -= TryStart;
        UnsubscribeStandardStartTriggers(_gameManager, _spawnManager, TryStart);

        _clicker?.ReleaseMode(this);
        if (_step != Step.None && _step != Step.Complete)
        {
            _spawnManager?.ReleaseSpawnPause(this);
            _autoClicker?.ReleasePause(this);
        }
    }

    private void TryStart()
    {
        if (_step != Step.None) return;
        if (!TutorialProgress.CanStart(TutorialIds.Expedition)) return;
        if (!TutorialProgress.IsCompleted(TutorialIds.Gacha) ||
            !TutorialProgress.IsCompleted(TutorialIds.DisplayRoom))
        {
            return;
        }

        if (!_slimeManager.IsGachaUnlocked || _slimeManager.DisplayRoomSlimeCount <= 0) return;
        if (!GameplayGate.IsActive || !_spawnManager.IsInitialized) return;
        if (_gameplaySpaceManager.IsMainFieldActive || _gameplaySpaceManager.IsTransitioning) return;
        if (_unlockPopupUI != null && _unlockPopupUI.IsPresenting) return;

        RectTransform button = _expedition.EnterButtonTarget;
        if (button == null || !button.gameObject.activeInHierarchy || _expedition.IsOpen) return;

        Begin();
    }

    private void Begin()
    {
        if (!TryBeginTutorial()) return;

        _step = Step.Intro;
        AcquireGameplayHold(_spawnManager, _autoClicker);
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);
        Spotlight.Hide();

        ShowDialogue(Content.GetDialogue(DialogueId.ExpeditionIntro), ShowEnterButtonStep);
    }

    // 탐험 버튼은 메뉴 줄의 끝쪽에 있어 줄이 밀려 있지 않으면 화면 밖이다. 보이는 곳까지 민 뒤에 구멍을 낸다.
    private void ShowEnterButtonStep()
    {
        RectTransform target = _expedition.EnterButtonTarget;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Abort("강조할 탐험 버튼이 없습니다.");
            return;
        }

        _step = Step.Scrolling;
        Spotlight.Hide();
        _menuScroll.ScrollToVisible(target, ShowEnterButtonSpotlight);
    }

    private void ShowEnterButtonSpotlight()
    {
        if (_step != Step.Scrolling) return;

        RectTransform target = _expedition.EnterButtonTarget;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Abort("강조할 탐험 버튼이 없습니다.");
            return;
        }

        _step = Step.EnterButton;
        Spotlight.ShowUiTarget(
            Content.ExpeditionButtonMessage,
            target,
            SpotlightInteractionMode.PassThroughPrimary);
    }

    private void OnExpeditionOpened()
    {
        if (_step != Step.EnterButton) return;

        _step = Step.Opening;
        Spotlight.Hide();
        // 본체가 튀어 오르는 동안에는 크기가 바뀌어 구멍이 어긋난다. 자리를 잡은 뒤에 짚는다.
        _settleTween?.Kill();
        _settleTween = DOVirtual.DelayedCall(_openSettleSeconds, ShowDestinationsStep).SetUpdate(true);
    }

    private void ShowDestinationsStep()
    {
        _settleTween = null;
        if (_step != Step.Opening) return;

        _step = Step.Destinations;
        ShowExplanation(_expedition.DestinationTabsTarget, DialogueId.ExpeditionDestinations, ShowPartyStep);
    }

    private void ShowPartyStep()
    {
        if (_step != Step.Destinations) return;

        _step = Step.Party;
        ShowExplanation(_expedition.MemberListTarget, DialogueId.ExpeditionParty, ShowFinalStep);
    }

    private void ShowFinalStep()
    {
        if (_step != Step.Party) return;

        _step = Step.Final;
        ShowExplanation(_expedition.ActionButtonTarget, DialogueId.ExpeditionFinal, Complete);
    }

    // 짚을 자리를 밝히고 그 바로 옆에 대사를 띄운다. 자리가 없으면 대사만 띄운다.
    private void ShowExplanation(RectTransform target, DialogueId dialogue, System.Action next)
    {
        bool hasTarget = target != null && target.gameObject.activeInHierarchy;
        if (hasTarget) Spotlight.ShowUiFocus(target);
        else Spotlight.Hide();

        ShowDialogue(
            Content.GetDialogue(dialogue),
            next,
            keepGuideVisible: hasTarget,
            placement: hasTarget ? DialoguePlacement.NearSpotlight : DialoguePlacement.Bottom);
    }

    private void Complete()
    {
        if (_step == Step.Complete) return;

        _settleTween?.Kill();
        TutorialProgress.MarkCompleted(TutorialIds.Expedition);
        Spotlight.Hide();
        _step = Step.Complete;
        FinishGameplayTeardown(_spawnManager, _autoClicker, _clicker, _gameplaySpaceManager);
    }

    // 완료로 기록하지 않고 접는다. 다음에 장식장에 들어올 때 다시 시작한다.
    private void Abort(string reason)
    {
        Debug.LogWarning($"탐험 튜토리얼을 중단합니다. : {reason}", this);
        _settleTween?.Kill();
        Spotlight.Hide();
        _step = Step.None;
        FinishGameplayTeardown(_spawnManager, _autoClicker, _clicker, _gameplaySpaceManager);
    }
}
