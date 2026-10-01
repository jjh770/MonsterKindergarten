using UnityEngine;

// 상점 해금(최고 Lv.9) 안내. 서랍 손잡이를 짚어 열게 하고, 열린 상점의 탭과 물건을 놓는 방법을 알려 준다.
// 손잡이는 Lv.9를 넘기는 순간 나타나고(PlaygroundShopUI), 이 안내는 그 해금 팝업이 끝난 뒤에 시작한다.
// 구매는 시키지 않는다. 무엇을 살지는 플레이어가 정한다.
public sealed class ShopTutorialSequence : TutorialSequenceBase
{
    public override string TutorialId => TutorialIds.Shop;

    private enum Step
    {
        None,
        Dialogue,
        DrawerButton,
        ShopTabs,
        Complete,
    }

    [SerializeField] private UpgradeUI _drawer;
    [SerializeField] private PlaygroundShopUI _shop;
    [SerializeField] private UnlockPopupUI _unlockPopupUI;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private AutoClicker _autoClicker;
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private SpawnManager _spawnManager;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private GameplaySpaceManager _gameplaySpaceManager;

    private Step _step;

    private void Start()
    {
        if (_drawer == null || _shop == null || _gameManager == null ||
            _spawnManager == null || _slimeManager == null ||
            _gameplaySpaceManager == null)
        {
            Debug.LogError("상점 튜토리얼의 GameScene 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        if (_unlockPopupUI != null)
        {
            _unlockPopupUI.PresentationCompleted += OnUnlockPresentationCompleted;
        }

        _drawer.Opened += OnDrawerOpened;
        SubscribeStandardStartTriggers(_gameManager, _spawnManager, TryStart);

        TryStart();
    }

    private void OnDestroy()
    {
        if (_unlockPopupUI != null)
        {
            _unlockPopupUI.PresentationCompleted -= OnUnlockPresentationCompleted;
        }

        if (_drawer != null)
        {
            _drawer.Opened -= OnDrawerOpened;
        }

        UnsubscribeStandardStartTriggers(_gameManager, _spawnManager, TryStart);

        _clicker?.ReleaseMode(this);
        if (_step != Step.None && _step != Step.Complete)
        {
            _spawnManager?.ReleaseSpawnPause(this);
            _autoClicker?.ReleasePause(this);
        }
    }

    private void OnUnlockPresentationCompleted(ESlimeGrade grade)
    {
        TryStart();
    }

    private void TryStart()
    {
        if (_step != Step.None) return;
        if (_slimeManager == null || !_slimeManager.IsShopUnlocked) return;
        // 손잡이가 보이지 않으면 짚을 수 없다. 보내기 모드 같은 연출이 치워 둔 동안은 기다린다.
        if (_drawer == null || !_drawer.IsToggleVisible) return;
        if (!IsCommonStartGateOpen(
                _spawnManager,
                _gameplaySpaceManager,
                _unlockPopupUI,
                TutorialIds.Shop))
        {
            return;
        }

        Begin();
    }

    private void Begin()
    {
        if (!TryBeginTutorial()) return;

        _step = Step.Dialogue;
        AcquireGameplayHold(_spawnManager, _autoClicker);
        _clicker?.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);
        _drawer.TryClose();
        Spotlight.Hide();

        ShowDialogue(
            Content.GetDialogue(DialogueId.Shop),
            ShowDrawerButtonStep);
    }

    private void ShowDrawerButtonStep()
    {
        RectTransform toggleTarget = _drawer.ToggleTarget;
        if (toggleTarget == null)
        {
            Complete();
            return;
        }

        _step = Step.DrawerButton;
        Spotlight.ShowUiTarget(
            Content.ShopButtonMessage,
            toggleTarget,
            SpotlightInteractionMode.PassThroughPrimary);
    }

    private void OnDrawerOpened()
    {
        if (_step != Step.DrawerButton) return;

        RectTransform tabsTarget = _shop.TabsTarget;
        if (tabsTarget != null)
        {
            Spotlight.ShowUiFocus(tabsTarget);
        }

        _step = Step.ShopTabs;
        ShowDialogue(
            Content.GetDialogue(DialogueId.ShopTabs),
            CloseDrawerAndComplete,
            keepGuideVisible: tabsTarget != null);
    }

    private void CloseDrawerAndComplete()
    {
        _drawer.TryClose();
        Complete();
    }

    private void Complete()
    {
        if (_step == Step.Complete) return;

        TutorialProgress.MarkCompleted(TutorialIds.Shop);
        _step = Step.Complete;
        FinishGameplayTeardown(_spawnManager, _autoClicker, _clicker, _gameplaySpaceManager);
    }
}
