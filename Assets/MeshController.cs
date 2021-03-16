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

	public int xSize, ySize;

	public Mesh mesh;
	public Material material;

	public NativeArray<Vector3> vertices;

	private void Awake () {
		vertices = new NativeArray<Vector3>((xSize + 1) * (ySize + 1), Allocator.Persistent);		
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

		int halfXSize = xSize / 2;
		int halfYSize = xSize / 2;

		for (int i = 0, y = 0; y <= ySize; y++) {
			for (int x = 0; x <= xSize; x++, i++) {
				float h = Mathf.PerlinNoise(((x * 0.1f) + 10000), (y * 0.1f) + 10000) * 5;
				vertices[i] = transform.TransformPoint(new Vector3(x - halfXSize, 0, y - halfYSize));
			}
		}
		mesh.vertices = vertices.ToArray();
		//mesh.vertices = vertices;

		int[] triangles = new int[xSize * ySize * 6];
		for (int ti = 0, vi = 0, y = 0; y < ySize; y++, vi++) {
			for (int x = 0; x < xSize; x++, ti += 6, vi++) {
				triangles[ti] = vi;
				triangles[ti + 3] = triangles[ti + 2] = vi + 1;
				triangles[ti + 4] = triangles[ti + 1] = vi + xSize + 1;
				triangles[ti + 5] = vi + xSize + 2;
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
		delta += (Time.DeltaTime * 10);
        MeshJob meshJob = new MeshJob()
		{
			s = 30,
			d = delta,
			noiseScale = 0.0404f,
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
    public void Execute(int index)
    {
		Vector3 p = vertices[index];
		float noise = Perlin.Noise((p.x + d) * noiseScale, 0, (p.z + d) * noiseScale);		
		float height = (s * 0.2f) + (s * noise);
		p.y = height;
		vertices[index] = p;
    }
}