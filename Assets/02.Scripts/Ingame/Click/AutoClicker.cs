using UnityEngine;

// 자동 생산의 대상 판정과 진행을 한곳에서 돌린다.
//
// 주기 자체는 각 슬라임이 들고 있다. 슬라임은 풀에서 재사용되므로 타이머를 여기서
// 개체별로 들고 있으면 수명을 따로 맞춰 줘야 하고, 그 정리 코드가 곧 비용이 된다.
// 반면 대상 규칙과 일시정지는 여기 남는다. 튜토리얼이 한 번에 멈출 수 있어야 하고,
// GameManager의 오프라인 보상이 같은 대상 규칙을 재현하기 때문이다.
public class AutoClicker : MonoBehaviour
{
    private bool _isPaused;

    private void Update()
    {
        if (_isPaused) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsGameplayActive) return;
        if (SpawnManager.Instance == null) return;

        // 순회 중에는 슬라임을 만들거나 없애지 않는다. 활성 목록이 바뀌면 예외가 난다.
        foreach (SlimeController target in SpawnManager.Instance.GetActiveTargets())
        {
            if (target == null ||
                target.IsDragging ||
                target.Location != ESlimeLocation.MainStage)
            {
                continue;
            }

            if (target.TickAutoProduction(Time.deltaTime))
            {
                AutoClick(target);
            }
        }
    }

    private void AutoClick(SlimeController target)
    {
        ClickInfo clickInfo = new ClickInfo
        {
            ClickType = EClickType.Auto,
            Position = target.transform.position,
            Point = PointCalculator.Calculate(target.Point, target.Grade, EClickType.Auto),
            Grade = target.Grade
        };

        target.OnClick(clickInfo);
    }

    public void SetPaused(bool isPaused)
    {
        _isPaused = isPaused;
    }
}
