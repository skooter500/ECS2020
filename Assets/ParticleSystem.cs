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
    public float3 position;
}

public class ParticleSystem : SystemBase
{
    public Vector3 center;
    private RenderMesh particleMesh;
    public UnityEngine.Material material;

    EntityArchetype particleArchetype;
    EntityManager entityManager;

    int size = 20000;

    private NativeArray<Entity> entities;
    private NativeArray<float3> targetPositions;

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
                    typeof(NonUniformScale),
                    typeof(Particle)

        );

        Material material = Resources.Load<Material>("SpiralMaterial");
        GameObject c = Resources.Load<GameObject>("Cube 1");
        Mesh mesh = c.GetComponent<MeshFilter>().sharedMesh;
        particleMesh = new RenderMesh
        {
            mesh = mesh,
            material = material
        };
    }

    protected override void OnDestroy()
    {
        entities.Dispose();
        targetPositions.Dispose();
    }

    protected override void OnCreate()
    {
        Instance = this;

        entities = new NativeArray<Entity>(size, Allocator.Persistent);
        targetPositions = new NativeArray<float3>(size, Allocator.Persistent);
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
        float points = controller.points;
        float radius = controller.radius;
        float inc = (math.PI * 2.0f) / points;
        float timeDelta = Time.DeltaTime;
        float speed = controller.speed;
        float spacer = controller.spacer == 0 ? 1 : controller.spacer;
        int direction = controller.direction;
        NativeArray<float3> positions = this.targetPositions;
        float thickness = controller.thickness;
        bool open = controller.open;
        int size = this.size;
        var jobHandle = Entities
            .WithNativeDisableParallelForRestriction(positions)
            .ForEach((int entityInQueryIndex, ref Particle p, ref Translation t, ref NonUniformScale s, ref Rotation r) =>
        {
            float angle = entityInQueryIndex * inc;
            
            float cycles;
            if (open)
            {
                cycles = 1 + (entityInQueryIndex / spacer);                
            }
            else
            {
                if (entityInQueryIndex < size / 2)
                {
                    cycles = 1 + (entityInQueryIndex / spacer);
                }
                else
                {
                    int i = (size -1) - entityInQueryIndex;
                    cycles = 1 + (i / spacer);
                }
            }

            float3 target = new float3();
            switch (direction)
            {
                case 0:
                    target = new float3(
                        math.cos(angle) * radius * cycles, 
                        math.sin(angle) * radius * cycles,
                        entityInQueryIndex);
                    break;
                case 1:
                    target = new float3(
                        - math.cos(angle) * radius * cycles, 
                        math.sin(angle) * radius * cycles,
                        entityInQueryIndex);
                    break;
                case 2:
                    target = new float3(
                        math.cos(angle) * radius * cycles, 
                        - math.sin(angle) * radius * cycles,
                        entityInQueryIndex);
                    break;
                case 3:
                    target = new float3(
                        - math.cos(angle) * radius * cycles, 
                        - math.sin(angle) * radius * cycles,
                        entityInQueryIndex);
                    break;

            } 
            p.position = math.lerp(p.position, target, timeDelta * speed);
            positions[entityInQueryIndex] = p.position;
            
            float3 previous;
            if (entityInQueryIndex > points)
            {
                previous = positions[entityInQueryIndex - (int) points - 1];
            }
            else
            {
                previous = Vector3.zero;
            }

            float3 toTarget1 = p.position - previous;
            float3 cent = previous + ((toTarget1) / 2.0f);

            s.Value = new float3(thickness, thickness, math.length(toTarget1));
            
            Quaternion q = Quaternion.LookRotation(toTarget1);
            r.Value = q;
            t.Value = cent;
        })
        .ScheduleParallel(Dependency);
        Dependency = JobHandle.CombineDependencies(Dependency, jobHandle);
    }
}