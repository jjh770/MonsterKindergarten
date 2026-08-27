public static class GameplaySaveGate
{
    private static bool s_isSavingEnabled = true;
    public static bool IsSavingEnabled => s_isSavingEnabled && !IsResetting;
    public static bool IsResetting { get; private set; }
    public static int ResetGeneration { get; private set; }

    public static void SetSavingEnabled(bool isEnabled)
    {
        s_isSavingEnabled = isEnabled;
    }

    public static void BeginReset()
    {
        IsResetting = true;
        // 로그인 화면으로 돌아간 뒤에도 이전 세션의 지연 저장을 폐기한다.
        ResetGeneration++;
    }

    public static void EndReset()
    {
        IsResetting = false;
    }
}
