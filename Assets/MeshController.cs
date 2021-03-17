using UnityEngine;
using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Burst;

// Adapted from https://catlikecoding.com/unity/tutorials/procedural-grid/

//[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshController : MonoBehaviour {

	public int size = 250;

	public Mesh mesh;
	public Material material;

	public NativeArray<Vector3> vertices;

	public float noiseScale = 0.0404f;
	public float yScale = 100;
	public float speed = 10;

	private void Awake () {
		vertices = new NativeArray<Vector3>((size + 1) * (size + 1), Allocator.Persistent);		
		Generate();		
		MeshDeformationSystem.Instance.CreateEntities();
		MeshDeformationSystem.Instance.Enabled = true;
	}

	void OnDestroy()
	{
		vertices.Dispose();
	}

	private void Generate () {
		mesh = new Mesh();
		//GetComponent<MeshFilter>().mesh = mesh; 
		mesh.name = "Procedural Grid";

		int halfXSize = size / 2;
		int halfYSize = size / 2;

		for (int i = 0, y = 0; y <= size; y++) {
			for (int x = 0; x <= size; x++, i++) {
				float h = Mathf.PerlinNoise(((x * 0.1f) + 10000), (y * 0.1f) + 10000) * 5;
				vertices[i] = transform.TransformPoint(new Vector3((x - halfXSize), 0, (y - halfYSize)));
			}
		}
		mesh.vertices = vertices.ToArray();
		//mesh.vertices = vertices;

		int[] triangles = new int[size * size * 6];
		for (int ti = 0, vi = 0, y = 0; y < size; y++, vi++) {
			for (int x = 0; x < size; x++, ti += 6, vi++) {
				triangles[ti] = vi;
				triangles[ti + 3] = triangles[ti + 2] = vi + 1;
				triangles[ti + 4] = triangles[ti + 1] = vi + size + 1;
				triangles[ti + 5] = vi + size + 2;
			}
		}
		mesh.triangles = triangles;
		mesh.RecalculateNormals();
	}

	private void OnDrawGizmos () {
		/*if (mesh != null)
		{
			Gizmos.DrawMesh(mesh);
		}
		*/		
	}
}

public struct MeshDeformation:IComponentData
{

}

public class MeshDeformationSystem : SystemBase
{
	MeshController meshController;
	public static MeshDeformationSystem Instance;

    protected override void OnCreate()
    {        
		Instance = this;
		Enabled = false;
    }

	public void CreateEntities()
	{
		meshController = GameObject.FindObjectOfType<MeshController>();

		RenderMesh rm = new RenderMesh()
		{
			mesh = meshController.mesh,
			material = meshController.material,
			castShadows = UnityEngine.Rendering.ShadowCastingMode.On
		};

		EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
		Entity meshEntity = entityManager.CreateEntity();

		entityManager.AddComponentData(meshEntity, new Translation(){Value = meshController.transform.position});
		entityManager.AddComponentData(meshEntity, new LocalToWorld());
		entityManager.AddComponentData(meshEntity, new RenderBounds());
		entityManager.AddComponentData(meshEntity, new MeshDeformation());		
		entityManager.AddSharedComponentData(meshEntity, rm);
	}

	float delta = 10000;

    protected override void OnUpdate()
    {
		delta += (Time.DeltaTime * meshController.speed);
        MeshJob meshJob = new MeshJob()
		{
			s = meshController.yScale,
			d = delta,
			noiseScale = meshController.noiseScale,
			size = meshController.size,
			vertices = meshController.vertices
		};

		var jh = meshJob.Schedule(meshController.vertices.Length, 64, Dependency);
		jh.Complete();
		meshController.mesh.vertices = meshController.vertices.ToArray();
		meshController.mesh.RecalculateNormals();
    }
}

[BurstCompile]
struct MeshJob : IJobParallelFor
{
	public NativeArray<Vector3> vertices;
	public float s;
	public float d;
	public float noiseScale;
	public int size;
    public void Execute(int index)
    {
		int row = index / (size);
		int col = index - (row * size);            
                
		Vector3 p = vertices[index];
		float noise = Perlin.Noise((p.x + d) * noiseScale, 0, (p.z + d) * noiseScale);		
		float height = (s * 0.2f) + (s * noise);
		p.y = height;
		vertices[index] = p;
    }
}