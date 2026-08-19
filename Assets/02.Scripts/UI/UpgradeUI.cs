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
    private bool _isInitialized;
    private bool _isRefreshingLayout;
    private RectTransform _toggleRectTransform;
    private Tween _moveTween;
    private float _toggleEdgeOffset;
    private float _closedPanelX;
    private float _openPanelX;
    private float _closedToggleX;
    private float _openToggleX;

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
        AddButtonOutline();

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

    private void AddButtonOutline()
    {
        if (_uiButton == null || _uiButton.targetGraphic == null) return;

        Outline outline = _uiButton.targetGraphic.GetComponent<Outline>();
        if (outline == null)
        {
            outline = _uiButton.targetGraphic.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = true;
    }

    private void ViewUI()
    {
        if (!_isToggleInputEnabled) return;

        SetOpened(!_isOpened);
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

        Vector2 rootSize = _rectTransform.rect.size;
        float panelWidth = _panelTarget.rect.width;

        float leftSafeInset = GetCanvasInset(Screen.safeArea.xMin, Screen.width, rootSize.x);
        float bottomSafeInset = GetCanvasInset(Screen.safeArea.yMin, Screen.height, rootSize.y);
        float topSafeInset = GetCanvasInset(
            Screen.height - Screen.safeArea.yMax,
            Screen.height,
            rootSize.y);

        _closedPanelX = -panelWidth;
        _openPanelX = leftSafeInset;
        _closedToggleX = leftSafeInset + _toggleEdgeOffset;
        _openToggleX = leftSafeInset + panelWidth + _toggleEdgeOffset;

        Vector2 panelOffsetMin = _panelTarget.offsetMin;
        Vector2 panelOffsetMax = _panelTarget.offsetMax;
        panelOffsetMin.y = bottomSafeInset;
        panelOffsetMax.y = -topSafeInset;
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
        float toggleX = _isOpened ? _openToggleX : _closedToggleX;

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

    private static float GetCanvasInset(float pixelInset, int screenSize, float canvasSize)
    {
        if (screenSize <= 0 || canvasSize <= 0f) return 0f;

        return Mathf.Max(0f, pixelInset / screenSize * canvasSize);
    }

    public void SetToggleInputEnabled(bool isEnabled)
    {
        _isToggleInputEnabled = isEnabled;
    }

}
