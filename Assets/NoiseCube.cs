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
}

public class NoiseCube : MonoBehaviour
{
    private EntityManager entityManager;

    public Mesh mesh;
    public Material material;

    public int size = 500;

    [Range(0.02f, 0.07f)]
    public float noiseScale = 0.01f;
    public float scale = 10;

    public float speed = 1.0f;

    public EntityArchetype archetype;

    NoiseSystem noiseSystem;



    public void OnDestroy()
    {
        Debug.Log("OnDestroy LifeEnabler");
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            NoiseSystem.Instance.Enabled = false;
        }
    }
    

    // Start is called before the first frame update
    void Start()
    {
        
        /*
        int halfSize = size / 2;

        float start = Time.realtimeSinceStartup;

        for(int row = 0 ; row < size ; row ++)
        {
            for(int col = 0 ; col < size ; col ++)
            {
                Entity e = entityManager.CreateEntity(archetype);
                entityManager.AddComponentData(e, new Translation{Value = new float3(row - halfSize, 0, col - halfSize)});
                entityManager.AddComponentData(e, new Rotation{Value = Quaternion.identity});
                entityManager.AddComponentData(e, new NonUniformScale{Value = new float3(1,1,1)});
                entityManager.AddSharedComponentData(e, r);
            }
        }

        float ellapsed = Time.realtimeSinceStartup - start;
        Debug.Log("Creating " + (size * size) + " entities took " + ellapsed + " seconds");
        */
        NoiseSystem.Instance.Enabled = true;   
        NoiseSystem.Instance.CreateEntities();
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
        noiseCube = GameObject.FindObjectOfType<NoiseCube>();
        Enabled = false;

        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        archetype = entityManager.CreateArchetype(
            typeof(Translation),
            typeof(Rotation),
            typeof(NonUniformScale),
            typeof(LocalToWorld),
            typeof(RenderBounds),
            typeof(NoiseCell)
        );

        cubeMesh = new RenderMesh 
        {
            mesh = noiseCube.mesh,
            material = noiseCube.material
        };

        noiseQuery = GetEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[] {
                    ComponentType.ReadOnly<NoiseCell>()
                }
        });

        entities = new NativeArray<Entity>((int)Mathf.Pow(noiseCube.size, 2), Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        entities.Dispose();
    }

    public void DestroyEntities()
    {
        entityManager.DestroyEntity(noiseQuery);
    }

    protected override void OnStopRunning()
    {
        Debug.Log("On stop running");
        DestroyEntities();
    }

    protected override void OnStartRunning()
    {
    }

    public void CreateEntities()
    {
        entityManager.CreateEntity(archetype, entities);        

        entityManager.AddSharedComponentData(noiseQuery, cubeMesh);
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

        JobHandle handle = Entities
            .WithBurst()
            .ForEach((int entityInQueryIndex, ref NoiseCell cell, ref Translation p, ref NonUniformScale scale) =>
            {
                int row = entityInQueryIndex / (size);
                int col = entityInQueryIndex - (row * size);            
                float height = (s * 0.2f) + (s * Perlin.Noise((p.Value.x + d) * noiseScale, 0, (p.Value.z + d) * noiseScale));

                // Should use the new noise functions
                //float2 noisePoint = new float2((p.Value.x + d) * noiseScale, (p.Value.z + d) * noiseScale);
                //float height = (s * 0.2f) + (s * noise.snoise(noisePoint));
                

                p.Value = new float3(row - halfSize, height / 2, col - halfSize);
                scale.Value = new float3(1, height, 1);
            })
        .ScheduleParallel(Dependency);
        Dependency = JobHandle.CombineDependencies(Dependency, handle);
    }
}
