using System;

public enum EPointTicketPurchaseFailure
{
    None,
    // 졸업식 전이거나 저장할 수 없는 세션이다.
    Unavailable,
    DailyLimit,
    NotEnoughPoints,
}

// 졸업식을 본 뒤 포인트로 뽑기권을 사는 하루 기록이다. 값을 바꾸는 동작은 새 값을 돌려주고, 저장은
// CurrencyManager가 포인트·뽑기권 잔액과 같은 문서에 함께 한다. 그래야 차감, 지급, 횟수가 한 번에 남는다.
//
// 하루는 광고 보상과 같은 한국 시간 0시 경계(AdRewardProgress.GetDayIndex)를 쓰고, DayIndex 0은 "기록 없음"이다.
// 더 큰 날짜만 인정하므로 시계를 뒤로 돌려도 횟수가 다시 늘지 않는다.
public readonly struct PointTicketPurchase
{
    // 한 장 값과 하루에 살 수 있는 장수다. 생산량과 상관없는 절대값이다.
    public const double PricePerTicket = 1e12;
    public const int DailyLimit = 10;

    public int Count { get; }
    public int DayIndex { get; }

    public PointTicketPurchase(int count, int dayIndex)
    {
        if (count < 0 || dayIndex < 0)
        {
            throw new ArgumentException($"뽑기권 구매 기록이 올바르지 않습니다. : 횟수 {count}, 날짜 {dayIndex}");
        }

        Count = count;
        DayIndex = dayIndex;
    }

    // 그 날에 산 장수다. 기록한 날보다 뒤의 날이면 아직 하나도 사지 않은 것이다.
    public int GetCount(int dayIndex) => dayIndex > DayIndex ? 0 : Count;

    public int GetRemaining(int dayIndex) => Math.Max(0, DailyLimit - GetCount(dayIndex));

    public PointTicketPurchase WithPurchased(int dayIndex, int amount)
    {
        return dayIndex > DayIndex
            ? new PointTicketPurchase(amount, dayIndex)
            : new PointTicketPurchase(Count + amount, DayIndex);
    }
}
