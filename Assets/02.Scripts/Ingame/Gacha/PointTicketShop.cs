// 졸업식을 본 뒤에만 열리는 포인트 뽑기권 구매다. 졸업 뒤에는 업그레이드가 곧 끝나 포인트를 쓸 곳이 없어지므로,
// 그 포인트로 뽑기를 더 돌려 특별 슬라임을 모으게 한다.
//
// 값과 하루 한도는 PointTicketPurchase가, 차감과 지급과 횟수 저장은 CurrencyManager가 한 문서에서 맡는다.
// 산 뽑기권은 플레이 기록의 "얻은 뽑기권"(SlimeStatus의 누적 수)에 넣지 않는다. 넣으면 한 번의 구매가
// 두 문서에 걸쳐 저장된다.
public static class PointTicketShop
{
    public const int AmountPerPurchase = 1;

    public static bool IsOpen(SlimeManager slimeManager)
    {
        return slimeManager != null && slimeManager.IsInitialized && slimeManager.IsMainEndingSeen;
    }

    public static int GetToday() => AdRewardProgress.GetDayIndex(ServerClock.TrustedUtcNow);

    public static int GetRemainingToday(CurrencyManager currencyManager)
    {
        return currencyManager != null ? currencyManager.PointTicketPurchase.GetRemaining(GetToday()) : 0;
    }

    public static EPointTicketPurchaseFailure TryBuy(SlimeManager slimeManager, CurrencyManager currencyManager)
    {
        if (!IsOpen(slimeManager) || currencyManager == null) return EPointTicketPurchaseFailure.Unavailable;

        return currencyManager.TryBuyTicketsWithPoints(AmountPerPurchase, GetToday());
    }
}
