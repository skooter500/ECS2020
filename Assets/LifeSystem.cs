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
    public int size = 20;

    private NativeArray<int> board;
    private NativeArray<int> next;
    private NativeHashMap<int, Entity> cells;

    private RenderMesh cubeMesh;
    public Material material;

    EndSimulationEntityCommandBufferSystem ecb;
    EntityArchetype cubeArchetype;

    Entity cubePrefab;

    EntityManager entityManager;

    private void Randomize()
    {
        for(int slice = 0 ; slice < size ; slice ++)
        {
            for(int row = 0 ; row < size ; row ++)
            {
                for(int col = 0 ; col < size ; col ++)
                {
                    float dice = UnityEngine.Random.Range(0.0f, 1.0f);
                    if (dice > 0.5f)
                    {
                        Set(slice, row, col, 255);
                    }
                    else
                    {
                        Set(slice, row, col, 0);
                    }
                }
            }
            
        }
    }

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
        //Enabled = false;

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
                    typeof(RenderBounds),
                    typeof(RenderMesh)
                    
        );

        Material material = Resources.Load<Material>("LifeMaterial");
        GameObject c = Resources.Load<GameObject>("Cube 1"); 
        Mesh mesh = c.GetComponent<MeshFilter>().sharedMesh;
        cubeMesh = new RenderMesh
        {
            mesh = mesh,
            material = material
        };

        cubePrefab = entityManager.CreateEntity(cubeArchetype);
        entityManager.SetSharedComponentData(cubePrefab, cubeMesh);
    }

    private void InitialState()
    {
        //Randomize();
        
        for(int col = 0 ; col < size ; col ++)
        {
            Set(0, size / 2, col, 255);
            Set(0, (size / 2) + 1, col, 255);
        }
        
    }

    protected override void OnDestroy()
    {
        board.Dispose();
        next.Dispose();
        cells.Dispose();
    }

    float timePassed = 0;
    int generation = 0;
    protected override void OnUpdate()
    {
        timePassed += Time.DeltaTime;
        if (timePassed > 2.0f)
        {
            var ecbpw = ecb.CreateCommandBuffer().AsParallelWriter();               
            Debug.Log(generation);
            generation ++;
            timePassed = 0;
            var lifeJob = new LifeJob()
            {
                cubePrefab = this.cubePrefab,
                board = this.board,
                next = this.next,
                cells = this.cells,
                size = this.size,
                ecb = ecbpw
            };

            var jobHandle = lifeJob.Schedule(size * size * size, 1, Dependency);
            Dependency = JobHandle.CombineDependencies(Dependency, jobHandle);        
        
            var cnJob = new CopyNextToBoard()
            {
                next = this.next,
                board = this.board
            };

            var cnHandle = cnJob.Schedule(size * size * size, 1, Dependency);
            Dependency = JobHandle.CombineDependencies(Dependency, cnHandle);        
        
            ecb.AddJobHandleForProducer(Dependency);
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

        public Entity cubePrefab; 

        //public EntityArchetype cubeArchetype;
        //public RenderMesh cubeMesh;

        public int size;

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
            next[cell] = val;

            int s = (cell / (size * size));
            int r = (cell - (slice * size * size)) / (size);
            int c = (cell - (row * size)) - (slice * size * size);
            
            // Do we need an entity created or destroyed
            if (val == 0)
            {
                Entity item;                
                if (cells.TryGetValue(cell, out item))
                {   
                    Debug.Log("Deleting an entity");                                     
                    ecb.DestroyEntity(i, item);
                    cells.Remove(cell);
                }                
            }
            else
            {
                Entity item;
                if (!cells.TryGetValue(cell, out item))
                {
                    Debug.Log("Creating an entity");
                    Entity e = ecb.Instantiate(i, cubePrefab);
                    //Entity e = ecb.CreateEntity(i, cubeArchetype);
                    Translation p = new Translation();
                    p.Value = new float3(s, row, col);
                    ecb.SetComponent<Translation>(i, e, p);
                    //ecb.AddSharedComponent(i, e, cubeMesh);
                    cells.TryAdd(cell, e);
                }
            }
        }

        private int CountNeighbours(int slice, int row, int col)
        {
            int count = 0;
            
            for (int s = slice - 1; s <= slice + 1; s++)
            {
                for (int r = row - 1; r <= row + 1; r++)
                {
                    for (int c = col - 1; c <= col + 1; c++)
                    {                        
                        if (! (r == row && c == col && s == slice))
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
            //Debug.LogFormat("i: {0} slice {1} row {2} col {3}", i, slice, row, col);
            int count = CountNeighbours(slice, row, col);    

            if (Get(slice, row, col) > 0)
            {
                if (count == 4 || count == 5)
                {
                    
                    Set(slice, row, col, 255, i);
                }
                else
                {
                    Set(slice, row, col, 0, i);
                }
            }
            else if (count == 5)
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