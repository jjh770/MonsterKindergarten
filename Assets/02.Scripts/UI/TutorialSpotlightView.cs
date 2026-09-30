using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum SpotlightInteractionMode
{
    BlockAll,
    PassThroughPrimary,
    AdvanceOnPrimaryTap,
}

public sealed class TutorialSpotlightView : MonoBehaviour, ICanvasRaycastFilter, IPointerClickHandler
{
    private static readonly int HoleCenterId = Shader.PropertyToID("_HoleCenter");
    private static readonly int HoleSizeId = Shader.PropertyToID("_HoleSize");
    private static readonly int SecondHoleCenterId = Shader.PropertyToID("_SecondHoleCenter");
    private static readonly int SecondHoleSizeId = Shader.PropertyToID("_SecondHoleSize");
    private static readonly int SecondHoleEnabledId = Shader.PropertyToID("_SecondHoleEnabled");
    private static readonly int HoleShapeId = Shader.PropertyToID("_HoleShape");
    private static readonly int SecondHoleShapeId = Shader.PropertyToID("_SecondHoleShape");
    private static readonly int RootSizeId = Shader.PropertyToID("_RootSize");

    [Header("References")]
    [SerializeField] private Image _overlayImage;
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private RectTransform _messageRect;
    [SerializeField] private RectTransform _arrowRect;
    [Tooltip("화살표 그림입니다. 원본이 아래를 가리키는 그림이며, 위아래로 까딱이는 것은 이 오브젝트입니다.")]
    [SerializeField] private RectTransform _arrowImageRect;

    [Header("Spotlight")]
    [SerializeField] private Vector2 _holeSize = new Vector2(320f, 320f);
    [SerializeField] private Vector2 _uiHolePadding = new Vector2(40f, 30f);
    [SerializeField] private Vector2 _messageOffset = new Vector2(0f, 240f);
    [SerializeField] private Vector2 _arrowOffset = new Vector2(0f, 135f);
    [Header("Arrow")]
    [Tooltip("화살표 끝과 구멍 가장자리 사이에 남길 간격입니다. 까딱이는 폭을 뺀 값입니다.")]
    [SerializeField, Min(0f)] private float _arrowGap = 12f;
    [Tooltip("화살표가 구멍 쪽으로 다가가는 거리입니다. 0이면 움직이지 않습니다.")]
    [SerializeField, Min(0f)] private float _arrowBobDistance = 18f;
    [Tooltip("한 번 다가가는 데 걸리는 시간(초)입니다. 갔다 오는 데 두 배가 걸립니다.")]
    [SerializeField, Min(0.05f)] private float _arrowBobDuration = 0.45f;

    [SerializeField, Min(0f)] private float _messageScreenMargin = 30f;
    [SerializeField, Min(0f)] private float _compactMessageWidth = 240f;

    [Tooltip("안내창 안에서 글자 둘레에 두는 여백의 합(가로, 세로)입니다.")]
    [SerializeField] private Vector2 _messagePadding = new Vector2(100f, 60f);

    private RectTransform _rootRect;
    private Canvas _canvas;
    private Camera _worldCamera;
    private Material _runtimeMaterial;
    private Tween _arrowBobTween;
    private Transform _worldTarget;
    private Transform _secondaryWorldTarget;
    private RectTransform _uiTarget;
    private RectTransform _secondaryUiTarget;
    private Vector2 _holeCenter;
    private Vector2 _currentHoleSize;
    private Vector2 _passThroughHoleSize;
    private Vector2 _secondHoleCenter;
    private Vector2 _secondHoleSize;
    private bool _hasSecondHole;
    private SpotlightInteractionMode _interactionMode;
    private bool _centerCalloutBetweenTargets;
    private bool _useRectangularHole;
    private bool _useRectangularSecondHole;
    private Color _defaultOverlayColor;
    private Vector2 _defaultMessageSize;
    private float _messageMaxWidth;

    public event Action AdvanceRequested;

    private void Awake()
    {
        _rootRect = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();
        _worldCamera = Camera.main;

        if (_overlayImage == null || _messageText == null ||
            _messageRect == null || _arrowRect == null || _arrowImageRect == null)
        {
            Debug.LogError("튜토리얼 스포트라이트 프리팹의 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _runtimeMaterial = Instantiate(_overlayImage.material);
        _overlayImage.material = _runtimeMaterial;
        _defaultOverlayColor = _overlayImage.color;
        _defaultMessageSize = _messageRect.sizeDelta;
        _messageMaxWidth = _defaultMessageSize.x;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        StopArrowBob();
    }

    private void OnDestroy()
    {
        if (_runtimeMaterial != null)
        {
            Destroy(_runtimeMaterial);
        }
    }

    public void Show(string message, Transform target)
    {
        SetCompactMessage(false);
        _worldTarget = target;
        _secondaryWorldTarget = null;
        _uiTarget = null;
        _secondaryUiTarget = null;
        _hasSecondHole = false;
        _interactionMode = SpotlightInteractionMode.BlockAll;
        _centerCalloutBetweenTargets = false;
        _useRectangularHole = false;
        _useRectangularSecondHole = false;
        SetBackgroundDim(1f);
        _arrowRect.gameObject.SetActive(true);
        Show(message);
    }

    public void ShowFocus(Transform target)
    {
        SetCompactMessage(false);
        _worldTarget = target;
        _secondaryWorldTarget = null;
        _uiTarget = null;
        _secondaryUiTarget = null;
        _hasSecondHole = false;
        _interactionMode = SpotlightInteractionMode.BlockAll;
        _centerCalloutBetweenTargets = false;
        _useRectangularHole = false;
        _useRectangularSecondHole = false;
        SetBackgroundDim(1f);
        _messageRect.gameObject.SetActive(false);
        _arrowRect.gameObject.SetActive(false);
        Activate();
    }

    public void ShowWorldTargets(string message, Transform firstTarget, Transform secondTarget)
    {
        SetCompactMessage(false);
        _worldTarget = firstTarget;
        _secondaryWorldTarget = secondTarget;
        _uiTarget = null;
        _secondaryUiTarget = null;
        _hasSecondHole = true;
        _interactionMode = SpotlightInteractionMode.BlockAll;
        _centerCalloutBetweenTargets = true;
        _useRectangularHole = false;
        _useRectangularSecondHole = false;
        SetBackgroundDim(1f);
        _arrowRect.gameObject.SetActive(false);
        Show(message);
    }

    public void ShowUiTarget(
        string message,
        RectTransform target,
        SpotlightInteractionMode interactionMode = SpotlightInteractionMode.BlockAll,
        float backgroundDimStrength = 1f)
    {
        SetCompactMessage(false);
        _worldTarget = null;
        _secondaryWorldTarget = null;
        _uiTarget = target;
        _secondaryUiTarget = null;
        _hasSecondHole = false;
        _interactionMode = interactionMode;
        _centerCalloutBetweenTargets = false;
        _useRectangularHole = true;
        _useRectangularSecondHole = true;
        SetBackgroundDim(backgroundDimStrength);
        _arrowRect.gameObject.SetActive(true);
        Show(message);
    }

    public void ShowUiFocus(RectTransform target, float backgroundDimStrength = 1f)
    {
        SetCompactMessage(false);
        _worldTarget = null;
        _secondaryWorldTarget = null;
        _uiTarget = target;
        _secondaryUiTarget = null;
        _hasSecondHole = false;
        _interactionMode = SpotlightInteractionMode.BlockAll;
        _centerCalloutBetweenTargets = false;
        _useRectangularHole = true;
        _useRectangularSecondHole = true;
        SetBackgroundDim(backgroundDimStrength);
        _messageRect.gameObject.SetActive(false);
        _arrowRect.gameObject.SetActive(false);
        Activate();
    }

    public void ShowUiTargets(
        string message,
        RectTransform primaryTarget,
        RectTransform secondaryTarget,
        SpotlightInteractionMode interactionMode = SpotlightInteractionMode.BlockAll,
        bool useCompactMessage = false,
        float backgroundDimStrength = 1f)
    {
        SetCompactMessage(useCompactMessage);
        _worldTarget = null;
        _secondaryWorldTarget = null;
        _uiTarget = primaryTarget;
        _secondaryUiTarget = secondaryTarget;
        _hasSecondHole = true;
        _interactionMode = interactionMode;
        _centerCalloutBetweenTargets = false;
        _useRectangularHole = true;
        _useRectangularSecondHole = true;
        SetBackgroundDim(backgroundDimStrength);
        _arrowRect.gameObject.SetActive(true);
        Show(message);
    }

    // 안내창의 최대 폭만 정한다. 실제 크기는 문구가 정해진 뒤 FitMessageBox가 맞춘다.
    private void SetCompactMessage(bool isCompact)
    {
        _messageMaxWidth = isCompact ? _compactMessageWidth : _defaultMessageSize.x;
    }

    // 행동 안내는 대화창과 다른 창에 담는다. 고정 폭이면 짧은 문구에도 긴 띠가 되므로
    // 글자 크기에 여백을 더해 창을 맞추고, 최대 폭을 넘으면 줄을 바꾼다.
    private void FitMessageBox(string message)
    {
        float maxTextWidth = Mathf.Max(0f, _messageMaxWidth - _messagePadding.x);
        Vector2 preferred = _messageText.GetPreferredValues(message, maxTextWidth, 0f);
        _messageRect.sizeDelta = new Vector2(
            Mathf.Min(preferred.x, maxTextWidth) + _messagePadding.x,
            preferred.y + _messagePadding.y);
    }

    private void Show(string message)
    {
        _messageRect.gameObject.SetActive(true);
        _messageText.text = message;
        FitMessageBox(message);
        Activate();
    }

    private void Activate()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        UpdateTargetPosition();
        RestartArrowBob();
    }

    public void Hide()
    {
        _worldTarget = null;
        _secondaryWorldTarget = null;
        _uiTarget = null;
        _secondaryUiTarget = null;
        _hasSecondHole = false;
        _interactionMode = SpotlightInteractionMode.BlockAll;
        _centerCalloutBetweenTargets = false;
        _useRectangularHole = false;
        _useRectangularSecondHole = false;
        SetBackgroundDim(1f);
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        UpdateTargetPosition();
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        return _interactionMode != SpotlightInteractionMode.PassThroughPrimary ||
               !IsInsidePrimaryHole(screenPoint, eventCamera);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_interactionMode != SpotlightInteractionMode.AdvanceOnPrimaryTap ||
            !IsInsidePrimaryHole(eventData.position, eventData.pressEventCamera))
        {
            return;
        }

        AdvanceRequested?.Invoke();
    }

    private bool IsInsidePrimaryHole(Vector2 screenPoint, Camera eventCamera)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootRect,
                screenPoint,
                eventCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        return IsInsideHole(
            localPoint,
            _holeCenter,
            _passThroughHoleSize,
            _useRectangularHole);
    }

    private static bool IsInsideHole(
        Vector2 point,
        Vector2 center,
        Vector2 size,
        bool isRectangular)
    {
        Vector2 halfSize = size * 0.5f;
        if (halfSize.x <= 0f || halfSize.y <= 0f) return false;

        Vector2 normalized = new Vector2(
            (point.x - center.x) / halfSize.x,
            (point.y - center.y) / halfSize.y);

        return isRectangular
            ? Mathf.Abs(normalized.x) <= 1f && Mathf.Abs(normalized.y) <= 1f
            : normalized.sqrMagnitude <= 1f;
    }

    // 통과/전진 판정용 실제 대상 영역 크기. 자식(라벨/아이콘)을 제외하고 _uiTarget 자체
    // rect만 사용한다. 대상의 월드 모서리를 _rootRect 로컬로 변환한 뒤 축 정렬 bounding으로
    // 크기만 취해, 회전이 없는 하단 HUD 등에서 안전하게 대상 영역을 좁힌다. padding은 더하지 않는다.
    private Vector2 CalculateTargetRectSize(RectTransform target)
    {
        Vector3[] worldCorners = new Vector3[4];
        target.GetWorldCorners(worldCorners);

        Vector2 min = _rootRect.InverseTransformPoint(worldCorners[0]);
        Vector2 max = min;
        for (int i = 1; i < 4; i++)
        {
            Vector2 local = _rootRect.InverseTransformPoint(worldCorners[i]);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        return max - min;
    }

    private void UpdateTargetPosition()
    {
        if (_canvas == null || _runtimeMaterial == null)
        {
            return;
        }

        if (_uiTarget != null)
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                _rootRect,
                _uiTarget);
            _holeCenter = bounds.center;
            // 표시용 크기: 자식 포함 bounds + padding. 시각 강조는 여유 있게 유지한다.
            _currentHoleSize = new Vector2(bounds.size.x, bounds.size.y) +
                               _uiHolePadding * 2f;
            // 판정용 크기: 옵션 (a) 자식 제외 + padding 미포함. 하단 HUD 버튼은 라벨 자식이
            // 버튼 rect보다 넓게 배치되므로, 자식 포함 bounds를 판정에 쓰면 이웃까지 오통과된다.
            // _uiTarget 자체 rect를 _rootRect 로컬 좌표계로 변환해 실제 대상 영역만 취한다.
            _passThroughHoleSize = CalculateTargetRectSize(_uiTarget);
        }
        else if (!TryGetWorldTargetCenter(_worldTarget, out _holeCenter))
        {
            return;
        }

        // 월드 대상은 padding이 없어 표시=판정. UI 대상은 위에서 각각 확정했으므로 유지한다.
        _currentHoleSize = _uiTarget != null
            ? _currentHoleSize
            : _holeSize;
        _passThroughHoleSize = _uiTarget != null
            ? _passThroughHoleSize
            : _holeSize;

        if (_secondaryUiTarget != null)
        {
            Bounds secondBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                _rootRect,
                _secondaryUiTarget);
            _secondHoleCenter = secondBounds.center;
            _secondHoleSize = new Vector2(
                secondBounds.size.x,
                secondBounds.size.y) + _uiHolePadding * 2f;
            _hasSecondHole = true;
        }
        else
        {
            _hasSecondHole = _secondaryWorldTarget != null &&
                             TryGetWorldTargetCenter(
                                 _secondaryWorldTarget,
                                 out _secondHoleCenter);
            _secondHoleSize = _holeSize;
        }

        Rect rect = _rootRect.rect;
        if (rect.width <= 0f || rect.height <= 0f) return;

        // 화면 밖으로 나간 사각형 구멍은 화면 안으로 잘라서 그린다. 잘라 내지 않으면 가장자리가
        // 화면 밖에 있어 발광 띠가 보이지 않고, 안내 화살표도 화면 밖을 가리킨다. 통과와 전진
        // 판정은 _holeCenter와 _passThroughHoleSize를 그대로 쓰므로 영향이 없다. 원형 구멍은
        // 보이는 부분이 이미 원호라 자르지 않는다.
        Vector2 visibleCenter = _holeCenter;
        Vector2 visibleSize = _currentHoleSize;
        if (_useRectangularHole)
        {
            ClampToRect(rect, ref visibleCenter, ref visibleSize);
        }

        if (_hasSecondHole && _useRectangularSecondHole)
        {
            ClampToRect(rect, ref _secondHoleCenter, ref _secondHoleSize);
        }

        Vector2 normalizedCenter = new Vector2(
            (visibleCenter.x - rect.xMin) / rect.width,
            (visibleCenter.y - rect.yMin) / rect.height);
        Vector2 normalizedHalfSize = new Vector2(
            visibleSize.x * 0.5f / rect.width,
            visibleSize.y * 0.5f / rect.height);

        _runtimeMaterial.SetVector(RootSizeId, new Vector4(rect.width, rect.height, 0f, 0f));
        _runtimeMaterial.SetVector(HoleCenterId, normalizedCenter);
        _runtimeMaterial.SetVector(HoleSizeId, normalizedHalfSize);
        _runtimeMaterial.SetFloat(HoleShapeId, _useRectangularHole ? 1f : 0f);
        _runtimeMaterial.SetFloat(SecondHoleEnabledId, _hasSecondHole ? 1f : 0f);
        _runtimeMaterial.SetFloat(
            SecondHoleShapeId,
            _useRectangularSecondHole ? 1f : 0f);

        Vector2 calloutCenter = visibleCenter;
        if (_hasSecondHole)
        {
            Vector2 normalizedSecondCenter = new Vector2(
                (_secondHoleCenter.x - rect.xMin) / rect.width,
                (_secondHoleCenter.y - rect.yMin) / rect.height);
            Vector2 normalizedSecondHalfSize = new Vector2(
                _secondHoleSize.x * 0.5f / rect.width,
                _secondHoleSize.y * 0.5f / rect.height);

            _runtimeMaterial.SetVector(SecondHoleCenterId, normalizedSecondCenter);
            _runtimeMaterial.SetVector(SecondHoleSizeId, normalizedSecondHalfSize);
            if (_centerCalloutBetweenTargets)
            {
                calloutCenter = (visibleCenter + _secondHoleCenter) * 0.5f;
            }
        }

        UpdateCalloutPosition(calloutCenter, visibleSize.y * 0.5f);
    }

    // 중심과 크기로 준 사각형을 bounds 안으로 자른다. 완전히 밖이면 손대지 않는다.
    private static void ClampToRect(Rect bounds, ref Vector2 center, ref Vector2 size)
    {
        Vector2 min = Vector2.Max(center - size * 0.5f, bounds.min);
        Vector2 max = Vector2.Min(center + size * 0.5f, bounds.max);
        if (max.x <= min.x || max.y <= min.y) return;

        center = (min + max) * 0.5f;
        size = max - min;
    }

    private void SetBackgroundDim(float strength)
    {
        Color color = _defaultOverlayColor;
        color.a = _defaultOverlayColor.a * Mathf.Clamp01(strength);
        _overlayImage.color = color;
    }

    private bool TryGetWorldTargetCenter(Transform target, out Vector2 localPosition)
    {
        localPosition = Vector2.zero;
        if (target == null || _worldCamera == null) return false;

        Vector3 screenPoint = _worldCamera.WorldToScreenPoint(target.position);
        Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootRect,
                screenPoint,
                uiCamera,
                out localPosition))
        {
            return false;
        }

        return true;
    }

    // 화살표는 구멍 가장자리에서 떨어진 자리에 놓는다. 고정 거리만 쓰면 큰 구멍에서 화살표가
    // 구멍 안으로 들어가고, 화살표가 커질수록 더 그렇다. 안내창은 화살표 바깥에 놓아 겹치지 않게
    // 하되, 구멍이 작으면 예전과 같은 거리를 유지한다.
    private void UpdateCalloutPosition(Vector2 calloutCenter, float holeHalfHeight)
    {
        bool placeBelow = calloutCenter.y > _rootRect.rect.center.y;
        float direction = placeBelow ? -1f : 1f;

        bool hasArrow = _arrowRect.gameObject.activeSelf;
        float arrowHalfHeight = _arrowImageRect.rect.height * 0.5f;
        float arrowDistance = Mathf.Max(
            Mathf.Abs(_arrowOffset.y),
            holeHalfHeight + _arrowGap + _arrowBobDistance + arrowHalfHeight);
        float messageDistance = Mathf.Abs(_messageOffset.y);
        if (hasArrow)
        {
            messageDistance = Mathf.Max(
                messageDistance,
                arrowDistance + arrowHalfHeight + _arrowGap +
                _messageRect.rect.height * 0.5f);
        }

        Vector2 desiredMessagePosition = calloutCenter + new Vector2(
            _messageOffset.x,
            messageDistance * direction);
        _messageRect.anchoredPosition = ClampInsideRoot(
            _messageRect,
            desiredMessagePosition,
            _messageScreenMargin);
        _arrowRect.anchoredPosition = calloutCenter + new Vector2(
            _arrowOffset.x,
            arrowDistance * direction);
        _arrowRect.localRotation = Quaternion.Euler(0f, 0f, placeBelow ? 180f : 0f);
    }

    // 원본 그림이 아래를 가리키므로 아래쪽(-y)이 구멍 쪽이다. 화살표 뿌리가 구멍 반대편에
    // 있을 때는 뿌리 오브젝트가 180도 돌아 있어 같은 방향이 구멍 쪽이 된다.
    private void RestartArrowBob()
    {
        StopArrowBob();
        _arrowImageRect.anchoredPosition = Vector2.zero;
        if (!_arrowRect.gameObject.activeSelf || _arrowBobDistance <= 0f) return;

        _arrowBobTween = _arrowImageRect
            .DOAnchorPosY(-_arrowBobDistance, _arrowBobDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void StopArrowBob()
    {
        _arrowBobTween?.Kill();
        _arrowBobTween = null;
    }


    private Vector2 ClampInsideRoot(
        RectTransform target,
        Vector2 desiredPosition,
        float margin)
    {
        Rect rootRect = _rootRect.rect;
        Rect targetRect = target.rect;
        Vector2 pivot = target.pivot;

        float minX = rootRect.xMin + margin + targetRect.width * pivot.x;
        float maxX = rootRect.xMax - margin - targetRect.width * (1f - pivot.x);
        float minY = rootRect.yMin + margin + targetRect.height * pivot.y;
        float maxY = rootRect.yMax - margin - targetRect.height * (1f - pivot.y);

        return new Vector2(
            minX <= maxX ? Mathf.Clamp(desiredPosition.x, minX, maxX) : rootRect.center.x,
            minY <= maxY ? Mathf.Clamp(desiredPosition.y, minY, maxY) : rootRect.center.y);
    }
}
