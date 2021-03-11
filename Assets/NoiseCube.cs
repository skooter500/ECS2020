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
        noiseCube = GameObject.FindObjectOfType<NoiseCube>();
        Enabled = false;
    }

    protected override void OnDestroy()
    {
    }

    protected override void OnStartRunning()
    {
    }

    public void CreateEntities()
    {
    }

    protected override void OnStopRunning()
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
    
    }
}
