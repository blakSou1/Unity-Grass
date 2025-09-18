using UnityEngine;

public class Grass : MonoBehaviour
{
    [SerializeField] private GrassData grassData;
    [SerializeField] private GameObject plane;

    private GrassComputeManager grassComputeManager;
    [SerializeField] private ChunkController chunkController;

    private void Awake()
    {
        chunkController.InitializeChunkBuffer(plane, grassData);

        grassComputeManager = new();

        grassComputeManager.Init(grassData, plane, chunkController);

        Terrain terrain = plane.GetComponent<Terrain>();
        grassData.computeShader.SetTexture(0, "_LayerMaskTexture",
        TerrainLayerMaskGenerator.GenerateLayerMaskTexture(terrain, grassData.IncludeLayers));

        float scaleX = 1f / terrain.terrainData.size.x;

        grassData.computeShader.SetVector("_LayerMaskTexture_ST",
        new Vector4(scaleX, scaleX, 0f, 0f));
    }

    private void Start()
    {
        grassComputeManager.UpdateGPUParams();
    }

    private void Update()
    {
        chunkController.OcclusionChunk();

#if UNITY_EDITOR
        if (grassData.testing)
        {
            grassData.texture = new Texture2D(grassData.gradientMapDimensions.x, grassData.gradientMapDimensions.y)
            {
                wrapMode = TextureWrapMode.Clamp
            };
            for (int x = 0; x < grassData.gradientMapDimensions.x; x++)
            {
                Color color = grassData.gradientClump.Evaluate((float)x / (float)grassData.gradientMapDimensions.x);
                for (int y = 0; y < grassData.gradientMapDimensions.y; y++)
                {
                    grassData.texture.SetPixel(x, y, color);
                }
            }
            grassData.texture.Apply();
            grassData.computeShader.SetTexture(0, GrassComputeManager.ClumpGradientMapId, grassData.texture);
        }
#endif

        grassComputeManager.UpdateGPUParams();

        grassComputeManager.Render(gameObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        chunkController.OnDrawGizmos();
    }
#endif

    private void OnDestroy()
    {
        grassComputeManager.Dispose();
        chunkController.Dispose();
    }
}
