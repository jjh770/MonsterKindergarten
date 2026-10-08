using UnityEngine;

// 광고 보너스가 있다는 것을 알려 준다. 기획서 §21.
//
// 시작을 정하는 것은 해금 레벨이 아니라 플레이어의 행동이다. 포인트가 모자라 업그레이드를 못 산 직후,
// 곧 포인트가 필요한 바로 그 순간에 광고 보너스를 소개한다. 그래서 TutorialProgress의 순서 줄 밖에
// 두고(isQueued: false), 장식장 안내를 마쳤는지는 여기서 직접 본다. 장식장 안내를 마쳐야 광고 보너스
// 버튼이 들어 있는 이동 패널의 쓰임을 이미 아는 상태가 된다.
//
// 흐름: 학자 슬라임 대화 -> 하단 패널 전환 버튼을 누르게 함 -> 이동 패널이 열리면 줄을 광고 보너스 버튼이
// 보이는 곳까지 밀고 그 버튼을 누르게 함 -> 팝업이 열리면 두 보상을 설명하고 끝낸다.
//
// 광고를 보게 하지는 않는다. 보고 싶을 때만 보는 것이 이 보상의 약속이고, 한도를 쓰거나 보상을 주는 일이
// 튜토리얼 안에서 일어나서도 안 된다. 끝난 뒤 팝업은 열린 채로 두어 플레이어가 직접 닫는다.
//
// 부족 안내가 뜬 직후에는 안내 문구와 효과음이 아직 화면에 있다. 그것이 사라지기를 기다렸다가 시작해야
// 스포트라이트와 겹치지 않는다. 조건이 맞지 않으면 그 한 번은 넘기고, 다음 부족 안내에서 다시 본다.
public sealed class AdBonusTutorialSequence : TutorialSequenceBase
{
    public override string TutorialId => TutorialIds.AdBonus;

    private enum Step
    {
        None,
        Intro,
        SwitchButton,
        Scrolling,
        AdButton,
        Popup,
        Complete,
    }

    [SerializeField] private SystemUpgradePanel _upgradePanel;
    [Tooltip("업그레이드 패널이 띄우는 부족 안내와 같은 인스턴스입니다.")]
    [SerializeField] private MessagePopupUI _messagePopup;
    [SerializeField] private BottomPanelSwitcher _panelSwitcher;
    [SerializeField] private HorizontalButtonScroll _menuScroll;
    [SerializeField] private AdBonusUI _adBonusUI;
    [SerializeField] private UnlockPopupUI _unlockPopupUI;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private AutoClicker _autoClicker;
    [SerializeField] private SpawnManager _spawnManager;
    [SerializeField] private GameplaySpaceManager _gameplaySpaceManager;

    private Step _step;
    private bool _isPending;

    private void Start()
    {
        if (_upgradePanel == null || _messagePopup == null || _panelSwitcher == null ||
            _menuScroll == null || _adBonusUI == null || _clicker == null ||
            _spawnManager == null || _gameplaySpaceManager == null)
        {
            Debug.LogError("광고 보너스 튜토리얼의 GameScene 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _upgradePanel.InsufficientPointsNotified += OnInsufficientPoints;
        _panelSwitcher.MovePanelOpened += OnMovePanelOpened;
        _adBonusUI.Opened += OnAdBonusOpened;
    }

    private void Update()
    {
        if (!_isPending || _step != Step.None) return;
        // 부족 안내가 떠 있는 동안은 기다린다. 사라진 뒤 한 번만 시작을 시도한다.
        if (_messagePopup.IsShowing) return;

        _isPending = false;
        TryStart();
    }

    private void OnDestroy()
    {
        if (_upgradePanel != null)
        {
            _upgradePanel.InsufficientPointsNotified -= OnInsufficientPoints;
        }

        if (_panelSwitcher != null)
        {
            _panelSwitcher.MovePanelOpened -= OnMovePanelOpened;
        }

        if (_adBonusUI != null)
        {
            _adBonusUI.Opened -= OnAdBonusOpened;
        }

        _clicker?.ReleaseMode(this);
        if (_step != Step.None && _step != Step.Complete)
        {
            _spawnManager?.ReleaseSpawnPause(this);
            _autoClicker?.ReleasePause(this);
        }
    }

    private void OnInsufficientPoints()
    {
        if (_step != Step.None) return;
        // 이미 본 플레이어는 더 지켜볼 이유가 없다.
        if (TutorialProgress.IsCompleted(TutorialIds.AdBonus)) return;

        _isPending = true;
    }

    private void TryStart()
    {
        if (!TutorialProgress.IsCompleted(TutorialIds.DisplayRoom)) return;
        if (!IsCommonStartGateOpen(
                _spawnManager,
                _gameplaySpaceManager,
                _unlockPopupUI,
                TutorialIds.AdBonus))
        {
            return;
        }

        // 버튼이 없거나 팝업을 열 수 없는 상태(다른 전면 화면이 떠 있음)에서는 가리킬 수도 열 수도 없다.
        RectTransform button = _adBonusUI.ButtonTarget;
        if (!_adBonusUI.isActiveAndEnabled || button == null || !_adBonusUI.CanOpenNow) return;

        Begin();
    }

    private void Begin()
    {
        if (!TryBeginTutorial()) return;

        _step = Step.Intro;
        AcquireGameplayHold(_spawnManager, _autoClicker);
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);
        Spotlight.Hide();

        ShowDialogue(Content.GetDialogue(DialogueId.AdBonus), ShowSwitchButtonStep);
    }

    private void ShowSwitchButtonStep()
    {
        if (_panelSwitcher.IsMovePanelOpen)
        {
            ShowAdButtonStep();
            return;
        }

        RectTransform target = _panelSwitcher.SwitchButtonTarget;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Abort("강조할 하단 패널 전환 버튼이 없습니다.");
            return;
        }

        _step = Step.SwitchButton;
        // 장식장 안내와 같은 문구다. 이동 패널을 여는 같은 동작을 가리킨다.
        Spotlight.ShowUiTarget(
            Content.DisplayRoomButtonMessage,
            target,
            SpotlightInteractionMode.PassThroughPrimary);
    }

    private void OnMovePanelOpened()
    {
        if (_step != Step.SwitchButton) return;

        ShowAdButtonStep();
    }

    // 광고 보너스 버튼은 이동 패널 줄의 끝쪽에 있어 줄이 밀려 있으면 화면 밖이다.
    // 그 버튼이 보이는 곳까지 줄을 민 뒤에 구멍을 낸다.
    private void ShowAdButtonStep()
    {
        RectTransform target = _adBonusUI.ButtonTarget;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Abort("강조할 광고 보너스 버튼이 없습니다.");
            return;
        }

        _step = Step.Scrolling;
        Spotlight.Hide();
        _menuScroll.ScrollToVisible(target, ShowAdButtonSpotlight);
    }

    private void ShowAdButtonSpotlight()
    {
        if (_step != Step.Scrolling) return;

        RectTransform target = _adBonusUI.ButtonTarget;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Abort("강조할 광고 보너스 버튼이 없습니다.");
            return;
        }

        _step = Step.AdButton;
        Spotlight.ShowUiTarget(
            Content.AdBonusButtonMessage,
            target,
            SpotlightInteractionMode.PassThroughPrimary);
    }

    private void OnAdBonusOpened()
    {
        if (_step != Step.AdButton) return;

        _step = Step.Popup;
        Spotlight.Hide();
        ShowDialogue(Content.GetDialogue(DialogueId.AdBonusPopup), Complete);
    }

    private void Complete()
    {
        if (_step == Step.Complete) return;

        TutorialProgress.MarkCompleted(TutorialIds.AdBonus);
        _step = Step.Complete;
        FinishGameplayTeardown(_spawnManager, _autoClicker, _clicker, _gameplaySpaceManager);
    }

    // 완료로 기록하지 않고 접는다. 가리킬 것이 없어 끝낼 수 없는 상태라서, 다음 부족 안내에서 다시 시작할 수
    // 있게 한다.
    private void Abort(string reason)
    {
        Debug.LogWarning($"광고 보너스 튜토리얼을 중단합니다. : {reason}", this);
        Spotlight.Hide();
        _step = Step.None;
        FinishGameplayTeardown(_spawnManager, _autoClicker, _clicker, _gameplaySpaceManager);
    }
}
