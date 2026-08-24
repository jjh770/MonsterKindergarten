public enum ESlimeLocation
{
    MainStage,
    DisplayRoom,
}

public static class SlimeLocationRules
{
    public static bool IsValid(ESlimeLocation location)
    {
        return location == ESlimeLocation.MainStage ||
               location == ESlimeLocation.DisplayRoom;
    }
}
