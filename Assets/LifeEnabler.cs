using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class LifeEnabler : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        TwoDLifeSystem.Instance.Enabled = true;
        TwoDLifeSystem.Instance.center = transform.position;
        TwoDLifeSystem.Instance.populated = false;
        TwoDLifeSystem.Instance.CreateEntities();
        TwoDLifeSystem.Instance.Clear();
        TwoDLifeSystem.Instance.Cross();
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
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            TwoDLifeSystem.Instance.Cross();
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            TwoDLifeSystem.Instance.Randomize();
        }
        

        if (Input.GetKeyDown(KeyCode.Space))
        {
            TwoDLifeSystem.Instance.Clear();
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            if (TwoDLifeSystem.Instance.delay > 0)
            {
                TwoDLifeSystem.Instance.delay-= Time.deltaTime * 5;
                if (TwoDLifeSystem.Instance.delay < 0)
                {
                    TwoDLifeSystem.Instance.delay = 0;
                }
            }
        }

        
        if (Input.GetKeyDown(KeyCode.P))
        {
            TwoDLifeSystem.Instance.delay+= Time.deltaTime * 5;
        }

    }
}
