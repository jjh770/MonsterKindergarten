using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeUI : MonoBehaviour
{
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private RectTransform _panelTarget;
    [SerializeField] private Button _uiButton;
    [SerializeField] private GameObject _doNotTouchPanel;
    [SerializeField] private float _movingDuration = 0.5f;

    private bool _isOpened = false;
    private bool _isToggleInputEnabled = true;
    private bool _isToggleVisible = true;
    private bool _isInitialized;
    private bool _isRefreshingLayout;
    private RectTransform _toggleRectTransform;
    private Tween _moveTween;
    private float _toggleEdgeOffset;
    private float _closedPanelX;
    private float _openPanelX;
    private float _closedToggleX;
    private float _openToggleX;
    private float _hiddenToggleX;

    public RectTransform ToggleTarget => _uiButton?.transform as RectTransform;
    public RectTransform PanelTarget => _panelTarget;
    public event System.Action Opened;
    public event System.Action Closed;

    private void Start()
    {
        if (_rectTransform == null ||
            _panelTarget == null ||
            _uiButton == null ||
            _doNotTouchPanel == null)
        {
            Debug.LogError("업그레이드 UI의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _toggleRectTransform = _uiButton.transform as RectTransform;
        if (_toggleRectTransform == null)
        {
            Debug.LogError("업그레이드 버튼에 RectTransform이 없습니다.", this);
            enabled = false;
            return;
        }

        _toggleEdgeOffset = _toggleRectTransform.anchoredPosition.x;
        _uiButton.onClick.AddListener(ViewUI);
        _doNotTouchPanel.SetActive(false);

        _isInitialized = true;
        RefreshLayout();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_isInitialized)
        {
            RefreshLayout();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && _isInitialized)
        {
            RefreshLayout();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && _isInitialized)
        {
            RefreshLayout();
        }
    }

    private void OnDisable()
    {
        _moveTween?.Kill();
        _moveTween = null;
    }

    private void OnEnable()
    {
        if (_isInitialized)
        {
            RefreshLayout();
        }
    }

    private void OnDestroy()
    {
        _uiButton?.onClick.RemoveListener(ViewUI);
    }

    private void ViewUI()
    {
        if (!_isToggleInputEnabled) return;

        SetOpened(!_isOpened);
    }

    public bool TryClose()
    {
        if (!_isOpened) return false;

        SetOpened(false);
        return true;
    }

    private void SetOpened(bool isOpened)
    {
        if (_isOpened == isOpened) return;

        _isOpened = isOpened;
        _doNotTouchPanel.SetActive(_isOpened);
        MoveDrawer(animated: true);

        if (_isOpened) Opened?.Invoke();
        else Closed?.Invoke();
    }

    private void RefreshLayout()
    {
        if (_isRefreshingLayout) return;

        _isRefreshingLayout = true;
        Canvas.ForceUpdateCanvases();

        float panelWidth = _panelTarget.rect.width;
        SafeAreaInsets insets = SafeAreaUtility.GetInsets(_rectTransform);

        _closedPanelX = -panelWidth;
        _openPanelX = insets.Left;
        _closedToggleX = insets.Left + _toggleEdgeOffset;
        _openToggleX = insets.Left + panelWidth + _toggleEdgeOffset;
        _hiddenToggleX = -_toggleRectTransform.rect.width;

        Vector2 panelOffsetMin = _panelTarget.offsetMin;
        Vector2 panelOffsetMax = _panelTarget.offsetMax;
        panelOffsetMin.y = insets.Bottom;
        panelOffsetMax.y = -insets.Top;
        _panelTarget.offsetMin = panelOffsetMin;
        _panelTarget.offsetMax = panelOffsetMax;

        MoveDrawer(animated: false);
        _isRefreshingLayout = false;
    }

    private void MoveDrawer(bool animated)
    {
        _moveTween?.Kill();
        _moveTween = null;

        float panelX = _isOpened ? _openPanelX : _closedPanelX;
        float toggleX = !_isToggleVisible
            ? _hiddenToggleX
            : _isOpened
                ? _openToggleX
                : _closedToggleX;

        if (!animated)
        {
            SetAnchoredPositionX(_panelTarget, panelX);
            SetAnchoredPositionX(_toggleRectTransform, toggleX);
            return;
        }

        Sequence sequence = DOTween.Sequence();
        sequence.Join(_panelTarget.DOAnchorPosX(panelX, _movingDuration));
        sequence.Join(_toggleRectTransform.DOAnchorPosX(toggleX, _movingDuration));
        _moveTween = sequence.OnComplete(() => _moveTween = null);
    }

    private static void SetAnchoredPositionX(RectTransform target, float x)
    {
        Vector2 position = target.anchoredPosition;
        position.x = x;
        target.anchoredPosition = position;
    }

    public void SetToggleInputEnabled(bool isEnabled)
    {
        _isToggleInputEnabled = isEnabled;

        if (_uiButton != null)
        {
            _uiButton.interactable = isEnabled;
        }
    }

    public void SetToggleVisible(bool isVisible, bool animated = true)
    {
        if (_isToggleVisible == isVisible) return;

        _isToggleVisible = isVisible;
        if (!isVisible && _isOpened)
        {
            SetOpened(false);
            return;
        }

        if (_isInitialized)
        {
            MoveDrawer(animated);
        }
    }

}
