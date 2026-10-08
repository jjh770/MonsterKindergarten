using System;

// 보상형 광고의 하루 횟수와 포인트 부스트 남은 시간이다. 값을 바꾸는 동작은 새 값을 돌려주고, 저장과 알림은
// 이 값을 가진 쪽(SlimeStatus, SlimeManager)이 맡는다.
//
// 하루는 한국 시간(UTC+9) 0시에 바뀐다. DayIndex는 그 날의 번호이고 0은 "기록 없음"이다. 날짜가 과거로
// 돌아간 것처럼 보이면 횟수를 되돌리지 않는다. 더 큰 날짜만 인정하므로 시계를 뒤로 돌려도 횟수가 늘지 않는다.
public readonly struct AdRewardProgress
{
    // 쓰는 쪽에서 나올 수 없는 값을 가르는 절대 상한이다. 광고 보상 표의 상한보다 넉넉하게 잡아, 표를 낮춰도
    // 저장이 막히지 않고 쓰는 쪽이 표의 상한으로 잘라서 쓴다.
    public const double MaxStoredBoostSeconds = 86400d;

    private static readonly TimeSpan s_koreaOffset = TimeSpan.FromHours(9);
    private static readonly DateTime s_unixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public double PointBoostRemainingSeconds { get; }
    public int PointBoostCount { get; }
    public int TicketCount { get; }
    public int DayIndex { get; }

    public AdRewardProgress(
        double pointBoostRemainingSeconds,
        int pointBoostCount,
        int ticketCount,
        int dayIndex)
    {
        if (double.IsNaN(pointBoostRemainingSeconds) ||
            pointBoostRemainingSeconds < 0d ||
            pointBoostRemainingSeconds > MaxStoredBoostSeconds)
        {
            throw new ArgumentException(
                $"포인트 부스트 남은 시간이 올바르지 않습니다. : {pointBoostRemainingSeconds}");
        }

        if (pointBoostCount < 0 || ticketCount < 0 || dayIndex < 0)
        {
            throw new ArgumentException(
                $"횟수나 날짜가 올바르지 않습니다. : 부스트 {pointBoostCount}, 뽑기권 {ticketCount}, 날짜 {dayIndex}");
        }

        PointBoostRemainingSeconds = pointBoostRemainingSeconds;
        PointBoostCount = pointBoostCount;
        TicketCount = ticketCount;
        DayIndex = dayIndex;
    }

    public bool HasPointBoost => PointBoostRemainingSeconds > 0d;

    // 한국 시간 0시를 경계로 센 날짜 번호다. 신뢰할 수 있는 UTC 시각을 넣는다.
    public static int GetDayIndex(DateTime utcNow)
    {
        double days = (utcNow + s_koreaOffset - s_unixEpoch).TotalDays;
        return (int)Math.Floor(days);
    }

    // 그 날에 센 횟수다. 기록한 날보다 뒤의 날이면 아직 하나도 보지 않은 것이다.
    public int GetTicketCount(int dayIndex) => dayIndex > DayIndex ? 0 : TicketCount;

    public int GetPointBoostCount(int dayIndex) => dayIndex > DayIndex ? 0 : PointBoostCount;

    public AdRewardProgress WithTicketRecorded(int dayIndex)
    {
        AdRewardProgress today = RolledTo(dayIndex);
        return new AdRewardProgress(
            today.PointBoostRemainingSeconds,
            today.PointBoostCount,
            today.TicketCount + 1,
            today.DayIndex);
    }

    // 뽑기권 지급이 실패했을 때 방금 센 횟수를 되돌린다.
    public AdRewardProgress WithTicketRecordUndone()
    {
        return new AdRewardProgress(
            PointBoostRemainingSeconds,
            PointBoostCount,
            Math.Max(0, TicketCount - 1),
            DayIndex);
    }

    public AdRewardProgress WithPointBoostAdded(int dayIndex, double seconds, double maxRemainingSeconds)
    {
        AdRewardProgress today = RolledTo(dayIndex);
        double remaining = Math.Min(today.PointBoostRemainingSeconds + seconds, maxRemainingSeconds);
        return new AdRewardProgress(
            remaining,
            today.PointBoostCount + 1,
            today.TicketCount,
            today.DayIndex);
    }

    public AdRewardProgress WithPointBoostElapsed(double seconds)
    {
        if (seconds <= 0d || PointBoostRemainingSeconds <= 0d) return this;

        return new AdRewardProgress(
            Math.Max(0d, PointBoostRemainingSeconds - seconds),
            PointBoostCount,
            TicketCount,
            DayIndex);
    }

    private AdRewardProgress RolledTo(int dayIndex)
    {
        if (dayIndex <= DayIndex) return this;

        return new AdRewardProgress(PointBoostRemainingSeconds, 0, 0, dayIndex);
    }
}
