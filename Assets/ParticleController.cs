using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

public class ParticleController : MonoBehaviour
{

    float delay = 1.0f;
    [Range(0,1000)]
    public float turnFraction = 1.618034f;
    [Range(0,5)]
    public float radius = 0.038f;

    [Range(0,5)]
    public float speed = 1;

    public float spacer = 50;

    public bool direction = true;

    public float thickness = 1;
    
    // Start is called before the first frame update
    void Start()
    {
        ParticleSystem.Instance.Enabled = true;
        ParticleSystem.Instance.center = transform.position;
        ParticleSystem.Instance.CreateEntities();
        //StartCoroutine(Change());
    }

    public void OnDestroy()
    {
        Debug.Log("OnDestroy LifeEnabler");
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            ParticleSystem.Instance.Enabled = false;
        }
    }

    float turnSpeed = 1.0f;
    float sizeSpeed = 0.001f;

    System.Collections.IEnumerator Change()
    {
        while(true)
        {
            int which = (int) Random.Range(0,4);
            switch(which)
            {
                case 0:
                    turnFraction = Random.Range(1, 20);        
                    break;
                case 1:
                    turnFraction = Random.Range(1, 20);              
                    break;
                case 2:
                    radius = Random.Range(0.5f, 5f);     
                    break;
                case 3:
                    direction = ! direction;
                    break;                    
            }
            yield return new WaitForSeconds(delay);
        }
    }
    
    int clickCount = 0;
    float lastClick = 0;
    Coroutine cr = null;
    public float[] newDelays = new float[3];
    // Update is called once per frame
    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            turnFraction = Random.Range(1, 50);            
        
        }
        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            radius = Random.Range(0.5f, 5f);
        }
        if (keyboard.digit2Key.wasPressedThisFrame)
        {
            direction = ! direction;
        }

        if (keyboard.digit3Key.wasPressedThisFrame)
        {
            if (cr != null)
            {
                StopCoroutine(cr);                        
            }
            clickCount ++;            
            float now = Time.time;
            float newDelay = now - lastClick;                   
            lastClick = now;         
            
            if (clickCount > 1)
            {
                newDelays[clickCount - 2] = newDelay;
                if (clickCount == 4)
                {
                    float sum = 0;
                    foreach(float d in newDelays)                    
                    {
                        sum += d;
                    }
                    clickCount = 0;                    
                    delay = sum / 3.0f;
                    cr = StartCoroutine(Change());
                }
            }
            
        }
        
        /*        
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
            radius += Time.deltaTime * sizeSpeed;
        }
        if (Input.GetAxis("Vertical") < 0)
        {
            radius -= Time.deltaTime * sizeSpeed;
        }
        */

    }
}
