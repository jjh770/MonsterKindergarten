public static class GameplaySaveGate
{
    public static bool IsSavingEnabled { get; private set; } = true;

    public static void SetSavingEnabled(bool isEnabled)
    {
        IsSavingEnabled = isEnabled;
    }
}
