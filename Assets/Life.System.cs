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

public class LifeSystem : SystemBase
{
    public static string[] rules;
    public string rule;
    public int size = 50;

    private NativeArray<int> board;
    private NativeArray<int> next;
    private NativeHashMap<int, Entity> cells;

    private RenderMesh cubeMesh;
    public Material material;

    EndSimulationEntityCommandBufferSystem ecb;
    EntityArchetype cubeArchetype;

    EntityManager entityManager;

    private void Set(int slice, int row, int col, int val)
    {
        int cell = LifeJob.ToCell(size, slice, row, col);
        board[cell] = val;
        
        // Do we need an entity created or destroyed
        if (val == 0)
        {
            Entity item;                
            if (cells.TryGetValue(cell, out item))
            {
                entityManager.DestroyEntity(item);
            }                
        }
        else
        {
            Entity item;
            if (!cells.TryGetValue(cell, out item))
            {
                Entity e = entityManager.CreateEntity(cubeArchetype);
                Translation p = new Translation();
                p.Value = new float3(slice, row, col);
                entityManager.SetComponentData<Translation>(e, p);
                entityManager.AddSharedComponentData(e, cubeMesh);
                cells.TryAdd(cell, e);
            }
        }
    }

    protected override void OnCreate()
    {
        board = new NativeArray<int>((int)Mathf.Pow(size, 3), Allocator.Persistent);
        next = new NativeArray<int>((int)Mathf.Pow(size, 3), Allocator.Persistent);
        cells = new NativeHashMap<int, Entity>((int)Mathf.Pow(size, 3), Allocator.Persistent);
        Enabled = false;

        ecb = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;


        CreateArchetype();

        InitialState();
    }

    private void CreateArchetype()
    {
        cubeArchetype = entityManager.CreateArchetype(
                    typeof(Translation),
                    typeof(LocalToWorld),
                    typeof(RenderBounds)
        );

        Material material = (Material)Resources.Load("Cube", typeof(Material));
        GameObject c = Resources.Load<GameObject>("Cube 1"); 
        Mesh mesh = c.GetComponent<MeshFilter>().sharedMesh;
        cubeMesh = new RenderMesh
        {
            mesh = mesh,
            material = material
        }; 
    }

    private void InitialState()
    {
        for(int col = 0 ; col < size ; col ++)
        {
            Set(0, size / 2, col, 255);
        }
    }

    protected override void OnDestroy()
    {
        board.Dispose();
        next.Dispose();
        cells.Dispose();
    }

    protected override void OnUpdate()
    {
        /*
        var ecbpw = ecb.CreateCommandBuffer().AsParallelWriter();
        
        var lifeJob = new LifeJob()
        {
            archetype = this.cubeArchetype,
            board = this.board,
            next = this.next,
            cells = this.cells,
            size = this.size,
            ecb = ecbpw
        };

        var jobHandle = lifeJob.Schedule(size * size * size, size, Dependency);

        JobHandle.CombineDependencies(Dependency, jobHandle);
        */
    }

    [BurstCompile]
    struct LifeJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<int> board;

        [NativeDisableParallelForRestriction]
        public NativeArray<int> next;

        [NativeDisableParallelForRestriction]
        public NativeHashMap<int, Entity> cells;

        public EntityCommandBuffer.ParallelWriter ecb;

        public EntityArchetype archetype;

        public int size;

        public RenderMesh cubeMesh;

        public static int ToCell(int size, int slice, int row, int col)
        {
            return (slice * size * size) + (row * size) + col;
        }

        public int Get(int slice, int row, int col)
        {
            if (row < 0 || row >= size || col < 0 || col >= size || slice < 0 || slice >= size)
            {
                return 0;
            }
            return (board[ToCell(size, slice, row, col)]);
        }

        public void Set(int slice, int row, int col, int val, int i)
        {
            int cell = ToCell(size, slice, row, col);
            board[cell] = val;

            int s = (cell / (size * size));
            int r = (cell - (slice * size * size)) / (size);
            int c = (cell - (row * size)) - (slice * size * size);
            
            // Do we need an entity created or destroyed
            if (val == 0)
            {
                Entity item;                
                if (cells.TryGetValue(cell, out item))
                {
                    ecb.DestroyEntity(i, item);
                }                
            }
            else
            {
                Entity item;
                if (!cells.TryGetValue(cell, out item))
                {
                    Entity e = ecb.CreateEntity(i, archetype);
                    Translation p = new Translation();
                    p.Value = new float3(s, row, col);
                    ecb.SetComponent<Translation>(i, e, p);
                    ecb.SetSharedComponent<RenderMesh>(i, e, cubeMesh);
                    cells.TryAdd(cell, e);
                }
            }
        }

        private int CountNeighbours(int row, int col, int slice)
        {
            int count = 0;
            for (int s = slice - 1; s <= slice + 1; s++)
            {
                for (int r = row - 1; r <= row + 1; r++)
                {
                    for (int c = col - 1; c <= col + 1; c++)
                    {
                        if (r != row && c != col && s != slice)
                        {
                            if (Get(s, r, c) > 0)
                            {
                                count++;
                            }
                        }
                    }
                }                
            }
            return count;
        }

        public void Execute(int i)
        {
            // Classic Conways
            int slice = (i / (size * size));
            int row = (i - (slice * size * size)) / (size);
            int col = (i - (row * size)) - (slice * size * size);
            
            int count = CountNeighbours(slice, row, col);
            if (Get(slice, row, col) > 0)
            {
                if (count == 2 || count == 3)
                {
                    Set(slice, row, col, 255, i);
                }
                else
                {
                    Set(slice, row, col, 0, i);
                }
            }
            else if (count == 3)
            {
                Set(slice, row, col, 255, i);
            }
            else
            {
                Set(slice, row, col, 0, i);
            }
        }
    }
}