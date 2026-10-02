namespace TFClassic.Core;

static class Angles
{
    public static float Diff(float a, float b)
    {
        float d = (b - a) % (2 * MathF.PI);
        if (d > MathF.PI) d -= 2 * MathF.PI;
        if (d < -MathF.PI) d += 2 * MathF.PI;
        return d;
    }

    public static float Approach(float cur, float des, float maxStep)
    {
        float d = Diff(cur, des);
        if (MathF.Abs(d) <= maxStep) return des;
        return cur + MathF.Sign(d) * maxStep;
    }
}
