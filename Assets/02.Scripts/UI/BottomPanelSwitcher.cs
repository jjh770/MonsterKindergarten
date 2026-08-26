using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 하단 HUD의 두 패널(시스템 업그레이드 / 스테이지 이동)을 슬라이드로 교체한다.
//
// 어떤 상황인지는 ApplyContext로 외부가 알려준다. 이 컴포넌트는 장식장이나
// 스테이지 상태를 직접 조회하지 않는다. 그래야 하늘 인트로처럼 장식장과
// 무관한 기능이 패널 상태를 알려고 DisplayRoomUI를 참조하지 않아도 된다.
public sealed class BottomPanelSwitcher : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Button _switchButton;
    [SerializeField] private RectTransform _systemUpgradePanel;
    [SerializeField] private RectTransform _movePanel;
    [SerializeField] private CanvasGroup _movePanelGroup;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float _animationDuration = 0.25f;
    [SerializeField, Min(0f)] private float _buttonMargin = 20f;

    private Sequence _sequence;
    private Vector2 _systemUpgradeStartPosition;
    private Vector2 _movePanelStartPosition;
    private bool _isMovePanelSelected;
    private bool _isAreaVisible = true;
    private bool _isForcedMovePanel;
    private bool _canSwitch;
    private bool _isInitialized;

    public RectTransform SwitchButtonTarget => _switchButton != null
        ? _switchButton.transform as RectTransform
        : null;
    public bool IsMovePanelOpen => _movePanel != null &&
                                   _movePanel.gameObject.activeSelf;

    // 이동 패널이 완전히 열린 순간. 튜토리얼과 하늘 인트로가 이 시점을 기다린다.
    public event Action MovePanelOpened;

    // 이동 패널의 자식 버튼을 노출해야 하는지. 슬라이드 중에는 항상 true다.
    // 자식 버튼의 주인들이 각자 이 신호를 받아 자기 버튼만 처리한다.
    public event Action<bool> MovePanelPresentationChanged;

    // 시작 위치는 다른 컴포넌트의 Start보다 먼저 확정되어야 한다.
    private void Awake()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        _systemUpgradeStartPosition = _systemUpgradePanel.anchoredPosition;
        _movePanelStartPosition = _movePanel.anchoredPosition;
        _isInitialized = true;
    }

    private void Start()
    {
        if (!_isInitialized) return;

        _switchButton.onClick.AddListener(Toggle);
        RefreshLayout();
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
        _switchButton?.onClick.RemoveListener(Toggle);
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_isInitialized)
        {
            RefreshLayout();
        }
    }

    private bool HasRequiredReferences()
    {
        bool hasReferences = _canvas != null &&
                             _switchButton != null &&
                             _systemUpgradePanel != null &&
                             _movePanel != null &&
                             _movePanelGroup != null;
        if (!hasReferences)
        {
            Debug.LogError("하단 패널 전환의 필수 참조가 비어 있습니다.", this);
        }

        return hasReferences;
    }

    // 패널 영역이 보이는지, 이동 패널이 강제되는지, 전환이 허용되는지를 받는다.
    public void ApplyContext(bool isAreaVisible, bool forceMovePanel, bool canSwitch)
    {
        if (!_isInitialized) return;

        _isAreaVisible = isAreaVisible;
        _isForcedMovePanel = forceMovePanel;
        _canSwitch = canSwitch;

        // 전환이 막힌 상태에서는 선택을 유지할 근거가 없다.
        if (!canSwitch && !forceMovePanel)
        {
            _isMovePanelSelected = false;
        }

        ApplyState();
    }

    // 공간이 바뀔 때 선택 상태를 한 번에 맞춘다.
    public void ResetSelection(bool selectMovePanel)
    {
        _isMovePanelSelected = selectMovePanel;
    }

    public void RefreshLayout()
    {
        RectTransform canvasRect = _canvas.transform as RectTransform;
        RectTransform switchRect = _switchButton.transform as RectTransform;
        if (canvasRect == null || switchRect == null) return;

        SafeAreaInsets insets = SafeAreaUtility.GetInsets(canvasRect);
        float panelTop = _systemUpgradeStartPosition.y +
                         _systemUpgradePanel.rect.height *
                         (1f - _systemUpgradePanel.pivot.y);
        switchRect.anchoredPosition = new Vector2(
            insets.Left + _buttonMargin,
            panelTop + _buttonMargin);
    }

    private void Toggle()
    {
        if (!_isInitialized ||
            !_isAreaVisible ||
            _isForcedMovePanel ||
            !_canSwitch)
        {
            return;
        }

        _isMovePanelSelected = !_isMovePanelSelected;
        PlayTransition(_isMovePanelSelected);
    }

    private void PlayTransition(bool showMovePanel)
    {
        _sequence?.Kill();
        _sequence = null;

        RectTransform outgoing = showMovePanel ? _systemUpgradePanel : _movePanel;
        RectTransform incoming = showMovePanel ? _movePanel : _systemUpgradePanel;
        Vector2 outgoingBase = showMovePanel
            ? _systemUpgradeStartPosition
            : _movePanelStartPosition;
        Vector2 incomingBase = showMovePanel
            ? _movePanelStartPosition
            : _systemUpgradeStartPosition;
        float distance = Mathf.Max(
            _systemUpgradePanel.rect.height,
            _movePanel.rect.height);

        _switchButton.interactable = false;
        outgoing.gameObject.SetActive(true);
        incoming.gameObject.SetActive(true);
        outgoing.anchoredPosition = outgoingBase;
        incoming.anchoredPosition = incomingBase + Vector2.down * distance;

        // 슬라이드 동안에는 패널 단위로 입력만 잠근다.
        // 자식 버튼을 하나씩 끄면 주인이 다른 버튼까지 여기서 건드리게 된다.
        _movePanelGroup.interactable = false;

        // 이동 패널이 빠져나가는 동안에도 자식 버튼은 함께 보여야 한다.
        MovePanelPresentationChanged?.Invoke(true);

        _sequence = DOTween.Sequence()
            .Join(outgoing.DOAnchorPos(
                outgoingBase + Vector2.down * distance,
                _animationDuration).SetEase(Ease.InOutCubic))
            .Join(incoming.DOAnchorPos(
                incomingBase,
                _animationDuration).SetEase(Ease.InOutCubic))
            .OnComplete(() =>
            {
                _sequence = null;
                outgoing.gameObject.SetActive(false);
                outgoing.anchoredPosition = outgoingBase;
                incoming.anchoredPosition = incomingBase;
                ApplyState();
                if (showMovePanel)
                {
                    MovePanelOpened?.Invoke();
                }
            });
    }

    private void ApplyState()
    {
        _sequence?.Kill();
        _sequence = null;

        bool showMovePanel = _isAreaVisible &&
                             (_isForcedMovePanel ||
                              (_canSwitch && _isMovePanelSelected));
        bool showSystemPanel = _isAreaVisible &&
                               !_isForcedMovePanel &&
                               !showMovePanel;

        _switchButton.gameObject.SetActive(
            _isAreaVisible && !_isForcedMovePanel && _canSwitch);
        _switchButton.interactable = true;
        _systemUpgradePanel.anchoredPosition = _systemUpgradeStartPosition;
        _movePanel.anchoredPosition = _movePanelStartPosition;
        _systemUpgradePanel.gameObject.SetActive(showSystemPanel);
        _movePanel.gameObject.SetActive(showMovePanel);
        _movePanelGroup.interactable = true;

        MovePanelPresentationChanged?.Invoke(showMovePanel);
    }
}
