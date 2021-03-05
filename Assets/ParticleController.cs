using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

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

    public int direction = 0;

    public float thickness = 1;

    public float[] distances = {200, 500, 800, 1000, 2000};
    Vector3 target;
    Transform camera;
    
    // Start is called before the first frame update
    void Start()
    {
        ParticleSystem.Instance.Enabled = true;
        ParticleSystem.Instance.center = transform.position;
        ParticleSystem.Instance.CreateEntities();
        camera = Camera.main.transform;
        target = camera.position;
        //StartCoroutine(Change());
        Cursor.visible = false;
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

    void ChooseNewDirection()
    {
        int newDir = direction;
        do
        {
             newDir = (int) Random.Range(0,4);
        }
        while(newDir == direction);
        direction = newDir;
    }

    void Randomize()
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
                turnFraction = Random.Range(1, 20);      
                break;
            case 3:
                target.z = distances[Random.Range(0, distances.Length)] ;
                ChooseNewDirection();
                break;                    
        }
    }

    System.Collections.IEnumerator Change()
    {
        while(true)
        {
            yield return new WaitForSeconds(delay);
            Randomize();            
        }
    }
    
    int clickCount = 0;
    float lastClick = 0;
    Coroutine cr = null;
    public float[] newDelays = new float[4];
    public bool open = true;
    // Update is called once per frame
    void Update()
    {
        //camera.position = Vector3.Lerp(camera.position, target, Time.deltaTime);
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Joystick1Button0))
        {
            turnFraction = Random.Range(1, 50);            
        
        }
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Joystick1Button2))
        {
            radius = Random.Range(0.5f, 5f);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Joystick1Button3))
        {
            ChooseNewDirection();
        }

        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Joystick1Button4))
        {
            if (cr != null)
            {
                StopCoroutine(cr);                        
            }
            clickCount ++;            
            float now = Time.time;
            float newDelay = now - lastClick;                   
            lastClick = now;         
            Randomize();
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

        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Joystick1Button5))
        {
            if (cr != null)
            {
                StopCoroutine(cr);                        
            }
            clickCount = 0;
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
            radius += Time.deltaTime * sizeSpeed;
        }
        if (Input.GetAxis("Vertical") < 0)
        {
            radius -= Time.deltaTime * sizeSpeed;
        }


    }
}
