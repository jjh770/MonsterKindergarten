using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeUI : MonoBehaviour
{
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private RectTransform _panelTarget;
    [SerializeField] private Button _uiButton;
    [SerializeField] private Button _dismissBackdropButton;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private float _movingDuration = 0.5f;

    [Tooltip("서랍이 이 아래에서 시작합니다. 강화하는 동안 포인트가 가려지지 않게 합니다.")]
    [SerializeField] private RectTransform _topBar;

    [Tooltip("손잡이 탭 안의 화살표입니다. 서랍이 열리면 닫는 방향으로 뒤집습니다.")]
    [SerializeField] private RectTransform _toggleArrow;

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
    public bool IsToggleInputEnabled => _isToggleInputEnabled;
    public event System.Action Opened;
    public event System.Action Closed;

    private void Start()
    {
        if (_rectTransform == null ||
            _panelTarget == null ||
            _uiButton == null ||
            _dismissBackdropButton == null ||
            _clicker == null)
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
        _dismissBackdropButton.onClick.AddListener(CloseFromBackdrop);
        _dismissBackdropButton.gameObject.SetActive(false);

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
        _clicker?.ReleaseMode(this);
    }

    private void OnEnable()
    {
        if (_isInitialized)
        {
            if (_isOpened)
            {
                _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Modal);
            }

            RefreshLayout();
        }
    }

    private void OnDestroy()
    {
        _uiButton?.onClick.RemoveListener(ViewUI);
        _dismissBackdropButton?.onClick.RemoveListener(CloseFromBackdrop);
        _clicker?.ReleaseMode(this);
    }

    private void ViewUI()
    {
        if (!_isToggleInputEnabled) return;

        SetOpened(!_isOpened);
    }

    private void CloseFromBackdrop()
    {
        TryClose();
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
        _dismissBackdropButton.gameObject.SetActive(_isOpened);
        if (_isOpened)
        {
            _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Modal);
        }
        else
        {
            _clicker.ReleaseMode(this);
        }

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
        panelOffsetMax.y = -Mathf.Max(insets.Top, GetTopBarReservedHeight());
        _panelTarget.offsetMin = panelOffsetMin;
        _panelTarget.offsetMax = panelOffsetMax;

        MoveDrawer(animated: false);
        _isRefreshingLayout = false;
    }

    // 서랍 부모의 위 끝에서 상단 바 아래 끝까지의 거리. 상단 바는 다른 캔버스에
    // 있지만 둘 다 오버레이라 월드 좌표가 화면 좌표로 같다.
    //
    // HudVisibility가 상단 바를 화면 밖으로 올린 동안 계산되면 이 값이 세이프
    // 에어리어보다 작아지므로, 호출부가 둘 중 큰 값을 쓴다.
    private float GetTopBarReservedHeight()
    {
        RectTransform parent = _panelTarget.parent as RectTransform;
        if (_topBar == null || parent == null) return 0f;

        Vector3[] corners = new Vector3[4];
        _topBar.GetWorldCorners(corners);
        float topBarBottom = parent.InverseTransformPoint(corners[0]).y;
        return parent.rect.yMax - topBarBottom;
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

        // 화살표는 여는 방향(오른쪽)을 가리킨다. 열린 뒤에도 그대로면 한 번 더 누르면
        // 닫힌다는 것을 알 수 없으므로 좌우로 뒤집는다. 탭 전체를 뒤집으면 평평한 면이
        // 패널 반대쪽으로 가므로 화살표만 뒤집는다.
        //
        // 위를 가리키는 삼각형 그림을 씬에서 270도 돌려 쓰므로 화면의 좌우는 화살표의
        // y축이다. x를 뒤집으면 위아래가 바뀌어 대칭인 삼각형은 그대로 보인다.
        float arrowScaleY = _isOpened ? -1f : 1f;

        if (!animated)
        {
            SetAnchoredPositionX(_panelTarget, panelX);
            SetAnchoredPositionX(_toggleRectTransform, toggleX);
            if (_toggleArrow != null)
            {
                SetScaleY(_toggleArrow, arrowScaleY);
            }

            return;
        }

        Sequence sequence = DOTween.Sequence();
        sequence.Join(_panelTarget.DOAnchorPosX(panelX, _movingDuration));
        sequence.Join(_toggleRectTransform.DOAnchorPosX(toggleX, _movingDuration));
        if (_toggleArrow != null)
        {
            sequence.Join(_toggleArrow.DOScaleY(arrowScaleY, _movingDuration));
        }
        _moveTween = sequence.OnComplete(() => _moveTween = null);
    }

    private static void SetAnchoredPositionX(RectTransform target, float x)
    {
        Vector2 position = target.anchoredPosition;
        position.x = x;
        target.anchoredPosition = position;
    }

    private static void SetScaleY(RectTransform target, float y)
    {
        Vector3 scale = target.localScale;
        scale.y = y;
        target.localScale = scale;
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
