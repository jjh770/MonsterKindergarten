public enum ESlimeLocation
{
    MainField,
    DisplayRoom,
}

public static class SlimeLocationRules
{
    public static bool IsValid(ESlimeLocation location)
    {
        return location == ESlimeLocation.MainField ||
               location == ESlimeLocation.DisplayRoom;
    }
}
