using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : MonoBehaviour {

    // How did this not exist until 2021 alpha?? https://twitter.com/six_ways/status/1316419918536093698
    private static readonly Vector3Int Vector3Int_forward = new Vector3Int(0, 0, 1);

    private static readonly int TEST_CHUNKS_PER_SIDE = 4;

    [Range(1, 10)]
    public int voxelSize = 1; // 1 meter.
    [Range(1, 40)]
    public int chunkDimension = 10; // 10x10x10
    [Range(1, 10)]
    public float sphereRadius = 5f;

    private SimplexNoiseGenerator simplexNoise = new SimplexNoiseGenerator("test_seed");

    public GameObject chunkPrefab;

    private Dictionary<Vector3Int, MarchingCubesChunk> chunks = new Dictionary<Vector3Int, MarchingCubesChunk>(); 


    // Start is called before the first frame update
    void Start() {
        int chunkDimensionLength = voxelSize * chunkDimension;
        for (int i = 0; i < TEST_CHUNKS_PER_SIDE; i++) {
            for (int j = 0; j < TEST_CHUNKS_PER_SIDE; j++) {
                for (int k = 0; k < TEST_CHUNKS_PER_SIDE; k++) {
                    Vector3Int chunkIndex = new Vector3Int(i, j, k);
                    Vector3Int chunkPos = Vector3Int.RoundToInt(this.transform.position) +
                        new Vector3Int(chunkDimensionLength * i, chunkDimensionLength * j, chunkDimensionLength * k);
                    GameObject chunkObject = Instantiate(chunkPrefab);
                    chunkObject.transform.parent = this.transform;
                    chunkObject.transform.position = chunkPos;
                    MarchingCubesChunk chunk = chunkObject.GetComponent<MarchingCubesChunk>();
                    chunk.Register(this);
                    chunks.Add(chunkIndex, chunk);
                }
            }
        }

        // StartCoroutine(GenerateAllMeshesAnimated());
        GenerateAllMeshes();
    }

    // Update is called once per frame
    void Update() {
        
    }

    // EXPENSIVE AF mostly for editor debug testing.
    private void GenerateAllMeshes() {
        foreach (MarchingCubesChunk chunk in chunks.Values) {
            if (chunk) {
                chunk.Register(this);
                chunk.GenerateMesh();
            }
        }
    }

    private IEnumerator GenerateAllMeshesAnimated() {
        yield return new WaitForSeconds(1f);
        foreach (MarchingCubesChunk chunk in chunks.Values) {
            if (chunk) {
                chunk.Register(this);
                chunk.GenerateMesh();
                yield return new WaitForSeconds(0.3f);
            }
        }
    }

    public float DensitySampleLocalPosition(Vector3 localPosition) {
        int chunkDimensionLength = voxelSize * chunkDimension;

        // For now this only works when centered at 0, need to update by translating to local space of ChunkManager.
        Vector3Int localPositionInt = Vector3Int.RoundToInt(localPosition);

        Vector3Int chunkPos = localPositionInt / chunkDimensionLength;

        if (chunks.ContainsKey(chunkPos)) {
            MarchingCubesChunk chunk = chunks[chunkPos];
            if (chunk.HasCachedDensity(localPositionInt)) {
                return chunk.GetCachedDensity(localPositionInt);
            }
        }

        return CalculateDensity(localPosition);
    }

    public float CalculateDensity(Vector3 position) {
        // Sin wave balls
        // return Mathf.Sin(position.x)/3f + Mathf.Sin(position.y) / 3f + Mathf.Sin(position.z) / 3f;
        // Single ocative Perlin noise.
        // return Perlin.Noise(position);
        // Simplex Noise

        // A test sphere that is located at 20, 20, 20 with radius 8
        Vector3 planetCenter = Vector3.one * 20f;
        float planetRadius = 15f;
        float planetMask = Vector3.Distance(position, planetCenter) < planetRadius ? 1f : 0f;
        // return planetMask;

        position /= 20f;
        float simplexDensity = simplexNoise.noise(position.x, position.y, position.z) + 0.5f;
        // return simplexDensity;
        return Mathf.Min(planetMask, simplexDensity);
        // return simplexNoise.coherentNoise(position.x, position.y, position.z);

        // Implicit Sphere
        // Vector3 boundsCenter = transform.position + Vector3.one * chunkDimension / 2f * voxelSize;
        // return 1f - Vector3.Distance(position, boundsCenter) / 10f * voxelSize;
        //float strength = 0f;
        //foreach (Vector3 spherePos in spheres) {
        //    strength = Mathf.Max(strength, SpherePointWeight(spherePos, position));
        //}
        //return strength;
    }

    public void DecreasePointDenisty(Vector3 position) {
        int chunkDimensionLength = voxelSize * chunkDimension;

        // For now this only works when centered at 0, need to update by translating to local space of ChunkManager.
        Vector3Int localPositionInt = Vector3Int.RoundToInt(position);


        HashSet<MarchingCubesChunk> chunksToRefresh = new HashSet<MarchingCubesChunk>();
        for (int i = -1; i <= 1; i++) {
            for (int j = -1; j <= 1; j++) {
                for (int k = -1; k <= 1; k++) {
                    Vector3Int affectedCellPos = localPositionInt + new Vector3Int(i, j, k);
                    Vector3Int chunkIndex = affectedCellPos / chunkDimensionLength;

                    if (chunks.ContainsKey(chunkIndex)) {
                        MarchingCubesChunk chunk = chunks[chunkIndex];
                        if (!chunk.HasCachedDensity(affectedCellPos)) {
                            chunk.SetCachedDensity(affectedCellPos, CalculateDensity(affectedCellPos));
                        }
                        chunk.DecreasePointDenisty(affectedCellPos);
                        chunksToRefresh.Add(chunk);

                        // If we're on a boundary position, add the adjacent chunk to refresh list.
                        if (affectedCellPos.x + 1 % chunkDimensionLength == 0) {
                            Vector3Int boundaryChunkIndex = chunkIndex + Vector3Int.right;
                            if (chunks.ContainsKey(boundaryChunkIndex)) {
                                chunksToRefresh.Add(chunks[boundaryChunkIndex]);
                            }
                        }
                        if (affectedCellPos.y + 1 % chunkDimensionLength == 0) {
                            Vector3Int boundaryChunkIndex = chunkIndex + Vector3Int.up;
                            if (chunks.ContainsKey(boundaryChunkIndex)) {
                                chunksToRefresh.Add(chunks[boundaryChunkIndex]);
                            }
                        }
                        if (affectedCellPos.z + 1 % chunkDimensionLength == 0) {
                            Vector3Int boundaryChunkIndex = chunkIndex + Vector3Int_forward;
                            if (chunks.ContainsKey(boundaryChunkIndex)) {
                                chunksToRefresh.Add(chunks[boundaryChunkIndex]);
                            }
                        }
                    }
                }
            }
        }

        foreach (MarchingCubesChunk chunk in chunksToRefresh) {
            chunk.RefreshMesh();
        }
    }

    private float SpherePointWeight(Vector3 spherePos, Vector3 point) {
        return 1f - voxelSize * Vector3.Distance(spherePos, point) / (sphereRadius * 2);
    }

    private void OnValidate() {
        GenerateAllMeshes();
    }
}
