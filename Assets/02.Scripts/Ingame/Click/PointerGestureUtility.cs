using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public static class PointerGestureUtility
{
    public static bool HasMultipleActiveTouches()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null) return false;

        int activeTouches = 0;
        foreach (TouchControl touch in touchscreen.touches)
        {
            if (!touch.press.isPressed) continue;
            if (++activeTouches >= 2) return true;
        }

        return false;
    }
}
