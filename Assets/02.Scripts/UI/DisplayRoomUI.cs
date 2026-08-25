using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DisplayRoomUI : MonoBehaviour
{
    [Header("Common")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Button _panelSwitchButton;
    [SerializeField] private RectTransform _stageMovePanel;
    [SerializeField] private Button _spaceButton;
    [SerializeField] private Button _sendButton;
    [SerializeField] private StageUI _stageUI;
    [SerializeField] private GameExitManager _gameExitManager;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private UpgradeUI _upgradeUI;
    [SerializeField] private RectTransform _systemUpgradePanel;

    [Header("Send Mode")]
    [SerializeField] private GameObject _sendModeRoot;
    [SerializeField] private CanvasGroup _sendModeCanvasGroup;
    [SerializeField] private RectTransform _sendModePrompt;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private GameObject _warningRoot;
    [SerializeField] private CanvasGroup _warningCanvasGroup;
    [SerializeField] private TextMeshProUGUI _warningText;
    [SerializeField] private RectTransform[] _topUiTargets = Array.Empty<RectTransform>();
    [SerializeField] private RectTransform[] _bottomUiTargets = Array.Empty<RectTransform>();

    [Header("Animation")]
    [SerializeField, Min(0f)] private float _buttonMargin = 50f;
    [SerializeField, Min(0f)] private float _panelAnimationDuration = 0.25f;
    [SerializeField, Min(0f)] private float _modeAnimationDuration = 0.35f;
    [SerializeField, Min(0f)] private float _warningDisplayDuration = 1.2f;

    private Vector2[] _topUiPositions = Array.Empty<Vector2>();
    private Vector2[] _bottomUiPositions = Array.Empty<Vector2>();
    private Sequence _modeSequence;
    private Sequence _panelSequence;
    private Sequence _warningSequence;
    private Vector2 _systemUpgradeStartPosition;
    private Vector2 _stageMoveStartPosition;
    private bool _isSendMode;
    private bool _isStageMovePanelSelected;
    private bool _isTransferPlaying;
    private Vector3 _transferStartPosition;

    public RectTransform PanelSwitchButtonTarget => _panelSwitchButton != null
        ? _panelSwitchButton.transform as RectTransform
        : null;
    public RectTransform SpaceButtonTarget => _spaceButton != null
        ? _spaceButton.transform as RectTransform
        : null;
    public RectTransform SendButtonTarget => _sendButton != null
        ? _sendButton.transform as RectTransform
        : null;
    public bool IsMovePanelOpen => _stageMovePanel != null &&
                                   _stageMovePanel.gameObject.activeSelf;

    public event Action MovePanelOpened;
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
        _systemUpgradeStartPosition = _systemUpgradePanel.anchoredPosition;
        _stageMoveStartPosition = _stageMovePanel.anchoredPosition;
        _sendModeRoot.SetActive(false);
        _warningRoot.SetActive(false);

        _panelSwitchButton.onClick.AddListener(ToggleBottomPanel);
        _spaceButton.onClick.AddListener(OnSpaceButtonClicked);
        _sendButton.onClick.AddListener(BeginSendMode);
        _cancelButton.onClick.AddListener(CancelSendMode);
        _clicker.TargetClicked += OnTargetClicked;
        StageManager.Instance.SpaceChanged += OnSpaceChanged;
        StageManager.Instance.StageTransitionCompleted += OnStageTransitionCompleted;
        GameManager.OnAllDataInitialized += Refresh;
        GameManager.Instance.OnGameplayActivated += Refresh;
        SlimeManager.OnHighestGradeChanged += OnHighestGradeChanged;

        RefreshLayout();
        _isStageMovePanelSelected =
            StageManager.Instance.CurrentSpace == EGameplaySpace.DisplayRoom;
        ApplySpacePresentation(
            StageManager.Instance.CurrentSpace == EGameplaySpace.DisplayRoom,
            animated: false);
        Refresh();
    }

    private void OnDestroy()
    {
        _modeSequence?.Kill();
        _panelSequence?.Kill();
        _warningSequence?.Kill();
        _panelSwitchButton?.onClick.RemoveListener(ToggleBottomPanel);
        _spaceButton?.onClick.RemoveListener(OnSpaceButtonClicked);
        _sendButton?.onClick.RemoveListener(BeginSendMode);
        _cancelButton?.onClick.RemoveListener(CancelSendMode);

        if (_clicker != null)
        {
            _clicker.TargetClicked -= OnTargetClicked;
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
                             _panelSwitchButton != null &&
                             _stageMovePanel != null &&
                             _spaceButton != null &&
                             _sendButton != null &&
                             _stageUI != null &&
                             _gameExitManager != null &&
                             _clicker != null &&
                             _upgradeUI != null &&
                             _systemUpgradePanel != null &&
                             _sendModeRoot != null &&
                             _sendModeCanvasGroup != null &&
                             _sendModePrompt != null &&
                             _cancelButton != null &&
                             _warningRoot != null &&
                             _warningCanvasGroup != null &&
                             _warningText != null &&
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
        if (_panelSwitchButton != null &&
            _canvas != null)
        {
            RefreshLayout();
        }
    }

    private void ToggleBottomPanel()
    {
        StageManager stageManager = StageManager.Instance;
        if (_isSendMode ||
            stageManager == null ||
            !stageManager.IsMainStageActive ||
            !IsDisplayRoomUnlocked())
        {
            return;
        }

        _isStageMovePanelSelected = !_isStageMovePanelSelected;
        PlayBottomPanelTransition(_isStageMovePanelSelected);
    }

    private void PlayBottomPanelTransition(bool showMovePanel)
    {
        _panelSequence?.Kill();
        _panelSequence = null;

        RectTransform outgoing = showMovePanel
            ? _systemUpgradePanel
            : _stageMovePanel;
        RectTransform incoming = showMovePanel
            ? _stageMovePanel
            : _systemUpgradePanel;
        Vector2 outgoingBase = showMovePanel
            ? _systemUpgradeStartPosition
            : _stageMoveStartPosition;
        Vector2 incomingBase = showMovePanel
            ? _stageMoveStartPosition
            : _systemUpgradeStartPosition;
        float distance = Mathf.Max(
            _systemUpgradePanel.rect.height,
            _stageMovePanel.rect.height);
        _panelSwitchButton.interactable = false;
        outgoing.gameObject.SetActive(true);
        incoming.gameObject.SetActive(true);
        outgoing.anchoredPosition = outgoingBase;
        incoming.anchoredPosition = incomingBase + Vector2.down * distance;

        // 이동 패널이 빠져나가는 동안에도 자식 버튼은 함께 보여야 한다.
        _stageUI.SetMenuPresentation(true);
        _stageUI.SetButtonInteractable(false);
        _spaceButton.gameObject.SetActive(true);
        _spaceButton.interactable = false;
        _sendButton.gameObject.SetActive(true);
        _sendButton.interactable = false;

        _panelSequence = DOTween.Sequence()
            .Join(outgoing.DOAnchorPos(
                outgoingBase + Vector2.down * distance,
                _panelAnimationDuration).SetEase(Ease.InOutCubic))
            .Join(incoming.DOAnchorPos(
                incomingBase,
                _panelAnimationDuration).SetEase(Ease.InOutCubic))
            .OnComplete(() =>
            {
                _panelSequence = null;
                outgoing.gameObject.SetActive(false);
                outgoing.anchoredPosition = outgoingBase;
                incoming.anchoredPosition = incomingBase;
                _panelSwitchButton.interactable = true;
                ApplyBottomPanelState();
                if (showMovePanel)
                {
                    MovePanelOpened?.Invoke();
                }
            });
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
        _upgradeUI.SetToggleInputEnabled(false);
        _isSendMode = true;
        _sendModeRoot.SetActive(true);
        _sendModeRoot.transform.SetAsLastSibling();
        _sendModeCanvasGroup.alpha = 0f;
        _warningRoot.SetActive(false);
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
        _warningSequence?.Kill();
        _warningRoot.SetActive(false);
        _gameExitManager.UnregisterBackHandler(this);
        StageManager.Instance?.RefreshInteraction();
        PlayModePresentation(show: false);
        ApplyBottomPanelState();
        Refresh();
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
            ShowWarning("같은 종류의 슬라임이 이미 장식장에 있어요.");
            return;
        }

        _isTransferPlaying = true;
        _transferStartPosition = target.transform.position;
        _clicker.SetInputMode(false, false);
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
            ShowWarning("이 슬라임은 장식장으로 보낼 수 없어요.");
        }
    }

    private void ApplySendModeInput()
    {
        _clicker.SetInputMode(
            clickEnabled: true,
            dragEnabled: false,
            invokeClickAction: false);
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
        _isStageMovePanelSelected = isDisplayRoom;
        ApplySpacePresentation(isDisplayRoom, animated: true);
        RefreshLayout();

        RefreshBackHandler();

        Refresh();
    }

    private void ApplySpacePresentation(bool isDisplayRoom, bool animated)
    {
        _upgradeUI.SetToggleVisible(!isDisplayRoom, animated);
        _systemUpgradePanel.anchoredPosition = _systemUpgradeStartPosition;
        ApplyBottomPanelState();
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
        if (_panelSwitchButton == null ||
            _stageMovePanel == null ||
            _spaceButton == null ||
            _sendButton == null ||
            _stageUI == null)
        {
            return;
        }

        StageManager stageManager = StageManager.Instance;
        bool isDisplayRoom = stageManager != null &&
                             !stageManager.IsMainStageActive;
        if (!isDisplayRoom && !IsDisplayRoomUnlocked())
        {
            _isStageMovePanelSelected = false;
        }

        ApplyBottomPanelState();
    }

    private bool IsDisplayRoomUnlocked()
    {
        return GameManager.Instance != null &&
               GameManager.Instance.IsAllDataInitialized &&
               GameManager.Instance.IsGameplayActive &&
               SlimeManager.Instance != null &&
               SlimeManager.Instance.IsDisplayRoomUnlocked;
    }

    private void ApplyBottomPanelState()
    {
        _panelSequence?.Kill();
        _panelSequence = null;

        StageManager stageManager = StageManager.Instance;
        bool isDisplayRoom = stageManager != null &&
                             !stageManager.IsMainStageActive;
        bool isUnlocked = IsDisplayRoomUnlocked();
        bool showMovePanel = !_isSendMode &&
                             (isDisplayRoom ||
                              (isUnlocked && _isStageMovePanelSelected));
        bool showSystemPanel = !_isSendMode &&
                               !isDisplayRoom &&
                               !showMovePanel;

        _panelSwitchButton.gameObject.SetActive(
            !_isSendMode &&
            !isDisplayRoom &&
            isUnlocked);
        _panelSwitchButton.interactable = true;
        _systemUpgradePanel.anchoredPosition = _systemUpgradeStartPosition;
        _stageMovePanel.anchoredPosition = _stageMoveStartPosition;
        _systemUpgradePanel.gameObject.SetActive(showSystemPanel);
        _stageMovePanel.gameObject.SetActive(showMovePanel);
        _stageUI.SetMenuPresentation(showMovePanel && !isDisplayRoom);
        _stageUI.SetButtonInteractable(showMovePanel && !isDisplayRoom);
        _spaceButton.gameObject.SetActive(showMovePanel);
        _spaceButton.interactable = showMovePanel;
        _sendButton.gameObject.SetActive(showMovePanel && !isDisplayRoom);
        _sendButton.interactable = showMovePanel && !isDisplayRoom;
    }

    // 장식장 토스트는 DisplayRoomInfoUI의 꺼내기 실패 안내도 함께 쓴다.
    public void ShowWarning(string message)
    {
        _warningSequence?.Kill();
        _warningText.text = message;
        _warningRoot.SetActive(true);
        _warningRoot.transform.SetAsLastSibling();
        _warningCanvasGroup.alpha = 0f;
        _warningSequence = DOTween.Sequence()
            .Append(_warningCanvasGroup.DOFade(1f, 0.15f))
            .AppendInterval(_warningDisplayDuration)
            .Append(_warningCanvasGroup.DOFade(0f, 0.2f))
            .OnComplete(() =>
            {
                _warningSequence = null;
                _warningRoot.SetActive(false);
            });
    }

    private void CacheUiPositions()
    {
        _topUiPositions = CachePositions(_topUiTargets);
        _bottomUiPositions = CachePositions(_bottomUiTargets);
    }

    private static Vector2[] CachePositions(RectTransform[] targets)
    {
        var positions = new Vector2[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                positions[i] = targets[i].anchoredPosition;
            }
        }

        return positions;
    }

    private void PlayModePresentation(bool show)
    {
        _modeSequence?.Kill();
        _modeSequence = DOTween.Sequence();
        float slideDistance = (_canvas.transform as RectTransform)?.rect.height ?? 1920f;

        AppendUiMoves(
            _modeSequence,
            _topUiTargets,
            _topUiPositions,
            show ? slideDistance : 0f);
        AppendUiMoves(
            _modeSequence,
            _bottomUiTargets,
            _bottomUiPositions,
            show ? -slideDistance : 0f);
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

    private void AppendUiMoves(
        Sequence sequence,
        RectTransform[] targets,
        Vector2[] basePositions,
        float yOffset)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            RectTransform target = targets[i];
            if (target == null) continue;

            Vector2 destination = basePositions[i] + Vector2.up * yOffset;
            sequence.Join(target.DOAnchorPos(destination, _modeAnimationDuration));
        }
    }

    private void RefreshLayout()
    {
        RectTransform canvasRect = _canvas.transform as RectTransform;
        RectTransform panelSwitchRect = _panelSwitchButton.transform as RectTransform;
        if (canvasRect == null ||
            panelSwitchRect == null)
        {
            return;
        }

        SafeAreaInsets insets = SafeAreaUtility.GetInsets(canvasRect);
        float panelTop = _systemUpgradeStartPosition.y +
                         _systemUpgradePanel.rect.height *
                         (1f - _systemUpgradePanel.pivot.y);
        panelSwitchRect.anchoredPosition = new Vector2(
            insets.Left + _buttonMargin,
            panelTop + _buttonMargin);

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
