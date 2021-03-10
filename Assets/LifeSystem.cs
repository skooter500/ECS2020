using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.SceneManagement;
using System;

struct Cell : IComponentData
{
    public int cellId;
}

public class TwoDLifeSystem : SystemBase
{
    public Vector3 center;
    public static string[] rules;
    public string rule;
    public int size = 400;

    private NativeArray<int> board;
    private NativeArray<int> next;
    private NativeArray<Entity> entities;

    private RenderMesh cubeMesh;
    public Material material;

    EntityArchetype cubeArchetype;

    EntityManager entityManager;

    EntityQuery cellQuery;

    public float delay = 0.0f;

    internal static readonly AABB OutOfBounds = new AABB
    {
        Center = new float3(-1000000, -1000000, -1000000),
        Extents = new float3(0, 0, 0),
    };

    internal static readonly float3 PositionOutOfBounds = new float3(-1000000, -1000000, -1000000);
    public static TwoDLifeSystem Instance;

    public void Randomize()
    {
        int halfSize = size / 2;
        {
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col <size; col++)
                {
                    float dice = UnityEngine.Random.Range(0.0f, 1.0f);
                    if (dice > 0.5f)
                    {
                        Set(ref board, size, row, col, 4);
                    }
                    
                    else
                    {
                        Set(ref board, size, row, col, 0);
                    }
                }
            }
        }
    }

    protected override void OnCreate()
    {
        Debug.Log("On create");
        Instance = this;
        board = new NativeArray<int>((int)Mathf.Pow(size, 2), Allocator.Persistent);
        next = new NativeArray<int>((int)Mathf.Pow(size, 2), Allocator.Persistent);
        entities = new NativeArray<Entity>((int)Mathf.Pow(size, 2), Allocator.Persistent);

        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        CreateArchetype();

        Enabled = false;
    }

    protected override void OnStartRunning()
    {
        
    }

    public void DestroyEntities()
    {
        entityManager.DestroyEntity(cellQuery);
    }

    protected override void OnStopRunning()
    {
        Debug.Log("On stop running");
        DestroyEntities();
    }

    public void CreateEntities()
    {
        entityManager.CreateEntity(cubeArchetype, entities);

        cellQuery = GetEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[] {
                    ComponentType.ReadOnly<Cell>()
                }
        });

        entityManager.AddSharedComponentData(cellQuery, cubeMesh);
    }

    private void CreateArchetype()
    {
        cubeArchetype = entityManager.CreateArchetype(
                    typeof(Translation),
                    typeof(Rotation),
                    typeof(NonUniformScale),
                    typeof(LocalToWorld),
                    typeof(RenderBounds),
                    typeof(Cell)

        );

        Material material = Resources.Load<Material>("LifeMaterial");
        GameObject c = Resources.Load<GameObject>("Cube 1");
        Mesh mesh = c.GetComponent<MeshFilter>().sharedMesh;
        cubeMesh = new RenderMesh
        {
            mesh = mesh,
            material = material
        };
    }

    public void Clear()
    {
        for (int i = 0; i < size * size; i++)
        {
            board[i] = 0;
        }        
    }

    public void Cross()
    {
        //Randomize();
        for (int i = 0; i < size; i++)
        {
            Set(ref board, size, size / 2, i, 4);
            Set(ref board, size, i, size / 2, 4);
            
            Set(ref board, size, size / 2 + 1, i, 4);
            Set(ref board, size, i, size / 2 + 1, 4);            
        }

    }

    protected override void OnDestroy()
    {
        board.Dispose();
        next.Dispose();
        entities.Dispose();
    }

    float timePassed = 0;
    int generation = 0;
    public bool populated = false;

    public static int ToCell(int size, int row, int col)
    {
        return (row * size) + col;
    }

    public static int Get(ref NativeArray<int> board, int size, int row, int col)
    {
        if (row < 0 || row >= size || col < 0 || col >= size)
        {
            return 0;
        }
        return (board[ToCell(size, row, col)]);
    }

    public static void Set(ref NativeArray<int> board, int size, int row, int col, int val)
    {
        int cell = ToCell(size, row, col);
        board[cell] = val;
    }

    private static int CountNeighbours(ref NativeArray<int> board, int size, int row, int col)
    {
        int count = 0;

        for (int r = row - 1; r <= row + 1; r++)
        {
            for (int c = col - 1; c <= col + 1; c++)
            {
                if (!(r == row && c == col))
                {
                    if (Get(ref board, size, r, c) == 4)
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }

    public static void SetPosition(int size, int row, int col, Vector3 center, ref Translation p)
    {
        int halfSize = size / 2;
        p.Value.z = center.x - (size / 2);
        p.Value.y = center.y + row - (size / 2);
        p.Value.x = center.z + col - (size / 2);
    }

    protected override void OnUpdate()
    {
        timePassed += Time.DeltaTime;

        NativeArray<int> board = this.board;
        NativeArray<int> next = this.next;
        int size = this.size;
        Vector3 center = this.center;
        if (!populated)
        {
            Debug.Log("populating!");
            populated = true;
            JobHandle popHandle = Entities
                .WithBurst()
                .ForEach((int entityInQueryIndex, ref Cell c, ref Translation p, ref NonUniformScale s) =>
            {
                int row = entityInQueryIndex / (size);
                int col = entityInQueryIndex - (row * size);
                c.cellId = entityInQueryIndex;
                s.Value = new float3(1,1, 3);
                if (board[entityInQueryIndex] > 0)
                {
                    SetPosition(size, row, col, center, ref p);
                }
                else
                {
                    p.Value = PositionOutOfBounds;
                }
            })
            .ScheduleParallel(Dependency);
            Dependency = JobHandle.CombineDependencies(Dependency, popHandle);
        }

        if (timePassed > delay)
        {
            //Debug.Log("Generation: " + generation);
            generation++;
            timePassed = 0;

            var lifeHandle = Entities
                .WithNativeDisableParallelForRestriction(board)
                .WithBurst()
                .ForEach((int entityInQueryIndex, ref Cell c, ref Translation p) =>
                {
                    int row = entityInQueryIndex / (size);
                    int col = entityInQueryIndex - (row * size);
                    int count = CountNeighbours(ref board, size, row, col);
                    int n = Get(ref board, size, row, col);
                    if (n > 0)
                    {
                        if (count == 2 || count == 3)
                        {

                            Set(ref next, size, row, col, n);
                            SetPosition(size, row, col, center, ref p);
                        }
                        else
                        {
                            Set(ref next, size, row, col, 0);
                            p.Value = PositionOutOfBounds;
                        }
                    }
                    else
                    {
                        if (count == 3)
                        {
                            Set(ref next, size, row, col, 4);
                            SetPosition(size, row, col, center, ref p);
                        }
                        else
                        {
                            Set(ref next, size, row, col, 0);
                            p.Value = PositionOutOfBounds;
                        }
                    }
                })
                .ScheduleParallel(Dependency);
            Dependency = JobHandle.CombineDependencies(Dependency, lifeHandle);

            var cnJob = new CopyNextToBoard()
            {
                next = this.next,
                board = this.board
            };

            var cnHandle = cnJob.Schedule(size * size, 1, Dependency);
            Dependency = JobHandle.CombineDependencies(Dependency, cnHandle);
        }
    }

    [BurstCompile]
    struct CopyNextToBoard : IJobParallelFor
    {
        public NativeArray<int> board;

        public NativeArray<int> next;

        public void Execute(int i)
        {
            board[i] = next[i];
        }
    }
}
