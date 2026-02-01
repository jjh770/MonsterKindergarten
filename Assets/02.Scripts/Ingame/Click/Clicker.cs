using UnityEngine;

public class Clicker : MonoBehaviour
{
    [SerializeField] private float _dragThresholdTime = 0.2f;
    [SerializeField] private float _dragThresholdDistance = 0.3f;

    [Header("Drag Bounds")]
    [SerializeField] private Vector2 _dragMinBounds = new Vector2(-5f, -3f);
    [SerializeField] private Vector2 _dragMaxBounds = new Vector2(5f, 3f);

    private Slime _selectedTarget;
    private Camera _mainCamera;
    private Vector2 _mouseDownPos;
    private float _mouseDownTime;
    private bool _isDragging;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TrySelect();
        }
        else if (Input.GetMouseButton(0) && _selectedTarget != null)
        {
            CheckDragStart();
            if (_isDragging)
            {
                UpdateDrag();
            }
        }
        else if (Input.GetMouseButtonUp(0) && _selectedTarget != null)
        {
            OnMouseUp();
        }
    }

    private void TrySelect()
    {
        Vector2 worldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero, 0f);

        if (hit)
        {
            Slime clickTarget = hit.collider.GetComponent<Slime>();
            if (clickTarget != null)
            {
                _selectedTarget = clickTarget;
                _mouseDownPos = worldPos;
                _mouseDownTime = Time.time;
                _isDragging = false;
            }
        }
    }

    private void CheckDragStart()
    {
        if (_isDragging) return;

        Vector2 currentPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        float distance = Vector2.Distance(_mouseDownPos, currentPos);
        float heldTime = Time.time - _mouseDownTime;

        // 일정 거리 이상 이동하거나 일정 시간 이상 누르면 드래그 시작
        if (distance > _dragThresholdDistance || heldTime > _dragThresholdTime)
        {
            _isDragging = true;
            _selectedTarget.StartDrag();
        }
    }

    private void UpdateDrag()
    {
        Vector2 mousePos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);

        mousePos.x = Mathf.Clamp(mousePos.x, _dragMinBounds.x, _dragMaxBounds.x);
        mousePos.y = Mathf.Clamp(mousePos.y, _dragMinBounds.y, _dragMaxBounds.y);

        _selectedTarget.transform.position = mousePos;
    }

    private void OnMouseUp()
    {
        if (_isDragging)
        {
            // 드래그 종료
            _selectedTarget.EndDrag();
        }
        else
        {
            // 클릭 처리 - ClickTarget의 레벨별 포인트 사용
            ESlimeGrade grade = (ESlimeGrade)_selectedTarget.Level;
            ClickInfo clickInfo = new ClickInfo
            {
                ClickType = EClickType.Manual,
                Point = PointCalculator.Calculate(_selectedTarget.Point, grade, EClickType.Manual),
                Position = _mouseDownPos,
                Level = _selectedTarget.Level
            };
            _selectedTarget.OnClick(clickInfo);
        }

        _selectedTarget = null;
        _isDragging = false;
    }
}
