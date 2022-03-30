using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoiseCell1 : MonoBehaviour
{
    float d;

    public void Update()
    {
        d += Time.deltaTime;

        float height = (30 * 0.2f) + (30 * Perlin.Noise((transform.position.x + d) * 0.1f, 0, (transform.position.z + d) * 0.1f));
        transform.localScale = new Vector3(1, height, 1);
    }
}
