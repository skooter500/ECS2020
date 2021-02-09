using UnityEngine;
using UnityEditor;

public class Utilities
{
    public static Color RandomColor()
    {
        return Color.HSVToRGB(Random.Range(0.0f, 1.0f), 1, 1);
    }

    public static float Map(float value, float r1, float r2, float m1, float m2)
    {
        float dist = value - r1;
        float range1 = r2 - r1;
        float range2 = m2 - m1;
        return m1 + ((dist / range1) * range2);
    }

    static public bool checkNaN(Quaternion v)
    {
        if (float.IsNaN(v.x) || float.IsInfinity(v.x))
        {
            return true;
        }
        if (float.IsNaN(v.y) || float.IsInfinity(v.y))
        {
            return true;
        }
        if (float.IsNaN(v.z) || float.IsInfinity(v.z))
        {
            return true;
        }
        if (float.IsNaN(v.w) || float.IsInfinity(v.w))
        {
            return true;
        }
        return false;
    }

    static public bool checkNaN(Vector3 v)
    {
        if (float.IsNaN(v.x) || float.IsInfinity(v.x))
        {
            return true;
        }
        if (float.IsNaN(v.y) || float.IsInfinity(v.y))
        {
            return true;
        }
        if (float.IsNaN(v.z) || float.IsInfinity(v.z))
        {
            return true;
        }
        return false;
    }


}