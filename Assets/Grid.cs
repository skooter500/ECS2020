using UnityEngine;
using System.Collections;

// Adapted from https://catlikecoding.com/unity/tutorials/procedural-grid/

//[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Grid : MonoBehaviour {

	public int xSize, ySize;

	Mesh mesh;

	private Vector3[] vertices;

	private void Awake () {
		Generate();
	}

	private void Generate () {
		mesh = new Mesh();
		//GetComponent<MeshFilter>().mesh = mesh; 
		mesh.name = "Procedural Grid";

		int halfXSize = xSize / 2;
		int halfYSize = xSize / 2;

		vertices = new Vector3[(xSize + 1) * (ySize + 1)];
		for (int i = 0, y = 0; y <= ySize; y++) {
			for (int x = 0; x <= xSize; x++, i++) {
				float h = Mathf.PerlinNoise(((x * 0.1f) + 10000), (y * 0.1f) + 10000) * 5;
				vertices[i] = transform.TransformPoint(new Vector3(x - halfXSize, h, y - halfYSize));
			}
		}
		mesh.vertices = vertices;

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
		if (mesh != null)
		{
			Gizmos.DrawMesh(mesh);
		}		
	}
}

