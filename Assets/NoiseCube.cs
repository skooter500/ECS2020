using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Jobs;
using Unity.Collections;

struct NoiseCell:IComponentData
{
    int x;
}

public class NoiseCube : MonoBehaviour
{
    private EntityManager entityManager;

    public Mesh mesh;
    public Material material;

    public int size = 500;

    public float noiseScale = 0.01f;
    public float scale = 10;

    public float speed = 1.0f;

    public EntityArchetype archetype;

    NoiseSystem noiseSystem;
    

    // Start is called before the first frame update
    void Start()
    {
        /*     
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;         

        archetype = entityManager.CreateArchetype(
            typeof(Translation),
            typeof(Rotation),
            typeof(RenderMesh),
            typeof(NonUniformScale),
            typeof(RenderBounds),
            typeof(LocalToWorld),
            typeof(NoiseCell)
        );
 
        float start = Time.realtimeSinceStartup;
        // Create the entities here
        int halfSize = size / 2;
        for(int i = 0 ; i < size * size ; i ++)
        {
            int row = i / (size);
            int col = i - (row * size);

    	    Entity entity = entityManager.CreateEntity(archetype);

            // Optionally set the component data here
            // Not really necessary because we do it in the job anyway
            entityManager.AddComponentData(entity, new Translation { Value = new float3(row - halfSize, 0, col - halfSize) });
	        entityManager.AddComponentData(entity, new Rotation { Value = Quaternion.identity});
            entityManager.AddComponentData(entity, new NonUniformScale{Value = new float3(1,1,1)});

            entityManager.AddSharedComponentData(entity, new RenderMesh 
            {
                mesh = mesh,
                material = material
            });

            float ellapsed = Time.realtimeSinceStartup - start;
            Debug.Log("Creating " + (size * size) + " entities took " + ellapsed + " seconds");
            
        }   
        */  

        NoiseSystem.Instance.CreateEntities();
        NoiseSystem.Instance.Enabled = true;   

    }
    
    // Update is called once per frame
    void Update()
    {
        // Nothing to do here!    
    }
}

class NoiseSystem:SystemBase
{
    NoiseCube noiseCube;
    float delta = 0;

    private EntityManager entityManager;

    private EntityQuery noiseQuery;
    EntityArchetype archetype;
    NativeArray<Entity> entities;

    public static NoiseSystem Instance;

    RenderMesh cubeMesh;

    protected override void OnCreate()
    {
        Instance = this;
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager; 
        noiseCube = GameObject.FindObjectOfType<NoiseCube>();

        archetype = entityManager.CreateArchetype(
            typeof(Translation),
            typeof(Rotation),
            typeof(NonUniformScale),
            typeof(RenderBounds),
            typeof(LocalToWorld),
            typeof(NoiseCell)
        );
 
        double  start = Time.ElapsedTime;
                
        entities = new NativeArray<Entity>(noiseCube.size * noiseCube.size, Allocator.Persistent);

        cubeMesh = new RenderMesh
        {
            mesh = noiseCube.mesh,
            material = noiseCube.material
        };
        
    }

    protected override void OnDestroy()
    {
        entities.Dispose();
    }

    protected override void OnStartRunning()
    {
        Debug.Log("On start running");
        base.OnStartRunning();        
    }

    public void CreateEntities()
    {
        entityManager.CreateEntity(archetype, entities);
        noiseQuery = GetEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[] {
                    ComponentType.ReadOnly<NoiseCell>()
                }
        });

        entityManager.AddSharedComponentData(noiseQuery, cubeMesh);
    }

    protected override void OnStopRunning()
    {
        entityManager.DestroyEntity(noiseQuery);
    }

    protected override void OnUpdate()
    {        
        int size = noiseCube.size;
        int halfSize = size / 2;
        float noiseScale = noiseCube.noiseScale;
        float offset = 10000;
        float d = offset + this.delta;
        float s = noiseCube.scale;
        delta += Time.DeltaTime * noiseCube.speed;
        
        JobHandle handle = Entities.ForEach((int entityInQueryIndex, ref NoiseCell cell, ref Translation p, ref NonUniformScale scale) =>
        {
            int row = entityInQueryIndex / (size);
            int col = entityInQueryIndex - (row * size);
            float height = s + (s * Perlin.Noise((p.Value.x + d) * noiseScale, 0, (p.Value.z + d) * noiseScale));
            p.Value = new float3(row - halfSize, height / 2, col - halfSize);
            
            scale.Value = new float3(1, height, 1);
        })
        .ScheduleParallel(Dependency);
        Dependency = JobHandle.CombineDependencies(Dependency, handle);
    }
}
