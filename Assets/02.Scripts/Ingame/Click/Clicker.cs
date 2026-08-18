using UnityEngine;
using UnityEngine.InputSystem;

public class Clicker : MonoBehaviour
{
    [SerializeField] private float _dragThresholdTime = 0.2f;
    [SerializeField] private float _dragThresholdDistance = 0.3f;
    [SerializeField] private float _mergeDetectionRadius = 0.65f;

    [Header("Drag Bounds")]
    [SerializeField] private Vector2 _dragMinBounds = new Vector2(-5f, -3f);
    [SerializeField] private Vector2 _dragMaxBounds = new Vector2(5f, 3f);

    private SlimeController _selectedTarget;
    private SlimeController _mergeCandidate;
    private Camera _mainCamera;
    private Vector2 _mouseDownPos;
    private float _mouseDownTime;
    private bool _isDragging;
    private bool _isClickEnabled = true;
    private bool _isDragEnabled = true;
    private SlimeController _restrictedTarget;
    private SlimeController _secondaryRestrictedTarget;

    public event System.Action<SlimeController> MergeCandidateChanged;
    public event System.Action<SlimeController> TargetClicked;
    public event System.Action<SlimeController> TargetDragCompleted;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsGameplayActive)
        {
            CancelSelection();
            return;
        }

        Pointer pointer = Pointer.current;
        if (pointer == null || _mainCamera == null) return;

        Vector2 pointerPosition = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame)
        {
            TrySelect(pointerPosition);
        }
        else if (pointer.press.isPressed && _selectedTarget != null)
        {
            CheckDragStart(pointerPosition);
            if (_isDragging)
            {
                UpdateDrag(pointerPosition);
            }
        }
        else if (pointer.press.wasReleasedThisFrame && _selectedTarget != null)
        {
            OnPointerUp(pointerPosition);
        }
    }

    private void OnDisable()
    {
        CancelSelection();
    }

    private void TrySelect(Vector2 pointerPosition)
    {
        if (!_isClickEnabled && !_isDragEnabled) return;

        Vector2 worldPos = _mainCamera.ScreenToWorldPoint(pointerPosition);
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero, 0f);

        if (hit)
        {
            SlimeController clickTarget = hit.collider.GetComponent<SlimeController>();
            if (clickTarget != null &&
                (_restrictedTarget == null ||
                 clickTarget == _restrictedTarget ||
                 clickTarget == _secondaryRestrictedTarget))
            {
                _selectedTarget = clickTarget;
                _mouseDownPos = worldPos;
                _mouseDownTime = Time.time;
                _isDragging = false;
            }
        }
    }

    private void CheckDragStart(Vector2 pointerPosition)
    {
        if (!_isDragEnabled || _isDragging) return;

        Vector2 currentPos = _mainCamera.ScreenToWorldPoint(pointerPosition);
        float distance = Vector2.Distance(_mouseDownPos, currentPos);
        float heldTime = Time.time - _mouseDownTime;

        // 일정 거리 이상 이동하거나 일정 시간 이상 누르면 드래그 시작
        if (distance > _dragThresholdDistance || heldTime > _dragThresholdTime)
        {
            _isDragging = true;
            _selectedTarget.StartDrag();
        }
    }

    private void UpdateDrag(Vector2 pointerPosition)
    {
        Vector2 mousePos = _mainCamera.ScreenToWorldPoint(pointerPosition);

        mousePos.x = Mathf.Clamp(mousePos.x, _dragMinBounds.x, _dragMaxBounds.x);
        mousePos.y = Mathf.Clamp(mousePos.y, _dragMinBounds.y, _dragMaxBounds.y);

        _selectedTarget.transform.position = mousePos;
        UpdateMergeCandidate();
    }

    private void OnPointerUp(Vector2 pointerPosition)
    {
        SlimeController selectedTarget = _selectedTarget;

        if (_isDragging)
        {
            // 릴리스 프레임의 포인터 위치까지 반영한 뒤 표시된 대상을 우선 합성한다.
            UpdateDrag(pointerPosition);
            _selectedTarget.EndDrag(_mergeCandidate);

            Vector2 releaseWorldPosition = _mainCamera.ScreenToWorldPoint(pointerPosition);
            if (Vector2.Distance(_mouseDownPos, releaseWorldPosition) >= _dragThresholdDistance)
            {
                TargetDragCompleted?.Invoke(selectedTarget);
            }
        }
        else if (_isClickEnabled)
        {
            // 클릭 처리 - ClickTarget의 레벨별 포인트 사용
            ClickInfo clickInfo = new ClickInfo
            {
                ClickType = EClickType.Manual,
                Point = PointCalculator.Calculate(_selectedTarget.Point, _selectedTarget.Grade, EClickType.Manual),
                Position = _mouseDownPos,
                Grade = _selectedTarget.Grade
            };
            _selectedTarget.OnClick(clickInfo);
            TargetClicked?.Invoke(selectedTarget);
        }

        SetMergeCandidate(null);
        _selectedTarget = null;
        _isDragging = false;
    }

    public void SetInputMode(
        bool clickEnabled,
        bool dragEnabled,
        SlimeController restrictedTarget = null,
        SlimeController secondaryRestrictedTarget = null)
    {
        CancelSelection();
        _isClickEnabled = clickEnabled;
        _isDragEnabled = dragEnabled;
        _restrictedTarget = restrictedTarget;
        _secondaryRestrictedTarget = secondaryRestrictedTarget;
    }

    private void UpdateMergeCandidate()
    {
        SlimeController nearestCandidate = null;
        float nearestDistanceSqr = float.MaxValue;
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            _selectedTarget.transform.position,
            Mathf.Max(0f, _mergeDetectionRadius));

        foreach (Collider2D hit in hits)
        {
            SlimeController candidate = hit.GetComponent<SlimeController>();
            if (!_selectedTarget.CanMergeWith(candidate)) continue;

            float distanceSqr = (
                candidate.transform.position - _selectedTarget.transform.position).sqrMagnitude;
            if (distanceSqr >= nearestDistanceSqr) continue;

            nearestCandidate = candidate;
            nearestDistanceSqr = distanceSqr;
        }

        SetMergeCandidate(nearestCandidate);
    }

    private void SetMergeCandidate(SlimeController candidate)
    {
        if (_mergeCandidate == candidate) return;

        _mergeCandidate = candidate;
        MergeCandidateChanged?.Invoke(candidate);
    }

    private void CancelSelection()
    {
        SetMergeCandidate(null);

        if (_selectedTarget != null && _isDragging)
        {
            _selectedTarget.CancelDrag();
        }

        _selectedTarget = null;
        _isDragging = false;
    }
}
