using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;

public class Tut1 : MonoBehaviour
{
    private EntityManager entityManager;

    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;
    // Start is called before the first frame update
    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager; 

        EntityArchetype archetype = entityManager.CreateArchetype(
		typeof(Translation),
		typeof(Rotation),
		typeof(RenderMesh),
                typeof(RenderBounds),
		typeof(LocalToWorld));
 
	    Entity entity = entityManager.CreateEntity(archetype);

	
	    entityManager.AddComponentData(entity, new Translation { Value = new float3(-3f, 0.5f, 5f) });

	    entityManager.AddComponentData(entity, new Rotation { Value = quaternion.EulerXYZ(new float3(0f, 45f, 0f)) });

	    entityManager.AddSharedComponentData(entity, new RenderMesh 
		{
			mesh = mesh,
			material = material
		});
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
