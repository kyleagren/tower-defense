using Godot;
using System.Collections.Generic;

public static class SigilGenerator
{
    public const int AnchorCount = 30;
    public const int LetterAnchorCount = 26;

    public static Vector2[] GeneratePoints(string name, Vector2 center, float radius)
    {
        List<Vector2> points = new List<Vector2>();

        foreach (char c in name)
        {
            if (!char.IsLetter(c))
            {
                continue;
            }

            int anchorIndex = char.ToUpperInvariant(c) - 'A';
            if (anchorIndex < 0 || anchorIndex >= LetterAnchorCount)
            {
                continue;
            }

            points.Add(GetAnchorPosition(anchorIndex, center, radius));
        }

        return points.ToArray();
    }

    public static Vector2 GetAnchorPosition(int index, Vector2 center, float radius)
    {
        float angleDegrees = -90f + index * (360f / AnchorCount);
        float angleRadians = Mathf.DegToRad(angleDegrees);
        return center + new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians)) * radius;
    }
}
