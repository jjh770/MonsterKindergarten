using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class DisplayRoomUI : MonoBehaviour
{
    [Header("Common")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private BottomPanelSwitcher _panelSwitcher;
    [SerializeField] private SpaceToggleButtonUI _spaceToggleButton;
    [SerializeField] private Button _sendButton;
    [SerializeField] private StageUI _stageUI;
    [SerializeField] private GameExitManager _gameExitManager;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private UpgradeUI _upgradeUI;

    [Header("Send Mode")]
    [SerializeField] private GameObject _sendModeRoot;
    [SerializeField] private CanvasGroup _sendModeCanvasGroup;
    [SerializeField] private RectTransform _sendModePrompt;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private ToastMessageUI _toast;
    // 상단은 개별 요소가 아니라 HUD 루트째로 올린다. 잎 노드를 직접 옮기면
    // 세이프에어리어로 자기 위치를 다시 잡는 UI(옵션 버튼)와 좌표가 충돌한다.
    [SerializeField] private RectTransform _topUiRoot;
    // 하단은 BottomHudRoot에 보내기 모드 오버레이가 함께 들어 있어 루트째 내릴 수 없다.
    // 업그레이드 서랍은 자기 폭으로 숨김 위치를 계산하므로 여기서 옮기지 않는다.
    [SerializeField] private RectTransform _bottomUiTarget;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float _modeAnimationDuration = 0.35f;

    private Vector2 _topUiStartPosition;
    private Vector2 _bottomUiStartPosition;
    private Sequence _modeSequence;
    private bool _isSendMode;
    private bool _isTransferPlaying;
    private bool _wasUpgradeToggleInputEnabled;
    private Vector3 _transferStartPosition;

    public RectTransform SendButtonTarget => _sendButton != null
        ? _sendButton.transform as RectTransform
        : null;
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

        CacheUiPositions();
        _sendModeRoot.SetActive(false);
        _toast.Hide();

        _panelSwitcher.MovePanelPresentationChanged += OnMovePanelPresentationChanged;
        _spaceToggleButton.Clicked += OnSpaceButtonClicked;
        _sendButton.onClick.AddListener(BeginSendMode);
        _cancelButton.onClick.AddListener(CancelSendMode);
        _clicker.TargetClicked += OnTargetClicked;
        StageManager.Instance.SpaceChanged += OnSpaceChanged;
        StageManager.Instance.StageTransitionCompleted += OnStageTransitionCompleted;
        GameManager.OnAllDataInitialized += Refresh;
        GameManager.Instance.OnGameplayActivated += Refresh;
        SlimeManager.OnHighestGradeChanged += OnHighestGradeChanged;

        RefreshLayout();
        bool isDisplayRoom =
            StageManager.Instance.CurrentSpace == EGameplaySpace.DisplayRoom;
        _panelSwitcher.ResetSelection(isDisplayRoom);
        ApplySpacePresentation(isDisplayRoom, animated: false);
        Refresh();
    }

    private void OnDestroy()
    {
        _modeSequence?.Kill();

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
            _clicker.ReleaseMode(this);
        }

        if (StageManager.Instance != null)
        {
            StageManager.Instance.SpaceChanged -= OnSpaceChanged;
            StageManager.Instance.StageTransitionCompleted -= OnStageTransitionCompleted;
        }

        GameManager.OnAllDataInitialized -= Refresh;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameplayActivated -= Refresh;
        }

        SlimeManager.OnHighestGradeChanged -= OnHighestGradeChanged;
        _gameExitManager?.UnregisterBackHandler(this);
    }

    private bool HasRequiredReferences()
    {
        bool hasReferences = _canvas != null &&
                             _panelSwitcher != null &&
                             _spaceToggleButton != null &&
                             _sendButton != null &&
                             _stageUI != null &&
                             _gameExitManager != null &&
                             _clicker != null &&
                             _upgradeUI != null &&
                             _sendModeRoot != null &&
                             _sendModeCanvasGroup != null &&
                             _sendModePrompt != null &&
                             _topUiRoot != null &&
                             _bottomUiTarget != null &&
                             _cancelButton != null &&
                             _toast != null &&
                             GameManager.Instance != null &&
                             StageManager.Instance != null;
        if (!hasReferences)
        {
            Debug.LogError("장식장 UI의 필수 참조가 비어 있습니다.", this);
        }

        return hasReferences;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_canvas != null)
        {
            RefreshLayout();
        }
    }

    private void OnSpaceButtonClicked()
    {
        StageManager stageManager = StageManager.Instance;
        if (stageManager == null || _isSendMode) return;

        if (stageManager.IsMainStageActive)
        {
            stageManager.TryEnterDisplayRoom();
        }
        else
        {
            stageManager.TryExitDisplayRoom();
        }
    }

    private void BeginSendMode()
    {
        StageManager stageManager = StageManager.Instance;
        if (_isSendMode ||
            stageManager == null ||
            !stageManager.IsMainStageActive ||
            stageManager.IsTransitioning)
        {
            return;
        }

        _upgradeUI.TryClose();
        _wasUpgradeToggleInputEnabled = _upgradeUI.IsToggleInputEnabled;
        _upgradeUI.SetToggleInputEnabled(false);
        // 서랍은 자기 폭과 세이프에어리어로 숨김 위치를 계산한다.
        // 좌우 이동을 여기서 흉내 내지 않고 서랍에 맡긴다.
        _upgradeUI.SetToggleVisible(false);
        _isSendMode = true;
        _sendModeRoot.SetActive(true);
        _sendModeRoot.transform.SetAsLastSibling();
        _sendModeCanvasGroup.alpha = 0f;
        _toast.Hide();
        ApplySendModeInput();
        _gameExitManager.RegisterBackHandler(this, TryCancelSendMode);
        PlayModePresentation(show: true);
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
        _gameExitManager.UnregisterBackHandler(this);
        _clicker.ReleaseMode(this);
        // 튜토리얼이 이미 잠가둔 경우까지 활성화하지 않고 진입 전 상태로 복구한다.
        _upgradeUI.SetToggleInputEnabled(_wasUpgradeToggleInputEnabled);
        PlayModePresentation(show: false);

        StageManager stageManager = StageManager.Instance;
        bool isDisplayRoom = stageManager != null && !stageManager.IsMainStageActive;
        ApplySpacePresentation(isDisplayRoom, animated: true);
        SendModeEnded?.Invoke();
    }

    private void OnTargetClicked(SlimeController target)
    {
        if (!_isSendMode || _isTransferPlaying || target == null) return;
        if (target.Location != ESlimeLocation.MainStage ||
            !target.IsCurrentStageActive)
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
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Modal);
        StageManager.Instance.PlayDisplayRoomTransfer(
            target,
            () => CompleteTransfer(target));
    }

    private void CompleteTransfer(SlimeController target)
    {
        if (target == null || SlimeManager.Instance == null)
        {
            EndSendMode();
            return;
        }

        try
        {
            SlimeManager.Instance.MoveSlime(
                target.InstanceId,
                ESlimeLocation.DisplayRoom);
            Vector2 destination = SpawnManager.Instance != null
                ? SpawnManager.Instance.GetRandomSpawnPosition()
                : Vector2.zero;
            target.transform.position = new Vector3(
                destination.x,
                destination.y,
                target.transform.position.z);
            StageManager.Instance?.RefreshSlimePresentation(target);
            _isTransferPlaying = false;
            ApplySendModeInput();
            SlimeTransferred?.Invoke(target);
        }
        catch (Exception e) when (e is InvalidOperationException ||
                                  e is ArgumentException)
        {
            Debug.LogWarning($"슬라임을 장식장으로 보낼 수 없습니다: {e.Message}");
            _isTransferPlaying = false;
            target.transform.position = _transferStartPosition;
            StageManager.Instance?.RefreshSlimePresentation(target);
            ApplySendModeInput();
            _toast.Show("이 슬라임은 장식장으로 보낼 수 없어요.");
        }
    }

    private void ApplySendModeInput()
    {
        _clicker.PushMode(this, ClickerInputMode.SelectOnly(), ClickerInputPriority.Selection);
    }

    private void OnStageTransitionCompleted()
    {
        if (_isSendMode)
        {
            ApplySendModeInput();
        }
    }

    private void OnSpaceChanged(EGameplaySpace space)
    {
        bool isDisplayRoom = space == EGameplaySpace.DisplayRoom;
        _panelSwitcher.ResetSelection(isDisplayRoom);
        ApplySpacePresentation(isDisplayRoom, animated: true);
        RefreshLayout();

        RefreshBackHandler();

        Refresh();
    }

    private void ApplySpacePresentation(bool isDisplayRoom, bool animated)
    {
        _spaceToggleButton.SetSpace(isDisplayRoom);
        _upgradeUI.SetToggleVisible(!isDisplayRoom, animated);
        Refresh();
    }

    private bool TryExitDisplayRoom()
    {
        return StageManager.Instance != null &&
               StageManager.Instance.TryExitDisplayRoom();
    }

    private bool HandleBack()
    {
        return TryExitDisplayRoom();
    }

    private void RefreshBackHandler()
    {
        if (_gameExitManager == null) return;

        bool isDisplayRoom = StageManager.Instance != null &&
                             !StageManager.Instance.IsMainStageActive;
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
            _stageUI == null)
        {
            return;
        }

        StageManager stageManager = StageManager.Instance;
        bool isDisplayRoom = stageManager != null &&
                             !stageManager.IsMainStageActive;
        _panelSwitcher.ApplyContext(
            isAreaVisible: !_isSendMode,
            forceMovePanel: isDisplayRoom,
            canSwitch: IsDisplayRoomUnlocked());
    }

    private bool IsDisplayRoomUnlocked()
    {
        return GameManager.Instance != null &&
               GameManager.Instance.IsAllDataInitialized &&
               GameManager.Instance.IsGameplayActive &&
               SlimeManager.Instance != null &&
               SlimeManager.Instance.IsDisplayRoomUnlocked;
    }

    // 이동 패널 안에 있는 버튼 중 이 컴포넌트가 소유한 것만 처리한다.
    // 장식장 버튼은 패널의 자식이라 부모 활성 상태가 그대로 노출을 결정하고,
    // 보내기 버튼만 장식장 안에서 추가로 숨긴다.
    private void OnMovePanelPresentationChanged(bool isMovePanelVisible)
    {
        StageManager stageManager = StageManager.Instance;
        bool isDisplayRoom = stageManager != null &&
                             !stageManager.IsMainStageActive;
        bool showChildren = isMovePanelVisible && !isDisplayRoom;

        _stageUI.SetMenuPresentation(showChildren);
        _sendButton.gameObject.SetActive(showChildren);
    }

    private void CacheUiPositions()
    {
        _topUiStartPosition = _topUiRoot.anchoredPosition;
        _bottomUiStartPosition = _bottomUiTarget.anchoredPosition;
    }

    private void PlayModePresentation(bool show)
    {
        _modeSequence?.Kill();
        _modeSequence = DOTween.Sequence();
        float slideDistance = (_canvas.transform as RectTransform)?.rect.height ?? 1920f;

        _modeSequence.Join(_topUiRoot.DOAnchorPos(
            _topUiStartPosition + Vector2.up * (show ? slideDistance : 0f),
            _modeAnimationDuration));
        _modeSequence.Join(_bottomUiTarget.DOAnchorPos(
            _bottomUiStartPosition + Vector2.down * (show ? slideDistance : 0f),
            _modeAnimationDuration));
        _modeSequence.Join(
            _sendModeCanvasGroup.DOFade(show ? 1f : 0f, _modeAnimationDuration));
        _modeSequence.OnComplete(() =>
        {
            _modeSequence = null;
            if (!show)
            {
                _sendModeRoot.SetActive(false);
            }
        });
    }

    private void RefreshLayout()
    {
        RectTransform canvasRect = _canvas.transform as RectTransform;
        if (canvasRect == null) return;

        SafeAreaInsets insets = SafeAreaUtility.GetInsets(canvasRect);
        Vector2 promptPosition = _sendModePrompt.anchoredPosition;
        promptPosition.y = -insets.Top - 80f;
        _sendModePrompt.anchoredPosition = promptPosition;

        if (_cancelButton.transform is RectTransform cancelRect)
        {
            Vector2 cancelPosition = cancelRect.anchoredPosition;
            cancelPosition.y = insets.Bottom + 90f;
            cancelRect.anchoredPosition = cancelPosition;
        }
    }

}
