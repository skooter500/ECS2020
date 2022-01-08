using UnityEngine;
using UnityEditor;

public class Utilities
{

    public const float TWO_PI = Mathf.PI * 2.0f;
    public static Color RandomColor()
    {
        return Color.HSVToRGB(Random.Range(0.0f, 1.0f), 1, 1);
    }

    public static void RecursiveSetColor(GameObject boid, Color color)
        {
            if (boid != null)
            {
                Renderer renderer = boid.GetComponent<Renderer>();
                if (renderer != null && ! renderer.materials[0].name.Contains("Trans"))
                {
                    renderer.material.color = color;
                }

                for (int j = 0; j < boid.transform.childCount; j++)
                {
                    RecursiveSetColor(boid.transform.GetChild(j).gameObject, color);
                }
            }            
        }        

    public static void SetLayerRecursively(GameObject obj, int newLayer)
        {
            if (null == obj)
            {
                return;
            }

            obj.layer = newLayer;

            foreach (Transform child in obj.transform)
            {
                if (null == child)
                {
                    continue;
                }
                SetLayerRecursively(child.gameObject, newLayer);
            }
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