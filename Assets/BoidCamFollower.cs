using UnityEngine;


public class BoidCamFollower:MonoBehaviour
{
    public void FixedUpdate()
    {
        transform.position = ew.BoidJobSystem.Instance.CamPosition;
        transform.rotation = ew.BoidJobSystem.Instance.CamRotation;
    }
}
