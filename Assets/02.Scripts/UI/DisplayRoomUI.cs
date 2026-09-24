using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class DisplayRoomUI : MonoBehaviour
{
    [Header("Common")]
    [SerializeField] private BottomPanelSwitcher _panelSwitcher;
    [SerializeField] private SpaceToggleButtonUI _spaceToggleButton;
    [SerializeField] private Button _sendButton;
    [FormerlySerializedAs("_stageUI")]
    [SerializeField] private BackgroundThemeUI _backgroundThemeUI;
    [SerializeField] private GameExitManager _gameExitManager;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private UpgradeUI _upgradeUI;

    [Header("Send Mode")]
    [SerializeField] private GameObject _sendModeRoot;
    [SerializeField] private CanvasGroup _sendModeCanvasGroup;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private ToastMessageUI _toast;
    // 하단은 요청하지 않는다. 시스템 업그레이드 패널은 BottomPanelSwitcher가,
    // 업그레이드 서랍은 UpgradeUI가 각자 숨김을 처리한다.
    [SerializeField] private HudVisibility _hudVisibility;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float _modeAnimationDuration = 0.35f;

    private GameplayModeSession _sendModeSession;
    private bool _isSendMode;
    private bool _isTransferPlaying;
    private Vector3 _transferStartPosition;

    public RectTransform SendButtonTarget => _sendButton != null
        ? _sendButton.transform as RectTransform
        : null;
    public bool IsSendMode => _isSendMode;
    public event Action SendModeStarted;
    public event Action SendModeEnded;
    public event Action<SlimeController> SlimeTransferred;

    private void Start()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        _sendModeSession = new GameplayModeSession(
            _upgradeUI,
            _clicker,
            _gameExitManager,
            _hudVisibility,
            EHudParts.Top,
            _sendModeRoot,
            _sendModeCanvasGroup,
            _modeAnimationDuration);
        _sendModeSession.ResetPresentation();
        _toast.Hide();

        _panelSwitcher.MovePanelPresentationChanged += OnMovePanelPresentationChanged;
        _spaceToggleButton.Clicked += OnSpaceButtonClicked;
        _sendButton.onClick.AddListener(BeginSendMode);
        _cancelButton.onClick.AddListener(CancelSendMode);
        _clicker.TargetClicked += OnTargetClicked;
        GameplaySpaceManager.Instance.SpaceChanged += OnSpaceChanged;
        GameplaySpaceManager.Instance.BackgroundThemeTransitionCompleted += OnBackgroundThemeTransitionCompleted;
        GameManager.OnAllDataInitialized += Refresh;
        GameManager.Instance.OnGameplayActivated += Refresh;
        SlimeManager.OnHighestGradeChanged += OnHighestGradeChanged;
        TutorialManager.Started += Refresh;
        TutorialManager.Finished += Refresh;

        bool isDisplayRoom =
            GameplaySpaceManager.Instance.CurrentSpace == EGameplaySpace.DisplayRoom;
        _panelSwitcher.ResetSelection(isDisplayRoom);
        ApplySpacePresentation(isDisplayRoom, animated: false);
        Refresh();
    }

    private void OnDestroy()
    {
        _sendModeSession?.Dispose();

        if (_panelSwitcher != null)
        {
            _panelSwitcher.MovePanelPresentationChanged -= OnMovePanelPresentationChanged;
        }

        if (_spaceToggleButton != null)
        {
            _spaceToggleButton.Clicked -= OnSpaceButtonClicked;
        }

        _sendButton?.onClick.RemoveListener(BeginSendMode);
        _cancelButton?.onClick.RemoveListener(CancelSendMode);

        if (_clicker != null)
        {
            _clicker.TargetClicked -= OnTargetClicked;
        }

        if (GameplaySpaceManager.Instance != null)
        {
            GameplaySpaceManager.Instance.SpaceChanged -= OnSpaceChanged;
            GameplaySpaceManager.Instance.BackgroundThemeTransitionCompleted -= OnBackgroundThemeTransitionCompleted;
        }

        GameManager.OnAllDataInitialized -= Refresh;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameplayActivated -= Refresh;
        }

        SlimeManager.OnHighestGradeChanged -= OnHighestGradeChanged;
        TutorialManager.Started -= Refresh;
        TutorialManager.Finished -= Refresh;
        _gameExitManager?.UnregisterBackHandler(this);
    }

    private bool HasRequiredReferences()
    {
        bool hasReferences = _panelSwitcher != null &&
                             _spaceToggleButton != null &&
                             _sendButton != null &&
                             _backgroundThemeUI != null &&
                             _gameExitManager != null &&
                             _clicker != null &&
                             _upgradeUI != null &&
                             _sendModeRoot != null &&
                             _sendModeCanvasGroup != null &&
                             _hudVisibility != null &&
                             _cancelButton != null &&
                             _toast != null &&
                             GameManager.Instance != null &&
                             GameplaySpaceManager.Instance != null;
        if (!hasReferences)
        {
            Debug.LogError("장식장 UI의 필수 참조가 비어 있습니다.", this);
        }

        return hasReferences;
    }

    private void OnSpaceButtonClicked()
    {
        GameplaySpaceManager spaceManager = GameplaySpaceManager.Instance;
        if (spaceManager == null || _isSendMode) return;

        if (spaceManager.IsMainFieldActive)
        {
            spaceManager.TryEnterDisplayRoom();
        }
        else
        {
            spaceManager.TryExitDisplayRoom();
        }
    }

    private void BeginSendMode()
    {
        GameplaySpaceManager spaceManager = GameplaySpaceManager.Instance;
        if (_isSendMode ||
            spaceManager == null ||
            !spaceManager.IsMainFieldActive ||
            spaceManager.IsTransitioning)
        {
            return;
        }

        _isSendMode = true;
        _toast.Hide();
        _sendModeSession.Enter(
            ClickerInputMode.SelectOnly(),
            ClickerInputPriority.Selection,
            TryCancelSendMode,
            bringRootToFront: true);
        Refresh();
        SendModeStarted?.Invoke();
    }

    public void CancelSendMode()
    {
        TryCancelSendMode();
    }

    private bool TryCancelSendMode()
    {
        if (!_isSendMode) return false;

        // 전송 연출 중에는 취소하지 않되 입력은 소비한다.
        // false를 반환하면 GameExitManager가 이 핸들러를 목록에서 제거해
        // 전송이 끝난 뒤 뒤로가기로 선택 모드를 빠져나갈 수 없게 된다.
        if (_isTransferPlaying) return true;

        EndSendMode();
        return true;
    }

    private void EndSendMode()
    {
        _isSendMode = false;
        _isTransferPlaying = false;
        _toast.Hide();
        _sendModeSession.Exit();

        GameplaySpaceManager spaceManager = GameplaySpaceManager.Instance;
        bool isDisplayRoom = spaceManager != null && !spaceManager.IsMainFieldActive;
        ApplySpacePresentation(isDisplayRoom, animated: true);
        SendModeEnded?.Invoke();
    }

    private void OnTargetClicked(SlimeController target)
    {
        if (!_isSendMode || _isTransferPlaying || target == null) return;
        if (target.Location != ESlimeLocation.MainField ||
            !target.IsMainFieldActive)
        {
            return;
        }

        if (SlimeManager.Instance == null ||
            !SlimeManager.Instance.CanMoveToDisplayRoom(
                target.Grade,
                target.IsSpecial))
        {
            _toast.Show("같은 종류의 슬라임이 이미 장식장에 있어요.");
            return;
        }

        _isTransferPlaying = true;
        _transferStartPosition = target.transform.position;
        _sendModeSession.SetInputMode(
            ClickerInputMode.Blocked,
            ClickerInputPriority.Modal);
        GameplaySpaceManager.Instance.PlayDisplayRoomTransfer(
            target,
            () => CompleteTransfer(target));
    }

    private void CompleteTransfer(SlimeController target)
    {
        GameplaySpaceManager spaceManager = GameplaySpaceManager.Instance;
        if (target == null || spaceManager == null)
        {
            EndSendMode();
            return;
        }

        _isTransferPlaying = false;
        bool moved = spaceManager.TryRelocateSlime(
            target,
            ESlimeLocation.DisplayRoom,
            _transferStartPosition);
        ApplySendModeInput();

        if (moved)
        {
            SlimeTransferred?.Invoke(target);
            return;
        }

        _toast.Show("이 슬라임은 장식장으로 보낼 수 없어요.");
    }

    private void ApplySendModeInput()
    {
        _sendModeSession.SetInputMode(
            ClickerInputMode.SelectOnly(),
            ClickerInputPriority.Selection);
    }

    private void OnBackgroundThemeTransitionCompleted()
    {
        if (_isSendMode)
        {
            ApplySendModeInput();
        }
    }

    private void OnSpaceChanged(EGameplaySpace space)
    {
        bool isDisplayRoom = space == EGameplaySpace.DisplayRoom;
        _panelSwitcher.ResetSelection(selectMovePanel: true);
        ApplySpacePresentation(isDisplayRoom, animated: true);

        RefreshBackHandler();

        Refresh();
    }

    private void ApplySpacePresentation(bool isDisplayRoom, bool animated)
    {
        _spaceToggleButton.SetSpace(isDisplayRoom);
        // 상점은 장식장에서도 쓴다. 공간에 따라 파는 물건만 달라진다.
        _upgradeUI.SetToggleVisible(true, animated);
        Refresh();
    }

    private bool TryExitDisplayRoom()
    {
        return GameplaySpaceManager.Instance != null &&
               GameplaySpaceManager.Instance.TryExitDisplayRoom();
    }

    private bool HandleBack()
    {
        return TryExitDisplayRoom();
    }

    private void RefreshBackHandler()
    {
        if (_gameExitManager == null) return;

        bool isDisplayRoom = GameplaySpaceManager.Instance != null &&
                             !GameplaySpaceManager.Instance.IsMainFieldActive;
        if (isDisplayRoom)
        {
            _gameExitManager.RegisterBackHandler(this, HandleBack);
        }
        else if (!_isSendMode)
        {
            _gameExitManager.UnregisterBackHandler(this);
        }
    }

    private void OnHighestGradeChanged(ESlimeGrade grade)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_panelSwitcher == null ||
            _spaceToggleButton == null ||
            _sendButton == null ||
            _backgroundThemeUI == null)
        {
            return;
        }

        GameplaySpaceManager spaceManager = GameplaySpaceManager.Instance;
        bool isDisplayRoom = spaceManager != null &&
                             !spaceManager.IsMainFieldActive;
        _panelSwitcher.ApplyContext(
            isAreaVisible: !_isSendMode,
            forceMovePanel: isDisplayRoom,
            canSwitch: IsDisplayRoomUnlocked());
    }

    private bool IsDisplayRoomUnlocked()
    {
        return GameplayGate.IsDisplayRoomAvailable;
    }

    // 이동 패널 안에 있는 버튼 중 이 컴포넌트가 소유한 것만 처리한다.
    // 장식장 버튼은 패널의 자식이라 부모 활성 상태가 그대로 노출을 결정하고,
    // 보내기 버튼만 장식장 안에서 추가로 숨긴다.
    private void OnMovePanelPresentationChanged(bool isMovePanelVisible)
    {
        GameplaySpaceManager spaceManager = GameplaySpaceManager.Instance;
        bool isDisplayRoom = spaceManager != null &&
                             !spaceManager.IsMainFieldActive;
        bool showChildren = isMovePanelVisible && !isDisplayRoom;

        _backgroundThemeUI.SetMenuPresentation(showChildren);
        _sendButton.gameObject.SetActive(showChildren);
    }

}
