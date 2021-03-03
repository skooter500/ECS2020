using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;


public struct Particle:IComponentData
{
    public float3 targetPos;
}

public class ParticleSystem : SystemBase
{
    public Vector3 center;
    private RenderMesh particleMesh;
    public UnityEngine.Material material;

    EntityArchetype particleArchetype;
    EntityManager entityManager;

    int size = 10000;

    private NativeArray<Entity> entities;

    EntityQuery particleQuery;

    public static ParticleSystem Instance;

    public ParticleController controller;
    
    public void CreateEntities()
    {
        entityManager.CreateEntity(particleArchetype, entities);

        particleQuery = GetEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[] {
                    ComponentType.ReadOnly<Particle>()
                }
        });

        entityManager.AddSharedComponentData(particleQuery, particleMesh);

        controller = GameObject.FindObjectOfType<ParticleController>();
    }

    private void CreateArchetype()
    {
        particleArchetype = entityManager.CreateArchetype(
                    typeof(Translation),
                    typeof(Rotation),
                    typeof(LocalToWorld),
                    typeof(RenderBounds),
                    typeof(Particle)

        );

        Material material = Resources.Load<Material>("SpiralMaterial");
        GameObject c = Resources.Load<GameObject>("Sphere");
        Mesh mesh = c.GetComponent<MeshFilter>().sharedMesh;
        particleMesh = new RenderMesh
        {
            mesh = mesh,
            material = material
        };
    }

    protected override void OnCreate()
    {
        Instance = this;

        entities = new NativeArray<Entity>(size, Allocator.Persistent);
        Enabled = false;
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        CreateArchetype();
    }

    public void DestroyEntities()
    {
        entityManager.DestroyEntity(particleQuery);
    }

    protected override void OnStopRunning()
    {
        Debug.Log("On stop running");
        DestroyEntities();
    }

    protected override void OnUpdate()
    {
        float turnFraction = controller.turnFraction;
        float radius = controller.radius;
        float inc = (math.PI * 2.0f) * turnFraction;
        float timeDelta = Time.DeltaTime;
        float speed = controller.speed;
        float spacer = controller.spacer == 0 ? 1 : controller.spacer;
        var jobHandle = Entities.ForEach((int entityInQueryIndex, ref Particle p, ref Translation t) =>
        {
            float angle = entityInQueryIndex * inc;
            
            //int cycles = 1 + (int)(angle / (math.PI * 2.0f));
            float cycles = 1 + (entityInQueryIndex / spacer);

            p.targetPos = new float3(math.cos(angle) * radius * cycles, math.sin(angle) * radius * cycles, 0);
            t.Value = math.lerp(t.Value, p.targetPos, timeDelta * speed);
        })
        .ScheduleParallel(Dependency);
        Dependency = JobHandle.CombineDependencies(Dependency, jobHandle);
    }
}