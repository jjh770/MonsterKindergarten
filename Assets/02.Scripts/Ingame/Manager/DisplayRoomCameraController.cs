using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// 장식장에서 카메라를 움직이는 일을 맡는다. 두 손가락 확대와 한 손가락 이동, 슬라임 포커스와
// 관찰, 기본 화면으로의 복귀다.
//
// 공간과 배경을 바꾸는 연출(GameplayTransitionPlayer)과는 바뀌는 이유가 다르다. 이쪽은
// 입력과 화면 경계 계산이 바뀔 때, 저쪽은 전환 타이밍이 바뀔 때 손댄다. 그래도 카메라는 하나라
// 둘이 주고받는 것이 있다. 전환 연출은 시작과 끝에 SetSuspended로 이쪽 입력을 멈추고, 화면이
// 가려진 순간 EnterSpace로 어느 공간을 보는지 알려 주며, 전환을 시작할 때 ResetView로 확대를
// 풀어 기본 화면에서 출발한다.
public sealed class DisplayRoomCameraController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Camera _camera;
    [SerializeField] private PlayAreaBounds _displayRoomArea;

    [Tooltip("확대한 상태에서 빈 곳을 한 손가락으로 끌어 화면을 옮길 때, 슬라임을 누른 손가락을 가려냅니다.")]
    [SerializeField] private Clicker _clicker;

    [Header("Display Room Zoom")]
    [Tooltip("장식장에서 쓰는 화면 크기입니다. 방의 벽을 읽을 수 없을 때만 쓰는 대체값입니다.")]
    [SerializeField, Min(0.1f)] private float _displayRoomOrthographicSize = 6.5f;
    [SerializeField, Min(0.1f)] private float _displayRoomMinZoomSize = 3.5f;
    [SerializeField, Min(0f)] private float _displayRoomFitPadding = 0.35f;
    [SerializeField, Min(0.05f)] private float _displayRoomMouseWheelStep = 0.75f;

    [Tooltip("이만큼(픽셀) 움직여야 끄는 것으로 봅니다. 그 아래는 그냥 누르기입니다.")]
    [SerializeField, Min(0f)] private float _displayRoomPanThresholdPixels = 12f;

    [Header("Display Room Focus")]
    [SerializeField, Min(0.1f)] private float _displayRoomFocusDuration = 0.35f;
    [SerializeField, Min(0.1f)] private float _displayRoomFocusSize = 2.5f;
    [SerializeField, Min(0.1f)] private float _displayRoomObservationSize = 2.1f;
    [SerializeField, Min(0f)] private float _displayRoomFollowSpeed = 8f;

    private const float ZoomedEpsilon = 0.01f;

    private Vector3 _cameraBasePosition;
    private float _cameraBaseOrthographicSize;
    private EGameplaySpace _currentSpace = EGameplaySpace.MainField;
    private bool _isSuspended;
    private Sequence _focusSequence;
    private SlimeController _displayRoomFocusTarget;
    private bool _isPinching;
    private float _previousPinchDistance;
    private Vector2 _previousPinchMidpoint;
    private bool _isPanCandidate;
    private bool _isPanning;
    private Vector2 _panStartScreen;
    private Vector2 _panPreviousScreen;
    private readonly List<RaycastResult> _uiRaycastResults = new();

    // 전환 연출이 카메라를 옮길 때 되돌아올 자리다. Awake에서 한 번 읽는다.
    public Vector3 BasePosition => _cameraBasePosition;

    // 기본 화면보다 확대돼 있는가. 방 전체가 보여야 하는 배치 모드가 묻는다.
    public bool IsZoomed => _currentSpace == EGameplaySpace.DisplayRoom &&
                            _camera.orthographicSize < BaseOrthographicSize - ZoomedEpsilon;

    // 공간마다 쉬는 자리의 화면 크기가 다르다. 확대·복귀·경계 계산이 모두 이 값을
    // 기준으로 삼아야 장식장에서 확대했다가 돌아올 때 메인 필드 크기로 튀지 않는다.
    private float BaseOrthographicSize => _currentSpace == EGameplaySpace.DisplayRoom
        ? GetDisplayRoomFitSize()
        : _cameraBaseOrthographicSize;

    private void Awake()
    {
        if (_camera == null)
        {
            Debug.LogError("장식장 카메라의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _cameraBasePosition = _camera.transform.position;
        _cameraBaseOrthographicSize = _camera.orthographicSize;
    }

    private void LateUpdate()
    {
        if (_displayRoomFocusTarget == null || _isSuspended)
        {
            return;
        }

        Vector3 destination = GetDisplayRoomFocusPosition(_displayRoomFocusTarget);
        float followAmount = 1f - Mathf.Exp(-_displayRoomFollowSpeed * Time.deltaTime);
        _camera.transform.position = Vector3.Lerp(
            _camera.transform.position,
            destination,
            followAmount);
    }

    private void Update()
    {
        if (!CanManuallyZoom())
        {
            ResetPinch();
            ResetPan();
            return;
        }

        if (TryHandlePinch())
        {
            ResetPan();
            return;
        }

        ResetPinch();
        HandleMouseWheelZoom();
        HandleSinglePointerPan();
    }

    private void OnDestroy()
    {
        _focusSequence?.Kill();
        ClearFocusTarget();
    }

    // 전환 연출이 도는 동안에는 입력과 포커스 추적을 멈춘다. 연출이 카메라를 직접 움직이기 때문이다.
    public void SetSuspended(bool isSuspended)
    {
        _isSuspended = isSuspended;
    }

    // 화면이 가려진 순간에 부른다. 공간마다 화면 크기가 다르므로 크기를 함께 맞춘다.
    // 알리기 전에 바꿔야 한다. 배경은 카메라 높이에 맞춰 크기를 다시 잡는데, 순서가 뒤바뀌면
    // 예전 높이로 계산해 화면보다 짧은 배경이 깔린다.
    public void EnterSpace(EGameplaySpace space)
    {
        _currentSpace = space;
        _camera.orthographicSize = BaseOrthographicSize;
    }

    // 포커스와 확대를 모두 풀고 기본 화면으로 되돌린다. 전환은 언제나 이 자리에서 출발한다.
    public void ResetView()
    {
        ClearFocusTarget();
        _focusSequence?.Kill();
        _focusSequence = null;
        _camera.transform.position = _cameraBasePosition;
        _camera.orthographicSize = BaseOrthographicSize;
    }

    public void FocusSlime(SlimeController target, Action onComplete)
    {
        if (target == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (_displayRoomFocusTarget != null &&
            _displayRoomFocusTarget != target)
        {
            _displayRoomFocusTarget.SetDisplayRoomCameraFocus(false);
        }

        _displayRoomFocusTarget = target;
        target.SetDisplayRoomCameraFocus(true);
        _focusSequence?.Kill();
        _focusSequence = DOTween.Sequence();
        _focusSequence.Join(
            _camera
                .DOOrthoSize(
                    Mathf.Min(BaseOrthographicSize, _displayRoomFocusSize),
                    _displayRoomFocusDuration)
                .SetEase(Ease.OutQuad));
        _focusSequence.OnComplete(() =>
        {
            _focusSequence = null;
            onComplete?.Invoke();
        });
    }

    public void BeginObservation(Action onComplete = null)
    {
        if (_displayRoomFocusTarget == null)
        {
            onComplete?.Invoke();
            return;
        }

        _focusSequence?.Kill();
        _focusSequence = DOTween.Sequence();
        _focusSequence.Join(
            _camera
                .DOOrthoSize(
                    Mathf.Min(
                        BaseOrthographicSize,
                        _displayRoomObservationSize),
                    _displayRoomFocusDuration)
                .SetEase(Ease.OutQuad));
        _focusSequence.OnComplete(() =>
        {
            _focusSequence = null;
            onComplete?.Invoke();
        });
    }

    public void EndObservation(Action onComplete = null)
    {
        if (_displayRoomFocusTarget == null)
        {
            onComplete?.Invoke();
            return;
        }

        _focusSequence?.Kill();
        _focusSequence = DOTween.Sequence();
        _focusSequence.Join(
            _camera
                .DOOrthoSize(
                    Mathf.Min(
                        BaseOrthographicSize,
                        _displayRoomFocusSize),
                    _displayRoomFocusDuration)
                .SetEase(Ease.OutQuad));
        _focusSequence.OnComplete(() =>
        {
            _focusSequence = null;
            onComplete?.Invoke();
        });
    }

    public void RestoreFocus(Action onComplete = null)
    {
        ClearFocusTarget();
        _focusSequence?.Kill();
        _focusSequence = DOTween.Sequence();
        _focusSequence.Join(
            _camera.transform
                .DOMove(_cameraBasePosition, _displayRoomFocusDuration)
                .SetEase(Ease.OutQuad));
        _focusSequence.Join(
            _camera
                .DOOrthoSize(BaseOrthographicSize, _displayRoomFocusDuration)
                .SetEase(Ease.OutQuad));
        _focusSequence.OnComplete(() =>
        {
            _focusSequence = null;
            _camera.transform.position = _cameraBasePosition;
            _camera.orthographicSize = BaseOrthographicSize;
            onComplete?.Invoke();
        });
    }

    private Vector3 GetDisplayRoomFocusPosition(SlimeController target)
    {
        if (target == null) return _cameraBasePosition;

        Vector3 position = target.transform.position;
        position.z = _cameraBasePosition.z;

        // 확대된 화면의 세로 범위를 기본 화면 안에 가둔다.
        // 위아래 벽까지 따라가면 배경 바깥의 여백이 드러난다.
        // 가로는 배경이 기본 화면보다 넓어 가두지 않는다.
        float verticalMargin = Mathf.Max(
            0f,
            BaseOrthographicSize - _camera.orthographicSize);
        position.y = Mathf.Clamp(
            position.y,
            _cameraBasePosition.y - verticalMargin,
            _cameraBasePosition.y + verticalMargin);
        return position;
    }

    private bool CanManuallyZoom()
    {
        return _currentSpace == EGameplaySpace.DisplayRoom &&
               !_isSuspended &&
               _focusSequence == null &&
               _displayRoomFocusTarget == null &&
               !DisplayRoomCameraInputGate.IsBlocked &&
               GameplayGate.IsActive;
    }

    private bool TryHandlePinch()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null) return false;

        TouchControl first = null;
        TouchControl second = null;
        foreach (TouchControl touch in touchscreen.touches)
        {
            if (!touch.press.isPressed) continue;

            if (first == null) first = touch;
            else
            {
                second = touch;
                break;
            }
        }

        if (first == null || second == null) return false;

        Vector2 firstPosition = first.position.ReadValue();
        Vector2 secondPosition = second.position.ReadValue();
        Vector2 midpoint = (firstPosition + secondPosition) * 0.5f;
        float distance = Vector2.Distance(firstPosition, secondPosition);

        if (!_isPinching || distance <= Mathf.Epsilon)
        {
            _isPinching = true;
            _previousPinchDistance = distance;
            _previousPinchMidpoint = midpoint;
            return true;
        }

        PanBetweenScreenPoints(_previousPinchMidpoint, midpoint);
        float targetSize = _camera.orthographicSize *
                           (_previousPinchDistance / distance);
        ApplyManualZoom(targetSize, midpoint);

        _previousPinchDistance = distance;
        _previousPinchMidpoint = midpoint;
        return true;
    }

    private void HandleMouseWheelZoom()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f)) return;

        ApplyManualZoom(
            _camera.orthographicSize - Mathf.Sign(scroll) * _displayRoomMouseWheelStep,
            mouse.position.ReadValue());
    }

    private void PanBetweenScreenPoints(Vector2 previous, Vector2 current)
    {
        Vector3 previousWorld = _camera.ScreenToWorldPoint(previous);
        Vector3 currentWorld = _camera.ScreenToWorldPoint(current);
        _camera.transform.position += previousWorld - currentWorld;
    }

    private void ApplyManualZoom(float targetSize, Vector2 anchorScreenPosition)
    {
        float maxSize = GetDisplayRoomFitSize();
        float minSize = Mathf.Min(_displayRoomMinZoomSize, maxSize);
        Vector3 anchorBefore = _camera.ScreenToWorldPoint(anchorScreenPosition);

        _camera.orthographicSize = Mathf.Clamp(targetSize, minSize, maxSize);

        Vector3 anchorAfter = _camera.ScreenToWorldPoint(anchorScreenPosition);
        _camera.transform.position += anchorBefore - anchorAfter;
        ClampDisplayRoomCamera();
    }

    private float GetDisplayRoomFitSize()
    {
        if (_displayRoomArea == null || !_displayRoomArea.HasWalls || _camera.aspect <= 0f)
        {
            return _displayRoomOrthographicSize;
        }

        Rect area = _displayRoomArea.WorldRect;
        return Mathf.Max(
            area.height * 0.5f + _displayRoomFitPadding,
            (area.width * 0.5f + _displayRoomFitPadding) / _camera.aspect);
    }

    private void ClampDisplayRoomCamera()
    {
        if (_displayRoomArea == null || !_displayRoomArea.HasWalls) return;

        Rect area = _displayRoomArea.WorldRect;
        float halfHeight = _camera.orthographicSize;
        float halfWidth = halfHeight * _camera.aspect;
        Vector3 position = _camera.transform.position;

        position.x = ClampAxis(
            position.x,
            area.xMin + halfWidth,
            area.xMax - halfWidth,
            area.center.x);
        position.y = ClampAxis(
            position.y,
            area.yMin + halfHeight,
            area.yMax - halfHeight,
            area.center.y);
        position.z = _cameraBasePosition.z;
        _camera.transform.position = position;
    }

    private static float ClampAxis(float value, float minimum, float maximum, float center)
    {
        return minimum <= maximum ? Mathf.Clamp(value, minimum, maximum) : center;
    }

    // 한 손가락(또는 마우스)으로 빈 곳을 끌면 확대한 화면을 옮긴다.
    //
    // 슬라임이나 UI를 누르고 시작한 손가락은 옮기지 않는다. 슬라임 위에서 시작한 손가락을
    // 옮겨 버리면 뗄 때 Clicker가 그 슬라임을 눌린 것으로 처리한다. 시작할 때 한 번만
    // 판단하는 것도 같은 이유다. 두 손가락 확대 뒤 남은 한 손가락이 갑자기 화면을 끌지 않는다.
    private void HandleSinglePointerPan()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null)
        {
            ResetPan();
            return;
        }

        Vector2 position = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame)
        {
            _isPanning = false;
            _panStartScreen = position;
            _panPreviousScreen = position;
            _isPanCandidate = _clicker != null &&
                              IsZoomed &&
                              !IsPointerOverUi(position) &&
                              !_clicker.HasSelectionTargetAt(position);
            return;
        }

        if (!pointer.press.isPressed)
        {
            ResetPan();
            return;
        }

        if (!_isPanCandidate) return;

        if (!_isPanning)
        {
            float threshold = _displayRoomPanThresholdPixels;
            if ((position - _panStartScreen).sqrMagnitude < threshold * threshold) return;

            // 문턱을 넘은 순간의 위치부터 옮긴다. 시작점부터 옮기면 화면이 한 번 튄다.
            _isPanning = true;
            _panPreviousScreen = position;
            return;
        }

        PanBetweenScreenPoints(_panPreviousScreen, position);
        ClampDisplayRoomCamera();
        _panPreviousScreen = position;
    }

    private void ResetPan()
    {
        _isPanCandidate = false;
        _isPanning = false;
    }

    // 누른 자리에 UI가 하나라도 걸리면 화면 이동이 아니라 UI 입력으로 본다.
    private bool IsPointerOverUi(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        var pointerData = new PointerEventData(eventSystem) { position = screenPosition };
        _uiRaycastResults.Clear();
        eventSystem.RaycastAll(pointerData, _uiRaycastResults);
        return _uiRaycastResults.Count > 0;
    }

    private void ResetPinch()
    {
        _isPinching = false;
        _previousPinchDistance = 0f;
    }

    // 추적 대상을 놓을 때 인터폴레이션도 함께 되돌린다.
    // ?.는 참조 null만 보므로 파괴된 오브젝트를 거르지 못한다.
    // Unity의 == 오버로드를 타도록 명시적으로 비교한다.
    private void ClearFocusTarget()
    {
        if (_displayRoomFocusTarget != null)
        {
            _displayRoomFocusTarget.SetDisplayRoomCameraFocus(false);
        }

        _displayRoomFocusTarget = null;
    }
}

// 도감처럼 화면을 독점하거나 오브젝트를 직접 배치하는 동안에는 같은 포인터의
// 휠·핀치 입력이 카메라까지 전달되지 않아야 한다. 소유자별 요청으로 관리해 한 UI가
// 닫히면서 다른 UI의 잠금까지 풀지 않게 한다.
public static class DisplayRoomCameraInputGate
{
    private static readonly HashSet<object> Owners = new();

    public static bool IsBlocked => Owners.Count > 0;

    public static void Push(object owner)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));

        Owners.Add(owner);
    }

    public static void Release(object owner)
    {
        if (owner == null) return;

        Owners.Remove(owner);
    }
}
