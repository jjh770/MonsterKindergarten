using UnityEngine;

public class Clicker : MonoBehaviour
{
    // 목적 : 마우스로 클릭하면 클릭 타겟을 클릭하고 싶다.
    private void Update()
    {
        // 1. 마우스 클릭을 감지한다.
        if (Input.GetMouseButtonDown(0))
        {
            // 2. 클릭 타겟을 클릭했는지 검사한다.
            Vector2 mousePos = Input.mousePosition;
            TryCLick(mousePos);
        }
    }

    private void TryCLick(Vector2 mousePos)
    {
        // 마우스의 스크린 좌표계를 원드 좌표계로 바꿔줄 필요가 있음.
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(mousePos);

        // 3. 맞다면 클릭한다.
        // 3-1. 마우스 좌표가 클릭타겟 위치와 비교했을 때 근처에 있는지
        // 3-2. 마우스 좌표로 가상의 레이저를 쏴서 그 레이저가 클릭타겟과 충돌했는지 체크 (Unity에서 보통 레이캐스트를 더 많이 씀)
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero, 0f);
        if (hit == true)
        {
            IClickable clickable = hit.collider.GetComponent<IClickable>();
            ClickInfo clickInfo = new ClickInfo
            {
                ClickType = EClickType.Manual,
                Damage = GameManager.Instance.ManualDamage,
                Position = hit.point
            };

            clickable?.OnClick(clickInfo);
        }
    }
}
