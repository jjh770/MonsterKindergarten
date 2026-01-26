using UnityEngine;

public class Clicker : MonoBehaviour
{
    private ClickTarget _draggingTarget;
    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TryStartDrag();
        }
        else if (Input.GetMouseButton(0) && _draggingTarget != null)
        {
            UpdateDrag();
        }
        else if (Input.GetMouseButtonUp(0) && _draggingTarget != null)
        {
            EndDrag();
        }
    }

    private void TryStartDrag()
    {
        Vector2 worldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero, 0f);

        if (hit)
        {
            ClickTarget clickTarget = hit.collider.GetComponent<ClickTarget>();
            if (clickTarget != null)
            {
                // 클릭 피드백 실행
                ClickInfo clickInfo = new ClickInfo
                {
                    ClickType = EClickType.Manual,
                    Damage = GameManager.Instance.ManualDamage,
                    Position = hit.point
                };
                clickTarget.OnClick(clickInfo);

                // 드래그 시작
                _draggingTarget = clickTarget;
                _draggingTarget.StartDrag();
            }
        }
    }

    private void UpdateDrag()
    {
        Vector2 mousePos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        _draggingTarget.transform.position = mousePos;
    }

    private void EndDrag()
    {
        _draggingTarget.EndDrag();
        _draggingTarget = null;
    }
}
