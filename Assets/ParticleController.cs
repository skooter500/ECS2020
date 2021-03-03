using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class ParticleController : MonoBehaviour
{

    [Range(0,1000)]
    public float turnFraction = 1.618034f;
    [Range(0,5)]
    public float radius = 0.038f;

    [Range(0,5)]
    public float speed = 1;

    public float spacer = 50;
    
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
            ParticleSystem.Instance.Enabled = false;
        }
    }

    float turnSpeed = 0.2f;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            turnFraction = Random.Range(0.01f, 5.0f);            
        }
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            radius = Random.Range(0.001f, 0.2f);
        }
        
        if (Input.GetAxis("Horizontal") > 0)
        {
            turnFraction += Time.deltaTime * turnSpeed;
        }
        if (Input.GetAxis("Horizontal") < 0)
        {
            turnFraction -= Time.deltaTime * turnSpeed;
        }

        if (Input.GetAxis("Vertical") > 0)
        {
            radius += Time.deltaTime * turnSpeed;
        }
        if (Input.GetAxis("Vertical") < 0)
        {
            radius -= Time.deltaTime * turnSpeed;
        }


    }
}
