namespace Repl.Server.Core.MathUtils;

public static class MathExtension
{
    public const float EPSILON = 0.0001f;
    
    public static int GCD(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x, nameof(x));
        ArgumentOutOfRangeException.ThrowIfNegative(y, nameof(y));
        while (y != 0)
        {
            int temp = y;
            y = x % y;
            x = temp;
        }
        return x;
    }

    public static int LCM(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x, nameof(x));
        ArgumentOutOfRangeException.ThrowIfNegative(y, nameof(y));
        int gcd = GCD(x, y);
        return x * y / gcd;
    }

    public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
    {
        return new Vector2();
    }
    
    public static float Lerp(float a, float b, float t)
    {
        return 0.0f;
    }
    
    public static bool IsEqualApprox(float a, float b)
    {
        return MathF.Abs(a - b) < EPSILON;
    }

    public static bool IsZeroApprox(float a)
    {
        return a <= EPSILON;
    }
}
