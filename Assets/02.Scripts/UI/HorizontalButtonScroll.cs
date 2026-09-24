using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Button을 ScrollRect 아래에 두지 않고 가로 메뉴를 드래그한다.
// ScrollRect가 드래그 임계값을 지워 자식 Button의 터치 클릭을 취소하는 문제를 피한다.
public sealed class HorizontalButtonScroll : MonoBehaviour
{
    [SerializeField] private RectTransform _viewport;
    [SerializeField] private HorizontalLayoutGroup _layout;
    [SerializeField] private RectTransform[] _items;
    [SerializeField] private Button[] _buttons;
    [SerializeField, Min(0f)] private float _horizontalPadding = 40f;
    [SerializeField, Min(0f)] private float _dragThreshold = 12f;

    private static readonly List<RaycastResult> _raycastResults = new();
    private Vector2[] _basePositions;
    private bool[] _buttonInteractableStates;
    private readonly Vector3[] _worldCorners = new Vector3[4];
    private Vector2 _pressScreenPosition;
    private Vector2 _previousLocalPosition;
    private float _scrollOffset;
    private float _minOffset;
    private float _maxOffset;
    private int _activeSignature;
    private int _restoreButtonsAfterFrame = -1;
    private bool _isPointerDown;
    private bool _isDragging;

    private void Awake()
    {
        if (_viewport == null || _layout == null ||
            _items == null || _items.Length == 0 ||
            _buttons == null || _buttons.Length == 0)
        {
            Debug.LogError("하단 메뉴 스크롤의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _basePositions = new Vector2[_items.Length];
        _buttonInteractableStates = new bool[_buttons.Length];
    }

    private void OnEnable()
    {
        if (_basePositions != null)
        {
            RefreshLayout();
        }
    }

    private void OnDisable()
    {
        RestoreButtonInput();
        _isPointerDown = false;
        _isDragging = false;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled && _basePositions != null)
        {
            RefreshLayout();
        }
    }

    private void Update()
    {
        if (_restoreButtonsAfterFrame >= 0 &&
            Time.frameCount > _restoreButtonsAfterFrame)
        {
            RestoreButtonInput();
        }

        int activeSignature = GetActiveSignature();
        if (activeSignature != _activeSignature)
        {
            RefreshLayout();
        }

        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        Vector2 screenPosition = pointer.position.ReadValue();
        if (_maxOffset > _minOffset &&
            pointer.press.wasPressedThisFrame &&
            RectTransformUtility.RectangleContainsScreenPoint(
                _viewport,
                screenPosition,
                GetEventCamera()) &&
            IsTopMostAtPointer(screenPosition))
        {
            _isPointerDown = TryGetLocalPosition(screenPosition, out _previousLocalPosition);
            _pressScreenPosition = screenPosition;
            _isDragging = false;
        }

        if (!_isPointerDown) return;

        if (pointer.press.isPressed && TryGetLocalPosition(screenPosition, out Vector2 localPosition))
        {
            if (!_isDragging &&
                Mathf.Abs(screenPosition.x - _pressScreenPosition.x) >= _dragThreshold)
            {
                _isDragging = true;
                SuspendButtonInput();
            }

            if (_isDragging)
            {
                _scrollOffset = Mathf.Clamp(
                    _scrollOffset + localPosition.x - _previousLocalPosition.x,
                    _minOffset,
                    _maxOffset);
                ApplyOffset();
            }

            _previousLocalPosition = localPosition;
        }

        if (pointer.press.wasReleasedThisFrame)
        {
            _isPointerDown = false;
            if (_isDragging)
            {
                _restoreButtonsAfterFrame = Time.frameCount;
            }

            _isDragging = false;
        }
    }

    private void RefreshLayout()
    {
        _layout.enabled = true;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_viewport);

        float contentMin = float.PositiveInfinity;
        float contentMax = float.NegativeInfinity;
        bool hasActiveItem = false;

        for (int i = 0; i < _items.Length; i++)
        {
            RectTransform item = _items[i];
            if (item == null) continue;

            _basePositions[i] = item.anchoredPosition;
            if (!item.gameObject.activeSelf) continue;

            hasActiveItem = true;
            item.GetWorldCorners(_worldCorners);
            float itemMin = _viewport.InverseTransformPoint(_worldCorners[0]).x;
            float itemMax = _viewport.InverseTransformPoint(_worldCorners[2]).x;
            contentMin = Mathf.Min(contentMin, itemMin);
            contentMax = Mathf.Max(contentMax, itemMax);
        }

        _layout.enabled = false;
        _activeSignature = GetActiveSignature();

        if (!hasActiveItem)
        {
            _minOffset = 0f;
            _maxOffset = 0f;
        }
        else
        {
            float viewportMin = _viewport.rect.xMin + _horizontalPadding;
            float viewportMax = _viewport.rect.xMax - _horizontalPadding;
            float contentWidth = contentMax - contentMin;
            float viewportWidth = Mathf.Max(0f, viewportMax - viewportMin);

            if (contentWidth <= viewportWidth)
            {
                _minOffset = 0f;
                _maxOffset = 0f;
            }
            else
            {
                _minOffset = viewportMax - contentMax;
                _maxOffset = viewportMin - contentMin;
            }
        }

        _scrollOffset = Mathf.Clamp(_scrollOffset, _minOffset, _maxOffset);
        ApplyOffset();
    }

    private void ApplyOffset()
    {
        for (int i = 0; i < _items.Length; i++)
        {
            RectTransform item = _items[i];
            if (item == null) continue;

            Vector2 position = _basePositions[i];
            position.x += _scrollOffset;
            item.anchoredPosition = position;
        }
    }

    private int GetActiveSignature()
    {
        int signature = 17;
        foreach (RectTransform item in _items)
        {
            signature = signature * 31 +
                        (item != null && item.gameObject.activeSelf ? 1 : 0);
        }

        return signature;
    }

    // 이 메뉴는 화면 입력을 직접 읽으므로, 위에 다른 UI가 덮여 있어도 알지 못한다.
    // 상점 서랍이 열린 채로 그 위를 끌면 뒤에서 메뉴가 같이 움직인다.
    //
    // 그래서 누른 자리의 맨 위에 걸린 것이 이 메뉴인지 확인한다. 판정은 캔버스
    // 순서를 따르므로, 서랍(PopupCanvas)이 덮고 있으면 그쪽이 먼저 잡힌다.
    private bool IsTopMostAtPointer(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        // 판정할 수단이 없으면 막지 않는다. 덮인 경우보다 안 덮인 경우가 흔하다.
        if (eventSystem == null) return true;

        var pointerData = new PointerEventData(eventSystem) { position = screenPosition };
        _raycastResults.Clear();
        eventSystem.RaycastAll(pointerData, _raycastResults);
        if (_raycastResults.Count == 0) return false;

        // IsChildOf는 자기 자신도 참으로 본다. 빈자리를 눌렀을 때 패널이 잡히는 경우다.
        return _raycastResults[0].gameObject.transform.IsChildOf(_viewport);
    }

    private bool TryGetLocalPosition(Vector2 screenPosition, out Vector2 localPosition)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _viewport,
            screenPosition,
            GetEventCamera(),
            out localPosition);
    }

    private Camera GetEventCamera()
    {
        Canvas canvas = _viewport.GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
    }

    private void SuspendButtonInput()
    {
        for (int i = 0; i < _buttons.Length; i++)
        {
            Button button = _buttons[i];
            if (button == null) continue;

            _buttonInteractableStates[i] = button.interactable;
            button.interactable = false;
        }
    }

    private void RestoreButtonInput()
    {
        if (_restoreButtonsAfterFrame < 0 && !_isDragging) return;

        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] != null)
            {
                _buttons[i].interactable = _buttonInteractableStates[i];
            }
        }

        _restoreButtonsAfterFrame = -1;
    }
}
