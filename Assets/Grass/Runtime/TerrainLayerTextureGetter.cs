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

        // Получаем данные terrain
        TerrainData terrainData = terrain.terrainData;
        int width = terrainData.alphamapWidth;
        int height = terrainData.alphamapHeight;
        
        // Получаем все слои terrain
        TerrainLayer[] allLayers = terrainData.terrainLayers;
        
        // Создаем массив для хранения индексов включенных слоев
        int[] includeLayerIndices = new int[includeLayers.Length];
        
        // Находим индексы включенных слоев
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
            {
                Debug.LogWarning($"Layer {includeLayers[i].name} not found in terrain");
            }
        }
        
        // Получаем альфамапы
        float[,,] alphaMaps = terrainData.GetAlphamaps(0, 0, width, height);
        
        // Создаем текстуру для маски
        Texture2D layerMaskTexture = new Texture2D(width, height, TextureFormat.R8, false);
        layerMaskTexture.wrapMode = TextureWrapMode.Clamp;
        layerMaskTexture.filterMode = FilterMode.Bilinear;
        
        // Создаем массив для данных текстуры
        byte[] textureData = new byte[width * height];
        
        // Заполняем текстуру данными
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float totalInfluence = 0f;
                
                // Суммируем влияние всех включенных слоев
                foreach (int layerIndex in includeLayerIndices)
                {
                    if (layerIndex >= 0 && layerIndex < alphaMaps.GetLength(2))
                    {
                        totalInfluence += alphaMaps[y, x, layerIndex];
                    }
                }
                
                // Нормализуем и сохраняем значение (0-255)
                textureData[y * width + x] = (byte)(Mathf.Clamp01(totalInfluence) * 255);
            }
        }
        
        // Применяем данные к текстуре
        layerMaskTexture.LoadRawTextureData(textureData);
        layerMaskTexture.Apply();
        
        return layerMaskTexture;
    }
    
}