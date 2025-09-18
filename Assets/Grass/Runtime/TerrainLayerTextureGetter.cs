using UnityEngine;

public class TerrainLayerMaskGenerator
{
    public static Texture2D GenerateLayerMaskTexture(Terrain terrain, TerrainLayer[] includeLayers)
    {
        if (terrain == null || includeLayers == null || includeLayers.Length == 0)
        {
            Debug.LogError("Invalid input parameters");
            return null;
        }

        TerrainData terrainData = terrain.terrainData;
        int width = terrainData.alphamapWidth;
        int height = terrainData.alphamapHeight;
        
        TerrainLayer[] allLayers = terrainData.terrainLayers;
        
        int[] includeLayerIndices = new int[includeLayers.Length];
        
        for (int i = 0; i < includeLayers.Length; i++)
        {
            includeLayerIndices[i] = -1;
            for (int j = 0; j < allLayers.Length; j++)
            {
                if (allLayers[j] == includeLayers[i])
                {
                    includeLayerIndices[i] = j;
                    break;
                }
            }
            
            if (includeLayerIndices[i] == -1)
                Debug.LogWarning($"Layer {includeLayers[i].name} not found in terrain");
        }
        
        float[,,] alphaMaps = terrainData.GetAlphamaps(0, 0, width, height);
        
        Texture2D layerMaskTexture = new Texture2D(width, height, TextureFormat.R8, false);
        layerMaskTexture.wrapMode = TextureWrapMode.Clamp;
        layerMaskTexture.filterMode = FilterMode.Bilinear;
        
        byte[] textureData = new byte[width * height];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float totalInfluence = 0f;
                
                foreach (int layerIndex in includeLayerIndices)
                {
                    if (layerIndex >= 0 && layerIndex < alphaMaps.GetLength(2))
                        totalInfluence += alphaMaps[y, x, layerIndex];
                }
                
                textureData[y * width + x] = (byte)(Mathf.Clamp01(totalInfluence) * 255);
            }
        }
        
        layerMaskTexture.LoadRawTextureData(textureData);
        layerMaskTexture.Apply();
        
        return layerMaskTexture;
    }
    
}