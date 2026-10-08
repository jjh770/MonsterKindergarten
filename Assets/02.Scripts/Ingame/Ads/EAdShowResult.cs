public enum EAdShowResult
{
    // 보상 획득 신호가 왔다. 보상은 이때만 지급한다.
    Rewarded,
    // 보상 없이 닫혔다.
    ClosedEarly,
    // 보여 줄 광고가 준비되지 않았다.
    NotReady,
    // 이미 다른 광고를 보여 주는 중이다.
    Busy,
    // 광고를 보여 주다가 실패했다.
    Failed,
}
