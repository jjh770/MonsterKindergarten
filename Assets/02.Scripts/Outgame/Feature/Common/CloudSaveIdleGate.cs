// 입력이 끊겼을 때 클라우드 쓰기 간격을 늘린다.
//
// 자동 생산은 손을 대지 않아도 재화를 바꾸므로, 화면을 켜 둔 채 방치하면 입력과 상관없이
// 쓰기가 계속 나간다. 방치 중에는 간격만 늘리고 쓰기를 막지는 않는다. 막으면 방치가 길어질수록
// 클라우드가 뒤처지는 폭이 제한 없이 커진다.
//
// 로컬 저장은 이 게이트와 무관하게 매번 즉시 일어난다. 뒤처지는 것은 클라우드 사본뿐이다.
// 입력을 보는 쪽(GameManager)이 방치 여부를 바꾸고, 상태가 바뀌는 순간 미뤄 둔 쓰기를 내보낸다.
public static class CloudSaveIdleGate
{
    // 입력이 이만큼 없으면 방치로 본다.
    public const float IdleThresholdSeconds = 60f;
    // 평소의 클라우드 쓰기 간격이다. 클릭과 자동 생산이 바꾼 값을 5초에 한 번 올린다.
    public const float ActiveIntervalSeconds = 5f;
    // 방치 중의 간격이다. 재설치나 기기 변경 때 이만큼의 자동 생산이 되돌아갈 수 있다.
    public const float IdleIntervalSeconds = 300f;

    public static bool IsIdle { get; private set; }

    public static float CurrentIntervalSeconds =>
        IsIdle ? IdleIntervalSeconds : ActiveIntervalSeconds;

    // 상태가 실제로 바뀌었으면 true를 돌려준다.
    public static bool SetIdle(bool isIdle)
    {
        if (IsIdle == isIdle) return false;

        IsIdle = isIdle;
        return true;
    }
}
