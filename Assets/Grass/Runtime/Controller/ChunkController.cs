using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[Serializable]
public class ChunkController : IDisposable
{
    private GrassData grassData;

    [SerializeField] private int chunkCount;
    private ChunkBuffer[] chunkBuffers;

    public List<ChunkBuffer> visibleChunks = new();

    private ComputeBuffer chunkBuffer;
    private Camera mainCamera;
    private Plane[] cameraFrustumPlanes = new Plane[6];

    public void InitializeChunkBuffer(GameObject plane, GrassData grassData)
    {
        this.grassData = grassData;

        chunkBuffers = new ChunkBuffer[chunkCount];

        Renderer planeRenderer = plane.GetComponent<Renderer>();
        Bounds planeBounds = planeRenderer.bounds;
        Vector3 planeSize = planeBounds.size;

        int chunksPerSide = Mathf.CeilToInt(Mathf.Sqrt(chunkCount));

        float chunkWidth = planeSize.x / chunksPerSide;
        float chunkLength = planeSize.z / chunksPerSide;

        float chunkSize = Mathf.Max(chunkWidth, chunkLength);

        Vector3 planeMin = planeBounds.min;

        for (int i = 0; i < chunkCount; i++)
        {
            int xIndex = i % chunksPerSide;
            int zIndex = i / chunksPerSide;

            float centerX = planeMin.x + (xIndex * chunkSize) + (chunkSize / 2f);
            float centerZ = planeMin.z + (zIndex * chunkSize) + (chunkSize / 2f);

            float height = GetHeightFromHeightmap(plane, new Vector2(centerX, centerZ));

            chunkBuffers[i] = new()
            {
                chunkId = (uint)i,
                grassAmount = (uint)grassData.resolution,
                isVisible = 0,
                minBounds = new Vector3(
                    centerX - chunkSize / 2f,
                    height - 10f, // Небольшой отступ вниз для рельефа
                    centerZ - chunkSize / 2f
                ),
                maxBounds = new Vector3(
                    centerX + chunkSize / 2f,
                    height + 10f, // Небольшой отступ вверх для рельефа
                    centerZ + chunkSize / 2f
                )
            };
        }

        grassData.computeShader.SetInt("_ChunkCount", 0);
    }

    private float GetHeightFromHeightmap(GameObject plane, Vector2 worldXZ)
    {
        Material objectRenderer;
        objectRenderer = plane.GetComponent<Renderer>().material;

        Texture heightMap = objectRenderer.GetTexture("_Heightmap");
        Vector2 heightmapTiling = objectRenderer.GetTextureScale("_Heightmap");

        float _HeightMapScale = heightmapTiling.x;
        float _HeightMapMultiplier = objectRenderer.GetFloat("_HeightMul");

        if (heightMap == null) return 0f;

        Vector2 uv = new(
            (worldXZ.x + _HeightMapScale / 2f) / _HeightMapScale,
            (worldXZ.y + _HeightMapScale / 2f) / _HeightMapScale
        );

        uv = Vector2.ClampMagnitude(uv, 1f);

        if (heightMap is Texture2D heightMapTex)
        {
            Color heightColor = heightMapTex.GetPixelBilinear(uv.x, uv.y);
            return heightColor.r * _HeightMapMultiplier;
        }

        return 0f;
    }

    public void OcclusionChunk()
    {
        mainCamera ??= Camera.main;

        // Получаем плоскости пирамиды видимости камеры
        GeometryUtility.CalculateFrustumPlanes(mainCamera, cameraFrustumPlanes);

        visibleChunks.Clear();

        for (int i = 0; i < chunkBuffers.Length; i++)
        {
            // Создаем bounds для чанка
            Bounds chunkBounds = new();
            chunkBounds.SetMinMax(chunkBuffers[i].minBounds, chunkBuffers[i].maxBounds);

            // Проверяем видимость с помощью плоскостей пирамиды видимости
            chunkBuffers[i].isVisible = CheckChunkVisibility(chunkBounds);

            // Дополнительная проверка расстояния
            float distanceToCamera = Vector3.Distance((chunkBuffers[i].minBounds + chunkBuffers[i].maxBounds) / 2f, mainCamera.transform.position);
            bool isInRenderDistance = distanceToCamera < grassData._DistanceCullEndDist;

            if (!isInRenderDistance)
            {
                chunkBuffers[i].isVisible = 0;
                continue;
            }

            if (chunkBuffers[i].isVisible > 0)
                visibleChunks.Add(chunkBuffers[i]);
        }

        UpdateComputeBuffer(visibleChunks);
    }
    
    private void UpdateComputeBuffer(List<ChunkBuffer> visibleChunks)
    {
        if (visibleChunks.Count == 0) return;

        chunkBuffer?.Release();
        chunkBuffer = null;

        chunkBuffer = new ComputeBuffer(
            visibleChunks.Count,
            Marshal.SizeOf(typeof(ChunkBuffer))
        );

        // Заполняем буфер данными видимых чанков
        chunkBuffer.SetData(visibleChunks.ToArray());

        grassData.computeShader.SetBuffer(0, "_ChunkBuffer", chunkBuffer);
        grassData.computeShader.SetInt("_ChunkCount", visibleChunks.Count);
    }

    private uint CheckChunkVisibility(Bounds bounds)
    {
        if (bounds.Contains(mainCamera.transform.position))
            return 1;

        var result = GeometryUtility.TestPlanesAABB(cameraFrustumPlanes, bounds);

        if (result)
        {
            Vector3[] corners = GetBoundsCorners(bounds);
            bool allCornersInside = true;

            foreach (var corner in corners)
            {
                Vector3 viewportPoint = mainCamera.WorldToViewportPoint(corner);
                if (viewportPoint.z < 0 || 
                    viewportPoint.x < 0 || viewportPoint.x > 1 ||
                    viewportPoint.y < 0 || viewportPoint.y > 1)
                {
                    allCornersInside = false;
                    break;
                }
            }

            return allCornersInside ? (uint)2 : (uint)1;
        }

        return 0;
    }

    private Vector3[] GetBoundsCorners(Bounds bounds)
    {
        Vector3[] corners = new Vector3[8];
        corners[0] = bounds.min;
        corners[1] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
        corners[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
        corners[3] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
        corners[4] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
        corners[5] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
        corners[6] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
        corners[7] = bounds.max;
        return corners;
    }

#if UNITY_EDITOR
    public void OnDrawGizmos()
    {
        if (chunkBuffers == null || chunkCount <= 0) return;

        for (int i = 0; i < chunkBuffers.Length; i++)
        {
            // Выбор цвета
            Color chunkColor = chunkBuffers[i].isVisible == 2 ? Color.green :
                               chunkBuffers[i].isVisible == 1 ? Color.magenta :
                               Color.gray;

            // Расчет центра и размера
            Vector3 size = chunkBuffers[i].maxBounds - chunkBuffers[i].minBounds;
            Vector3 center = (chunkBuffers[i].minBounds + chunkBuffers[i].maxBounds) / 2f;

            // Установка цветов для Gizmos и Handles
            Gizmos.color = chunkColor;
            UnityEditor.Handles.color = chunkColor;

            // Отрисовка куба
            Gizmos.DrawWireCube(center, size);

            // Линия между минимальной и максимальной точками
            Gizmos.DrawLine(chunkBuffers[i].minBounds, chunkBuffers[i].maxBounds);

            // Текстовая метка с информацией о чанке
            UnityEditor.Handles.Label(
                (chunkBuffers[i].minBounds + chunkBuffers[i].maxBounds) / 2f,
                $"Chunk {chunkBuffers[i].chunkId}\n" +
                $"Grass: {chunkBuffers[i].grassAmount}\n" +
                $"Visible: {chunkBuffers[i].isVisible > 0}\n"
            );
        }
    }
#endif

    public void Dispose()
    {
        chunkBuffer?.Dispose();
    }

}

[StructLayout(LayoutKind.Sequential)]
public struct ChunkBuffer
{
    public uint chunkId;
    public uint grassAmount;
    public uint isVisible;
    public Vector3 minBounds;
    public Vector3 maxBounds;
}