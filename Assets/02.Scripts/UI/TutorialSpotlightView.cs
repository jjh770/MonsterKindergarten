using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialSpotlightView : MonoBehaviour, ICanvasRaycastFilter
{
    private static readonly int HoleCenterId = Shader.PropertyToID("_HoleCenter");
    private static readonly int HoleSizeId = Shader.PropertyToID("_HoleSize");

    [Header("References")]
    [SerializeField] private Image _overlayImage;
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private RectTransform _messageRect;
    [SerializeField] private RectTransform _arrowRect;

    [Header("Spotlight")]
    [SerializeField] private Vector2 _holeSize = new Vector2(320f, 320f);
    [SerializeField] private Vector2 _messageOffset = new Vector2(0f, 240f);
    [SerializeField] private Vector2 _arrowOffset = new Vector2(0f, 135f);

    private RectTransform _rootRect;
    private Canvas _canvas;
    private Camera _worldCamera;
    private Material _runtimeMaterial;
    private Transform _target;
    private Vector2 _holeCenter;

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
        _messageText.text = message;
        _target = target;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        UpdateTargetPosition();
    }

    public void Hide()
    {
        _target = null;
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        UpdateTargetPosition();
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootRect,
                screenPoint,
                eventCamera,
                out Vector2 localPoint))
        {
            return true;
        }

        Vector2 halfSize = _holeSize * 0.5f;
        if (halfSize.x <= 0f || halfSize.y <= 0f) return true;

        Vector2 normalized = new Vector2(
            (localPoint.x - _holeCenter.x) / halfSize.x,
            (localPoint.y - _holeCenter.y) / halfSize.y);

        // false인 구멍 내부는 뒤의 UI로 이벤트를 통과시키고, 외부는 오버레이가 소비한다.
        return normalized.sqrMagnitude > 1f;
    }

    private void UpdateTargetPosition()
    {
        if (_target == null || _worldCamera == null || _canvas == null ||
            _runtimeMaterial == null)
        {
            return;
        }

        Vector3 screenPoint = _worldCamera.WorldToScreenPoint(_target.position);
        Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootRect,
                screenPoint,
                uiCamera,
                out _holeCenter))
        {
            return;
        }

        Rect rect = _rootRect.rect;
        if (rect.width <= 0f || rect.height <= 0f) return;

        Vector2 normalizedCenter = new Vector2(
            (_holeCenter.x - rect.xMin) / rect.width,
            (_holeCenter.y - rect.yMin) / rect.height);
        Vector2 normalizedHalfSize = new Vector2(
            _holeSize.x * 0.5f / rect.width,
            _holeSize.y * 0.5f / rect.height);

        _runtimeMaterial.SetVector(HoleCenterId, normalizedCenter);
        _runtimeMaterial.SetVector(HoleSizeId, normalizedHalfSize);
        _messageRect.anchoredPosition = _holeCenter + _messageOffset;
        _arrowRect.anchoredPosition = _holeCenter + _arrowOffset;
    }
}
