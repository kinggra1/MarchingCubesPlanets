using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class MarchingCubesChunk : MonoBehaviour {
    // How did this not exist until 2021 alpha?? https://twitter.com/six_ways/status/1316419918536093698
    private static readonly Vector3Int Vector3Int_forward = new Vector3Int(0, 0, 1);

    public int voxelSize = 1; // 1 meter.
    public int chunkDimension = 10; // 10x10x10

    public GameObject debugVertex;

    private Dictionary<Vector3Int, float> vertexDensities = new Dictionary<Vector3Int, float>();
    private TriangleTable.Gridcell[,,] cells;

    private ChunkManager chunkManager;
    // based off of object's starting position. Fixed for consistent regeneration.
    private Vector3Int noiseRootPosition; 
    private Mesh mesh;

    private void Awake() {
        mesh = new Mesh();
        mesh.name = "MarchingCubesMesh";
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;
    }
    private void Start() {

    }

    public void Register(ChunkManager manager) {
        noiseRootPosition = Vector3Int.RoundToInt(this.transform.localPosition);
        this.chunkManager = manager;
        this.voxelSize = manager.voxelSize;
        this.chunkDimension = manager.chunkDimension;
    }

    public void GenerateMesh() {
        cells = new TriangleTable.Gridcell[chunkDimension, chunkDimension, chunkDimension];
        int vertCount = (int)chunkDimension + 1;

        for (int x = 0; x < vertCount; x++) {
            for (int y = 0; y < vertCount; y++) {
                for (int z = 0; z < vertCount; z++) {
                    Vector3Int vertPosition = noiseRootPosition + new Vector3Int(x * voxelSize, y * voxelSize, z * voxelSize);
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

                        cell.strength[0] = FetchOrCalculateStrength(pos0);
                        cell.strength[1] = FetchOrCalculateStrength(pos1);
                        cell.strength[2] = FetchOrCalculateStrength(pos2);
                        cell.strength[3] = FetchOrCalculateStrength(pos3);
                        cell.strength[4] = FetchOrCalculateStrength(pos4);
                        cell.strength[5] = FetchOrCalculateStrength(pos5);
                        cell.strength[6] = FetchOrCalculateStrength(pos6);
                        cell.strength[7] = FetchOrCalculateStrength(pos7);

                        cells[x, y, z] = cell;
                    }
                }
            }
        }

        RefreshMesh();   
    }

    private IEnumerator MeshGeneration() {
        UpdateCellStrengths();

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
                vertices[i * 3 + v] = triangles[i].verts[v] - noiseRootPosition;
                triangleIndices[i * 3 + v] = i * 3 + v;
            }
            // SetMeshValues(vertices, triangleIndices);
            // yield return new WaitForSecondsRealtime(0.0005f);
        }
        SetMeshValues(vertices, triangleIndices);
        yield return null;
    }

    public void RefreshMesh() {
        StartCoroutine(MeshGeneration());
    }

    private void UpdateCellStrengths() {
        int vertCount = (int)chunkDimension + 1;
        for (int x = 0; x < vertCount; x++) {
            for (int y = 0; y < vertCount; y++) {
                for (int z = 0; z < vertCount; z++) {
                    if (x != chunkDimension && y != chunkDimension && z != chunkDimension) {
                        TriangleTable.Gridcell cell = cells[x, y, z];

                        cell.strength[0] = FetchOrCalculateStrength(cell.position[0]);
                        cell.strength[1] = FetchOrCalculateStrength(cell.position[1]);
                        cell.strength[2] = FetchOrCalculateStrength(cell.position[2]);
                        cell.strength[3] = FetchOrCalculateStrength(cell.position[3]);
                        cell.strength[4] = FetchOrCalculateStrength(cell.position[4]);
                        cell.strength[5] = FetchOrCalculateStrength(cell.position[5]);
                        cell.strength[6] = FetchOrCalculateStrength(cell.position[6]);
                        cell.strength[7] = FetchOrCalculateStrength(cell.position[7]);

                        cells[x, y, z] = cell;
                    }
                }
            }

        }
    }

    public void SetMeshValues(Vector3[] vertices, int[] triangleIndices) {
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangleIndices;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        Physics.BakeMesh(mesh.GetInstanceID(), false);
    }

    private float FetchOrCalculateStrength(Vector3Int position) {
        if (HasCachedDensity(position)) {
            return GetCachedDensity(position);
        }
        float density = chunkManager.DensitySampleLocalPosition(position);
        vertexDensities.Add(position, density);
        return density;
    }

    public void ReceivePlayerClick(Vector3 position) {
        chunkManager.DecreasePointDenisty(position);
    }

    public void DecreasePointDenisty(Vector3Int position) {
        SetCachedDensity(position, 0f);
    }

    public bool HasCachedDensity(Vector3Int position) {
        return vertexDensities.ContainsKey(position);
    }
    public float GetCachedDensity(Vector3Int position) {
        return vertexDensities[position];
    }

    public void SetCachedDensity(Vector3Int position, float value) {
        vertexDensities[position] = value;
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

    }

    private void OnValidate() {
        // GenerateMesh();
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
