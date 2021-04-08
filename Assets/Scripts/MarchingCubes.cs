using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class MarchingCubes : MonoBehaviour {

    // How did this not exist until 2021 alpha?? https://twitter.com/six_ways/status/1316419918536093698
    private static readonly Vector3Int Vector3Int_forward = new Vector3Int(0, 0, 1);

    [Range(1, 10)]
    public int voxelSize = 1; // 1 meter.
    [Range(1,40)]
    public uint chunkDimension = 10; // 10x10x10
    [Range(1, 10)]
    public float sphereRadius = 5f;

    public Vector3[] spheres = new Vector3[0];
    public Vector3[] velocities = new Vector3[0];
    public GameObject debugVertex;

    private Vector3[] debugVerts;
    private TriangleTable.Gridcell[,,] cells;

    private Mesh mesh;
    private SimplexNoiseGenerator simplexNoise = new SimplexNoiseGenerator();

    private void Start() {
        ConstructChunk();
    }

    private void ConstructChunk() {
        cells = new TriangleTable.Gridcell[chunkDimension, chunkDimension, chunkDimension];
        int vertCount = (int)chunkDimension + 1;
        debugVerts = new Vector3[vertCount * vertCount * vertCount];

        for (int x = 0; x < vertCount; x++) {
            for (int y = 0; y < vertCount; y++) {
                for (int z = 0; z < vertCount; z++) {
                    int vertIndex = x * vertCount * vertCount + y * vertCount + z;
                    Vector3Int vertPosition = new Vector3Int(x * voxelSize, y * voxelSize, z * voxelSize); ;
                    // GameObject vertex = Instantiate(debugVertex, this.transform);
                    // vertex.transform.position = vertPosition;
                    // debugVerts[vertIndex] = vertPosition;


                    // Create cells all the way up to the boundary edge case of the chunk.
                    if (x != chunkDimension && y != chunkDimension && z != chunkDimension) {
                        TriangleTable.Gridcell cell;
                        cell.position = new Vector3Int[8];
                        cell.strength = new float[8];
                        Vector3Int pos0 = vertPosition;
                        Vector3Int pos1 = vertPosition + Vector3Int.up;
                        Vector3Int pos2 = vertPosition + Vector3Int.up + Vector3Int.right;
                        Vector3Int pos3 = vertPosition + Vector3Int.right;
                        Vector3Int pos4 = vertPosition + Vector3Int_forward;
                        Vector3Int pos5 = vertPosition + Vector3Int_forward + Vector3Int.up;
                        Vector3Int pos6 = vertPosition + Vector3Int_forward + Vector3Int.up + Vector3Int.right;
                        Vector3Int pos7 = vertPosition + Vector3Int_forward + Vector3Int.right;

                        cell.position[0] = pos0;
                        cell.position[1] = pos1;
                        cell.position[2] = pos2;
                        cell.position[3] = pos3;
                        cell.position[4] = pos4;
                        cell.position[5] = pos5;
                        cell.position[6] = pos6;
                        cell.position[7] = pos7;

                        cell.strength[0] = CalculateStrength(pos0);
                        cell.strength[1] = CalculateStrength(pos1);
                        cell.strength[2] = CalculateStrength(pos2);
                        cell.strength[3] = CalculateStrength(pos3);
                        cell.strength[4] = CalculateStrength(pos4);
                        cell.strength[5] = CalculateStrength(pos5);
                        cell.strength[6] = CalculateStrength(pos6);
                        cell.strength[7] = CalculateStrength(pos7);

                        cells[x, y, z] = cell;
                    }
                }
            }
        }

        List<TriangleTable.Triangle> triangles = new List<TriangleTable.Triangle>();
        for (int x = 0; x < chunkDimension; x++) {
            for (int y = 0; y < chunkDimension; y++) {
                for (int z = 0; z < chunkDimension; z++) {
                    triangles.AddRange(TriangleTable.Polygonise(cells[x, y, z], 0.5f));
                }
            }
        }

        int[] triangleIndices = new int[triangles.Count * 3];
        Vector3[] vertices = new Vector3[triangles.Count * 3];
        for (int i = 0; i < triangles.Count; i++) {
            for (int v = 0; v < 3; v++) {
                vertices[i * 3 + v] = triangles[i].verts[v];
                triangleIndices[i * 3 + v] = i * 3 + v;
            }
        }

        mesh = new Mesh();
        mesh.name = "MarchingCubesMesh";
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        //triangles = new int[] {
        //    0, 10, 1
        //};

        meshFilter.mesh = mesh;

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangleIndices;
        mesh.RecalculateNormals();
        // mesh.vertices = new Vector3[] { new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1) };
        // mesh.uv = new Vector2[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1) };
        // mesh.triangles = new int[] { 0, 10, 1 };
    }

    private float CalculateStrength(Vector3 position) {
        // Sin wave balls
        // return Mathf.Sin(position.x)/3f + Mathf.Sin(position.y) / 3f + Mathf.Sin(position.z) / 3f;
        // Single ocative Perlin noise.
        // return Perlin.Noise(position);
        // Simplex Noise
        // position /= 20f;
        // return simplexNoise.noise(position.x, position.y, position.z) + 0.5f;
        // return simplexNoise.coherentNoise(position.x, position.y, position.z);

        // Implicit Sphere
        // Vector3 boundsCenter = transform.position + Vector3.one * chunkDimension / 2f * voxelSize;
        // return 1f - Vector3.Distance(position, boundsCenter) / 10f * voxelSize;
        float strength = 0f;
        foreach (Vector3 spherePos in spheres) {
            strength = Mathf.Max(strength, SpherePointWeight(spherePos, position));
        }
        return strength;
    }

    private float SpherePointWeight(Vector3 spherePos, Vector3 point) {
        return 1f - voxelSize * Vector3.Distance(spherePos, point) / (sphereRadius*2);
    }

    private static T[] Flatten<T>(T[,,] array) {
        T[] result = new T[array.Length];
        int index = 0;
        for (int x = 0; x < array.GetUpperBound(0); x++) {
            for (int y = 0; y < array.GetUpperBound(1); y++) {
                for (int z = 0; z < array.GetUpperBound(2); z++) {
                    result[index] = array[x, y, z];
                }
            }
        }
        return result;
    }

    // Update is called once per frame
    void Update() {
        float maxBound = chunkDimension - sphereRadius;
        float minBound = sphereRadius;
        for (int i = 0; i < spheres.Length; i++) {
            Vector3 velocity = velocities[i];
            Vector3 newPos = spheres[i] += velocity * Time.deltaTime;
            if (newPos.x < minBound || newPos.x > maxBound) {
                newPos.x = (newPos.x < minBound) ? minBound : maxBound;
                velocity.x = -velocity.x;
                velocities[i] = velocity;
            }
            if (newPos.y < minBound || newPos.y > maxBound) {
                newPos.y = (newPos.y < minBound) ? minBound : maxBound;
                velocity.y = -velocity.y;
                velocities[i] = velocity;
            }
            if (newPos.z < minBound || newPos.z > maxBound) {
                newPos.z = (newPos.z < minBound) ? minBound : maxBound;
                velocity.z = -velocity.z;
                velocities[i] = velocity;
            }
            spheres[i] = newPos;
        }
        ConstructChunk();
    }

    private void OnValidate() {
        ConstructChunk();
    }
    void OnDrawGizmos() {

        Vector3 boundsCenter = transform.position + Vector3.one * chunkDimension / 2f * voxelSize;
        Vector3 boundsSize = Vector3.one * chunkDimension * voxelSize;
        Gizmos.DrawWireCube(boundsCenter, boundsSize);

        Gizmos.color = Color.blue;
        //foreach (Vector3 debugVert in debugVerts) {
        //    Vector3 center = debugVert + Vector3.one * voxelSize / 2f;
        //    Vector3 size = Vector3.one * voxelSize;
        //    // Gizmos.DrawWireCube(center, size);

        //    Gizmos.DrawSphere(debugVert, 0.05f);
        //}
    }
}
