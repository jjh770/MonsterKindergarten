using UnityEngine;

// 졸업식을 본 뒤 포인트로 뽑기권을 살 수 있다는 것을 알려 주는 마지막 안내다.
//
// 해금 레벨이 아니라 졸업식이 시작을 정하므로 순서 줄 밖에 둔다(isQueued: false). 졸업식은 장식장에서 열리고
// 끝나도 장식장에 머무르므로, 메인 필드로 돌아온 뒤에 시작한다. 그래서 공간이 바뀌는 순간을 따로 받지 않고
// 짧은 간격으로 조건을 본다. 이 기능보다 먼저 졸업한 계정도 다음 접속에서 한 번 보게 된다.
//
// 흐름: 학자 슬라임 대화 -> 뽑기 버튼을 누르게 함 -> 기계 화면이 열리면 끝낸다.
// 기계 화면은 튜토리얼 안내보다 위에 그려지므로 그 안에서는 스포트라이트를 띄우지 않는다. 대신 한 번도 사 보지
// 않은 계정에는 기계 화면이 구매 버튼을 스스로 두근거리게 한다(GachaResultDirector). 구매를 시키지는 않는다.
// 열린 순간 완료로 남기므로 화면 안에서 앱이 내려가도 다시 보이지 않는다.
public sealed class PointTicketShopTutorialSequence : TutorialSequenceBase
{
    public override string TutorialId => TutorialIds.PointTicketShop;

    private enum Step
    {
        None,
        Dialogue,
        GachaButton,
        Complete,
    }

    [SerializeField] private GachaButtonUI _gachaButton;
    [SerializeField] private UnlockPopupUI _unlockPopupUI;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private AutoClicker _autoClicker;
    [SerializeField] private SpawnManager _spawnManager;
    [SerializeField] private GameplaySpaceManager _gameplaySpaceManager;
    [SerializeField, Min(0.05f)] private float _checkInterval = 0.5f;

    private Step _step;
    private float _timer;

    private void Start()
    {
        if (_gachaButton == null || _slimeManager == null || _clicker == null ||
            _autoClicker == null || _spawnManager == null || _gameplaySpaceManager == null)
        {
            Debug.LogError("포인트 뽑기권 구매 튜토리얼의 GameScene 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _gachaButton.MachineOpened += OnMachineOpened;
    }

    private void OnDestroy()
    {
        if (_gachaButton != null) _gachaButton.MachineOpened -= OnMachineOpened;

        if (_step != Step.None && _step != Step.Complete)
        {
            ReleaseGameplayHold(_spawnManager, _autoClicker, _clicker);
        }
    }

    private void Update()
    {
        if (_step != Step.None) return;

        _timer += Time.unscaledDeltaTime;
        if (_timer < _checkInterval) return;

        _timer = 0f;
        TryStart();
    }

    private void TryStart()
    {
        if (!_slimeManager.IsInitialized || !_slimeManager.IsMainEndingSeen) return;
        if (!IsCommonStartGateOpen(
                _spawnManager,
                _gameplaySpaceManager,
                _unlockPopupUI,
                TutorialIds.PointTicketShop))
        {
            return;
        }

        RectTransform target = _gachaButton.ButtonTarget;
        if (target == null || !target.gameObject.activeInHierarchy) return;
        if (!TryBeginTutorial()) return;

        _step = Step.Dialogue;
        AcquireGameplayHold(_spawnManager, _autoClicker);
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);
        Spotlight.Hide();
        ShowDialogue(Content.GetDialogue(DialogueId.PointTicketShop), ShowGachaButtonStep);
    }

    private void ShowGachaButtonStep()
    {
        RectTransform target = _gachaButton.ButtonTarget;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Abort("강조할 뽑기 버튼이 없습니다.");
            return;
        }

        _step = Step.GachaButton;
        Spotlight.ShowUiTarget(
            Content.PointTicketShopButtonMessage,
            target,
            SpotlightInteractionMode.PassThroughPrimary);
    }

    private void OnMachineOpened()
    {
        if (_step != Step.GachaButton) return;

        Spotlight.Hide();
        TutorialProgress.MarkCompleted(TutorialIds.PointTicketShop);
        _step = Step.Complete;
        FinishGameplayTeardown(_spawnManager, _autoClicker, _clicker, _gameplaySpaceManager);
    }

    // 완료로 남기지 않고 접어, 조건이 다시 맞으면 처음부터 보여 준다.
    private void Abort(string reason)
    {
        Debug.LogWarning($"포인트 뽑기권 구매 튜토리얼을 중단합니다. : {reason}", this);
        Spotlight.Hide();
        _step = Step.None;
        FinishGameplayTeardown(_spawnManager, _autoClicker, _clicker, _gameplaySpaceManager);
    }
}
