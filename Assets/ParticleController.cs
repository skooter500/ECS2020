using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class ParticleController : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        ParticleSystem.Instance.Enabled = true;
        ParticleSystem.Instance.center = transform.position;
        ParticleSystem.Instance.CreateEntities();
    }

    public void OnDestroy()
    {
        Debug.Log("OnDestroy LifeEnabler");
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            TwoDLifeSystem.Instance.Enabled = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
