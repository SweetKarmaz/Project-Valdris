using UnityEngine;

public static class GameExtensions
{
    public static float Remap(this float value, float fromMin, float fromMax, float toMin, float toMax)
        => toMin + (value - fromMin) / (fromMax - fromMin) * (toMax - toMin);

    public static bool InRange(this float value, float min, float max) => value >= min && value <= max;

    public static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0f, v.z);

    public static float FlatDistance(this Vector3 a, Vector3 b) => Vector3.Distance(a.Flat(), b.Flat());
}
