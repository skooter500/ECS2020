using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class NoiseSpawner1 : MonoBehaviour
{
    public int size = 10;
    public GameObject Prefab;

    public float gap = 0.1f;
    // Start is called before the first frame update
    void Start()
    {
        // Create entity prefab from the game object hierarchy once
        var settings = GameObjectConversionSettings.FromWorld(World.DefaultGameObjectInjectionWorld,  new BlobAssetStore());
        var prefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(Prefab, settings);
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        int half = size / 2;
        for (int x = -half ; x < half ; x ++)
        {
            for (int y = -half ; y < half ; y ++)
            {
                for (int z = -half ; z < half ; z ++)
                {
                    var instance = entityManager.Instantiate(prefab);

                    // Place the instantiated entity in a grid with some noise
                    var position = transform.TransformPoint(new float3(x * (1 + gap), y * (1 + gap), z * (1 + gap)));
                    entityManager.SetComponentData(instance, new Translation {Value = position});   
                    entityManager.AddComponentData(instance, new Scale {Value = 1});   
                                     
                }
            }
        }

        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
