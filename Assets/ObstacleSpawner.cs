using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    private EntityManager entityManager;
    public GameObject prefab;
    private Entity entityPrefab;

    public int count = 5;
    public float radius;
    // Start is called before the first frame update
    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        var settings = GameObjectConversionSettings.FromWorld(World.DefaultGameObjectInjectionWorld, new BlobAssetStore());
        entityPrefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(prefab, settings);

        for(int i = 0 ; i < count ; i ++)
        {
            var instance = entityManager.Instantiate(entityPrefab);

            var position = transform.TransformPoint(UnityEngine.Random.insideUnitSphere * radius);
            entityManager.AddComponentData(instance, new Translation {Value = position});   
            entityManager.AddComponentData(instance, new Scale {Value = UnityEngine.Random.Range(300, 500)});
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
