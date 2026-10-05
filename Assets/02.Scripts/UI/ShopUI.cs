using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private RectTransform _panelTarget;
    [SerializeField] private Button _uiButton;
    [SerializeField] private Button _dismissBackdropButton;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private float _movingDuration = 0.5f;
    [Header("Motion")]
    [Tooltip("서랍이 열릴 때의 움직임입니다. OutBack은 열린 자리를 살짝 지나쳤다가 돌아옵니다.")]
    [SerializeField] private Ease _openEase = Ease.OutBack;
    [Tooltip("열릴 때 지나치는 정도입니다. 클수록 더 튑니다.")]
    [SerializeField, Min(0f)] private float _openOvershoot = 1.3f;
    [Tooltip("서랍이 닫힐 때 걸리는 시간입니다. 열 때보다 짧게 두면 빠릿하게 들어갑니다.")]
    [SerializeField, Min(0.05f)] private float _closeDuration = 0.35f;
    [Tooltip("서랍이 닫힐 때의 움직임입니다. InBack은 살짝 뒤로 물러났다가 빠르게 들어갑니다.")]
    [SerializeField] private Ease _closeEase = Ease.InBack;
    [SerializeField, Min(0f)] private float _closeOvershoot = 1f;

    [Tooltip("서랍의 다음 콘텐츠가 준비되기 전까지 손잡이를 숨깁니다.")]
    [SerializeField] private bool _isContentAvailable;

    [Tooltip("서랍이 이 아래에서 시작합니다. 강화하는 동안 포인트가 가려지지 않게 합니다.")]
    [SerializeField] private RectTransform _topBar;

    [Tooltip("손잡이 탭 안의 화살표입니다. 서랍이 열리면 닫는 방향으로 뒤집습니다.")]
    [SerializeField] private RectTransform _toggleArrow;

    private bool _isOpened = false;

    // 공간과 튜토리얼이 정하는 기본값이다. 연출이 잠시 치우는 것과는 별개라,
    // 연출이 끝나면 이 값으로 돌아간다.
    private bool _isToggleInputEnabled = true;
    private bool _isToggleVisible = true;

    // 서랍을 치워 둔 연출들. 하나라도 남아 있으면 치운 채로 둔다.
    private readonly List<object> _standDownOwners = new();
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
    public bool IsToggleVisible => _isToggleVisible;
    private bool IsStandingDown => _standDownOwners.Count > 0;
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
        _isToggleVisible = _isContentAvailable;
        _isToggleInputEnabled = _isContentAvailable;
        _uiButton.interactable = _isContentAvailable;
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
        if (!_isToggleInputEnabled || IsStandingDown) return;

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
        // 씬이 내려가는 중에 다른 UI의 OnDestroy가 스탠드다운을 풀면 이 자리로 온다. 그때는
        // 서랍의 사각형이 이미 파괴돼 있을 수 있고, 어느 쪽이 먼저 정리될지는 정해져 있지
        // 않다. 움직일 대상이 없으면 아무것도 하지 않는다.
        if (_panelTarget == null || _toggleRectTransform == null) return;

        _moveTween?.Kill();
        _moveTween = null;

        float panelX = _isOpened ? _openPanelX : _closedPanelX;
        float toggleX = !_isToggleVisible || IsStandingDown
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

        // 열 때와 닫을 때 움직임을 달리한다. 열 때는 통통 튀며 들어오고, 닫을 때는 살짝 물러났다가 빠르게
        // 들어간다. 패널, 손잡이, 화살표가 같은 곡선을 써야 따로 놀지 않는다.
        float duration = _isOpened ? _movingDuration : _closeDuration;
        Ease ease = _isOpened ? _openEase : _closeEase;
        float overshoot = _isOpened ? _openOvershoot : _closeOvershoot;
        Sequence sequence = DOTween.Sequence();
        sequence.Join(_panelTarget.DOAnchorPosX(panelX, duration).SetEase(ease, overshoot));
        sequence.Join(_toggleRectTransform.DOAnchorPosX(toggleX, duration).SetEase(ease, overshoot));
        if (_toggleArrow != null)
        {
            sequence.Join(_toggleArrow.DOScaleY(arrowScaleY, duration).SetEase(ease, overshoot));
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
        _isToggleInputEnabled = _isContentAvailable && isEnabled;
        ApplyToggleInput();
    }

    // 연출이 서랍을 잠시 치운다. 소유자별로 쌓고 하나라도 남아 있으면 치운 채로
    // 둔다. HudVisibility의 숨김 요청, Clicker의 입력 모드와 같은 방식이다.
    //
    // 연출마다 들어올 때의 값을 따로 기억하면, 둘이 겹쳤을 때 나중에 끝난 쪽이
    // 먼저 끝난 쪽이 기억한 값을 되살린다. 기억하는 곳이 하나면 그럴 수 없다.
    public void PushStandDown(object owner)
    {
        if (owner == null || _standDownOwners.Contains(owner)) return;

        _standDownOwners.Add(owner);
        TryClose();
        ApplyStandDown(animated: true);
    }

    // 연출 없이 즉시 되돌려야 하는 정리 경로에서는 animated를 끈다.
    public void ReleaseStandDown(object owner, bool animated = true)
    {
        if (owner == null || !_standDownOwners.Remove(owner)) return;

        ApplyStandDown(animated);
    }

    private void ApplyStandDown(bool animated)
    {
        ApplyToggleInput();

        if (_isInitialized)
        {
            MoveDrawer(animated);
        }
    }

    private void ApplyToggleInput()
    {
        if (_uiButton == null) return;

        _uiButton.interactable = _isToggleInputEnabled && !IsStandingDown;
    }

    // 서랍을 열어 줄지는 진행도가 정한다. 처음에는 손잡이를 치워 두었다가 상점이 열리는 순간 불러낸다.
    // 입력 가능 여부는 공간과 튜토리얼이 정하므로 여기서는 켜 두기만 하고, 호출부가 그 규칙을
    // 다시 적용하게 한다(GameplaySpaceManager.RefreshInteraction).
    public void SetContentAvailable(bool isAvailable, bool animated = true)
    {
        if (_isContentAvailable == isAvailable) return;

        _isContentAvailable = isAvailable;
        // Start가 이 값으로 처음 상태를 정하므로 아직 시작 전이면 값만 바꿔 둔다.
        if (!_isInitialized) return;

        _isToggleInputEnabled = isAvailable;
        _isToggleVisible = isAvailable;
        if (!isAvailable && _isOpened)
        {
            SetOpened(false);
            return;
        }

        ApplyToggleInput();
        MoveDrawer(animated);
    }

    public void SetToggleVisible(bool isVisible, bool animated = true)
    {
        isVisible = _isContentAvailable && isVisible;
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
