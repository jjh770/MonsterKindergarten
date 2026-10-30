using UnityEngine;

// 졸업식을 본 뒤 포인트로 뽑기권을 살 수 있다는 것을 알려 주는 마지막 안내다.
//
// 해금 레벨이 아니라 졸업식이 시작을 정하므로 순서 줄 밖에 둔다(isQueued: false). 졸업식은 장식장에서 열리고
// 끝나도 장식장에 머무르므로, 메인 필드로 돌아온 뒤에 시작한다. 그래서 공간이 바뀌는 순간을 따로 받지 않고
// 짧은 간격으로 조건을 본다. 이 기능보다 먼저 졸업한 계정도 다음 접속에서 한 번 보게 된다.
//
// 흐름: 학자 슬라임 대화로 구매를 알려 주고 끝낸다. 뽑기 버튼을 누르게 하거나 사게 하지는 않는다.
// 기계 화면을 처음 열면, 한 번도 사 보지 않은 계정에는 구매 버튼이 스스로 두근거린다(GachaResultDirector).
// 뽑기 버튼이 보일 때만 시작한다. 대화가 그 버튼을 가리키는 말이기 때문이다.
public sealed class PointTicketShopTutorialSequence : TutorialSequenceBase
{
    public override string TutorialId => TutorialIds.PointTicketShop;

    private enum Step
    {
        None,
        Dialogue,
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
        }
    }

    private void OnDestroy()
    {
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
        ShowDialogue(Content.GetDialogue(DialogueId.PointTicketShop), Complete);
    }

    private void Complete()
    {
        if (_step != Step.Dialogue) return;

        TutorialProgress.MarkCompleted(TutorialIds.PointTicketShop);
        _step = Step.Complete;
        FinishGameplayTeardown(_spawnManager, _autoClicker, _clicker, _gameplaySpaceManager);
    }
}
