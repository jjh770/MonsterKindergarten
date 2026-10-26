using System;

// 보상은 출발 시 정해 둔다. 귀환 화면을 다시 열거나 재접속해도 다시 추첨하지 않는다.
public sealed class ExpeditionReward
{
    public double Points { get; }
    public int GachaTickets { get; }
    public EPlaygroundObjectType? Decoration { get; }

    public ExpeditionReward(
        double points,
        int gachaTickets,
        EPlaygroundObjectType? decoration = null)
    {
        if (double.IsNaN(points) || double.IsInfinity(points) ||
            points < 0d || points != Math.Floor(points))
        {
            throw new ArgumentOutOfRangeException(nameof(points), "탐험 포인트는 유한한 정수여야 합니다.");
        }
        if (gachaTickets < 0)
            throw new ArgumentOutOfRangeException(nameof(gachaTickets));
        if (decoration.HasValue && !PlaygroundRules.IsDecoration(decoration.Value))
            throw new ArgumentOutOfRangeException(nameof(decoration));

        Points = points;
        GachaTickets = gachaTickets;
        Decoration = decoration;
    }
}
