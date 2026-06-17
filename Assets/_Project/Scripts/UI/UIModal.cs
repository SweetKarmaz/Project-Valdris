using UnityEngine;

// Tracks transient "modal" in-world windows that need the mouse cursor — loot
// windows, the merchant shop, etc. (GameUI is separate; it has its own pause +
// cursor handling.) While any modal is open the cursor is freed and gameplay
// input that would conflict (camera look, melee swing, new interactions) is
// suppressed by systems that check IsOpen.
//
// Use a balanced Push()/Pop() pair around each window's open/close.
public static class UIModal
{
    static int _open;

    public static bool IsOpen => _open > 0;

    public static void Push()
    {
        _open++;
        if (_open == 1) SetCursor(free: true);
    }

    public static void Pop()
    {
        if (_open <= 0) return;
        _open--;
        if (_open == 0) RestoreCursor();
    }

    static void RestoreCursor()
    {
        // Don't grab the cursor back if another system still wants it free.
        if (PauseMenuController.IsPaused || GameUI.IsOpen) return;
        SetCursor(free: false);
    }

    static void SetCursor(bool free)
    {
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible   = free;
    }
}
