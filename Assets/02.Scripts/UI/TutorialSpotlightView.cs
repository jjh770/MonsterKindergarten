using System;
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

    [Header("References")]
    [SerializeField] private Image _overlayImage;
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private RectTransform _messageRect;
    [SerializeField] private RectTransform _arrowRect;

    [Header("Spotlight")]
    [SerializeField] private Vector2 _holeSize = new Vector2(320f, 320f);
    [SerializeField] private Vector2 _uiHolePadding = new Vector2(40f, 30f);
    [SerializeField] private Vector2 _messageOffset = new Vector2(0f, 240f);
    [SerializeField] private Vector2 _arrowOffset = new Vector2(0f, 135f);
    [SerializeField, Min(0f)] private float _messageScreenMargin = 30f;
    [SerializeField, Min(0f)] private float _compactMessageWidth = 240f;

    private RectTransform _rootRect;
    private Canvas _canvas;
    private Camera _worldCamera;
    private Material _runtimeMaterial;
    private Transform _worldTarget;
    private Transform _secondaryWorldTarget;
    private RectTransform _uiTarget;
    private RectTransform _secondaryUiTarget;
    private Vector2 _holeCenter;
    private Vector2 _currentHoleSize;
    private Vector2 _secondHoleCenter;
    private Vector2 _secondHoleSize;
    private bool _hasSecondHole;
    private SpotlightInteractionMode _interactionMode;
    private bool _centerCalloutBetweenTargets;
    private bool _useRectangularHole;
    private bool _useRectangularSecondHole;
    private Vector2 _defaultMessageSize;

    public event Action AdvanceRequested;

    private void Awake()
    {
        _rootRect = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();
        _worldCamera = Camera.main;

        if (_overlayImage == null || _messageText == null ||
            _messageRect == null || _arrowRect == null)
        {
            Debug.LogError("튜토리얼 스포트라이트 프리팹의 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _runtimeMaterial = Instantiate(_overlayImage.material);
        _overlayImage.material = _runtimeMaterial;
        _defaultMessageSize = _messageRect.sizeDelta;
        gameObject.SetActive(false);
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
        _arrowRect.gameObject.SetActive(false);
        Show(message);
    }

    public void ShowUiTarget(
        string message,
        RectTransform target,
        SpotlightInteractionMode interactionMode = SpotlightInteractionMode.BlockAll)
    {
        SetCompactMessage(false);
        _worldTarget = null;
        _secondaryWorldTarget = null;
        _uiTarget = target;
        _secondaryUiTarget = null;
        _hasSecondHole = false;
        _interactionMode = interactionMode;
        _centerCalloutBetweenTargets = false;
        _useRectangularHole = false;
        _useRectangularSecondHole = false;
        _arrowRect.gameObject.SetActive(true);
        Show(message);
    }

    public void ShowUiFocus(
        RectTransform target,
        bool useRectangularHole = false)
    {
        SetCompactMessage(false);
        _worldTarget = null;
        _secondaryWorldTarget = null;
        _uiTarget = target;
        _secondaryUiTarget = null;
        _hasSecondHole = false;
        _interactionMode = SpotlightInteractionMode.BlockAll;
        _centerCalloutBetweenTargets = false;
        _useRectangularHole = useRectangularHole;
        _useRectangularSecondHole = false;
        _messageRect.gameObject.SetActive(false);
        _arrowRect.gameObject.SetActive(false);
        Activate();
    }

    public void ShowUiFocusTargets(
        RectTransform firstTarget,
        RectTransform secondTarget,
        bool useRectangularHoles = false)
    {
        SetCompactMessage(false);
        _worldTarget = null;
        _secondaryWorldTarget = null;
        _uiTarget = firstTarget;
        _secondaryUiTarget = secondTarget;
        _hasSecondHole = true;
        _interactionMode = SpotlightInteractionMode.BlockAll;
        _centerCalloutBetweenTargets = false;
        _useRectangularHole = useRectangularHoles;
        _useRectangularSecondHole = useRectangularHoles;
        _messageRect.gameObject.SetActive(false);
        _arrowRect.gameObject.SetActive(false);
        Activate();
    }

    public void ShowUiTargets(
        string message,
        RectTransform primaryTarget,
        RectTransform secondaryTarget,
        SpotlightInteractionMode interactionMode = SpotlightInteractionMode.BlockAll,
        bool useRectangularSecondaryHole = false,
        bool useCompactMessage = false)
    {
        SetCompactMessage(useCompactMessage);
        _worldTarget = null;
        _secondaryWorldTarget = null;
        _uiTarget = primaryTarget;
        _secondaryUiTarget = secondaryTarget;
        _hasSecondHole = true;
        _interactionMode = interactionMode;
        _centerCalloutBetweenTargets = false;
        _useRectangularHole = false;
        _useRectangularSecondHole = useRectangularSecondaryHole;
        _arrowRect.gameObject.SetActive(true);
        Show(message);
    }

    private void SetCompactMessage(bool isCompact)
    {
        Vector2 messageSize = _defaultMessageSize;
        if (isCompact)
        {
            messageSize.x = _compactMessageWidth;
        }

        _messageRect.sizeDelta = messageSize;
    }

    private void Show(string message)
    {
        _messageRect.gameObject.SetActive(true);
        _messageText.text = message;
        Activate();
    }

    private void Activate()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        UpdateTargetPosition();
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
            _currentHoleSize,
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
            _currentHoleSize = new Vector2(bounds.size.x, bounds.size.y) +
                               _uiHolePadding * 2f;
        }
        else if (!TryGetWorldTargetCenter(_worldTarget, out _holeCenter))
        {
            return;
        }

        _currentHoleSize = _uiTarget != null
            ? _currentHoleSize
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

        Vector2 normalizedCenter = new Vector2(
            (_holeCenter.x - rect.xMin) / rect.width,
            (_holeCenter.y - rect.yMin) / rect.height);
        Vector2 normalizedHalfSize = new Vector2(
            _currentHoleSize.x * 0.5f / rect.width,
            _currentHoleSize.y * 0.5f / rect.height);

        _runtimeMaterial.SetVector(HoleCenterId, normalizedCenter);
        _runtimeMaterial.SetVector(HoleSizeId, normalizedHalfSize);
        _runtimeMaterial.SetFloat(HoleShapeId, _useRectangularHole ? 1f : 0f);
        _runtimeMaterial.SetFloat(SecondHoleEnabledId, _hasSecondHole ? 1f : 0f);
        _runtimeMaterial.SetFloat(
            SecondHoleShapeId,
            _useRectangularSecondHole ? 1f : 0f);

        Vector2 calloutCenter = _holeCenter;
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
                calloutCenter = (_holeCenter + _secondHoleCenter) * 0.5f;
            }
        }

        UpdateCalloutPosition(calloutCenter);
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

    private void UpdateCalloutPosition(Vector2 calloutCenter)
    {
        bool placeBelow = calloutCenter.y > _rootRect.rect.center.y;
        float direction = placeBelow ? -1f : 1f;

        Vector2 desiredMessagePosition = calloutCenter + new Vector2(
            _messageOffset.x,
            Mathf.Abs(_messageOffset.y) * direction);
        _messageRect.anchoredPosition = ClampInsideRoot(
            _messageRect,
            desiredMessagePosition,
            _messageScreenMargin);
        _arrowRect.anchoredPosition = calloutCenter + new Vector2(
            _arrowOffset.x,
            Mathf.Abs(_arrowOffset.y) * direction);
        _arrowRect.localRotation = Quaternion.Euler(0f, 0f, placeBelow ? 180f : 0f);
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
