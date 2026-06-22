// Global gate for scripted sequences that need to take movement away from the
// player while leaving mouse-look intact (forced dialogues, escorts). Movement
// is suppressed in PlayerController.Update; the look camera is a separate
// component and keeps running. Balanced Lock()/Unlock() (ref-counted) so nested
// steps don't release control early.
public static class CutsceneControl
{
    static int _locks;

    public static bool MovementLocked => _locks > 0;

    public static void Lock()   => _locks++;
    public static void Unlock() { if (_locks > 0) _locks--; }
    public static void ForceClear() => _locks = 0;   // safety on scene change / new game
}
