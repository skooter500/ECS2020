using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class LifeEnabler : MonoBehaviour
{
    public GameObject cubePrefab; 

    // Start is called before the first frame update
    void Start()
    {
        LifeSystem.Instance.Enabled = true;
        LifeSystem.Instance.center = transform.position;
        LifeSystem.Instance.CreateEntities();
        LifeSystem.Instance.InitialState();
    }

    public void OnDestroy()
    {
        Debug.Log("OnDestroy LifeEnabler");
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            LifeSystem.Instance.Enabled = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            LifeSystem.Instance.Clear();
            LifeSystem.Instance.InitialState();
            LifeSystem.Instance.populated = false;
        }
        
    }
}
