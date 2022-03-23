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
     
    }
    

    // Start is called before the first frame update
    void Start()
    {
        
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
        Enabled = false;

        
    }

    protected override void OnDestroy()
    {
    }

    public void DestroyEntities()
    {
        
    }

    protected override void OnStopRunning()
    {
        
    }

    protected override void OnStartRunning()
    {
        noiseCube = GameObject.FindObjectOfType<NoiseCube>();        
        
    }

    public void CreateEntities()
    {
        
        
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
