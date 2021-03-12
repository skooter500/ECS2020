using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CornerCamera : MonoBehaviour
{
    public NoiseCube noiseCube; 
    public Transform cam;
    public float baseHeight = 50;
    public Vector3 target;

    public float height = 0;

    float baseLength;

    float targetNoiseScale;

    // Start is called before the first frame update
    void Start()
    {
        float corner  = (noiseCube.size / 2) + 0;
        height = baseHeight;
        target = new Vector3(corner, height, corner);
        baseLength = target.magnitude;
        targetNoiseScale = noiseCube.noiseScale;  
    }

    // Update is called once per frame
    void Update()
    {
        target.y = height;
        cam.transform.position = Vector3.Lerp(cam.transform.position, target, Time.deltaTime);
        cam.transform.rotation = Quaternion.LookRotation(- cam.transform.position);

        //noiseCube.noiseScale = Mathf.Lerp(noiseCube.noiseScale, targetNoiseScale, Time.deltaTime * 0.1f);

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Joystick1Button0))
        {
            target = Quaternion.AngleAxis(90, Vector3.up) * target;
            //height = baseHeight + (Random.Range(-.5f, .5f) * baseHeight);
            //targetNoiseScale = Random.Range(0.02f, 0.07f);
        }
    }
}
