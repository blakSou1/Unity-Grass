using System;
using UnityEngine;

public class GrassComputeManager : IDisposable
{
    private GrassData grassData;
    private ChunkController chunkController;

    private Texture heightMap;
    private float _HeightMapScale = 200;
    private float _HeightMapMultiplier = 5;

    private ComputeBuffer grassBladesBuffer;
    private ComputeBuffer argsBuffer;
    private ComputeBuffer meshTriangles;

    private ComputeBuffer meshPositions;
    private ComputeBuffer meshColors;
    private ComputeBuffer meshUvs;

    private ComputeBuffer clumpParametersBuffer;

    private ClumpParametersStruct[] clumpParametersArray;

    private Mesh clonedMesh;

    private const int ARGS_STRIDE = sizeof(int) * 4;

    private Camera cam;

    private Bounds bounds;

    #region Param
    public static readonly int
        grassBladesBufferID = Shader.PropertyToID("_GrassBlades"),
        jitterStrengthId = Shader.PropertyToID("_JitterStrength"),
        heightMapId = Shader.PropertyToID("HeightMap"),

        distanceCullStartDistId = Shader.PropertyToID("_DistanceCullStartDist"),
        distanceCullEndDistId = Shader.PropertyToID("_DistanceCullEndDist"),

        worldSpaceCameraPositionId = Shader.PropertyToID("_WSpaceCameraPos"),

        clumpParametersId = Shader.PropertyToID("_ClumpParameters"),

        windTexID = Shader.PropertyToID("WindTex"),
        clumpTexID = Shader.PropertyToID("ClumpTex"),
        ClumpGradientMapId = Shader.PropertyToID("ClumpGradientMap"),
        vpMatrixID = Shader.PropertyToID("_VP_MATRIX"),
        FrustumCullNearOffsetId = Shader.PropertyToID("_FrustumCullNearOffset"),
        FrustumCullEdgeOffsetId = Shader.PropertyToID("_FrustumCullEdgeOffset"),
        ClumpColorUniformityId = Shader.PropertyToID("_ClumpColorUniformity"),
        CentreColorSmoothStepLowerId = Shader.PropertyToID("_CentreColorSmoothStepLower"),
        CentreColorSmoothStepUpperId = Shader.PropertyToID("_CentreColorSmoothStepUpper"),
        BigWindSpeedID = Shader.PropertyToID("_BigWindSpeed"),
        BigWindScaleID = Shader.PropertyToID("_BigWindScale"),
        BigWindRotateAmountID = Shader.PropertyToID("_BigWindRotateAmount"),
        GlobalWindFacingAngleID = Shader.PropertyToID("_GlobalWindFacingAngle"),
        GlobalWindFacingContributionID = Shader.PropertyToID("_GlobalWindFacingContribution"),
        WindControlID = Shader.PropertyToID("_WindControl"),
        DistanceCullMinimumGrassAmountlID = Shader.PropertyToID("_DistanceCullMinimumGrassAmount"),
        WindTexContrastID = Shader.PropertyToID("_WindTexContrast"),

        ClumpScaleID = Shader.PropertyToID("_ClumpScale");
    #endregion

    public void Init(GrassData grassData, GameObject plane, ChunkController chunkController)
    {
        cam ??= Camera.main;

        InitializeBasicData(grassData, chunkController);

        grassData.computeShader.SetFloat("_GrassPlatformOffsetY", plane.transform.position.y);

        CreateBuffers(plane);
        SetComputeShaderParameters();
        SetClumpParameters();
    }

    private void InitializeBasicData(GrassData grassData, ChunkController chunkController)
    {
        this.grassData = grassData;
        this.chunkController = chunkController;

        grassData.computeShader.SetFloat(distanceCullStartDistId, grassData._DistanceCullStartDist);
        grassData.computeShader.SetFloat(distanceCullEndDistId, grassData._DistanceCullEndDist);
        grassData.computeShader.SetFloat(DistanceCullMinimumGrassAmountlID, grassData._DistanceCullMinimumGrassAmount);

        grassData.computeShader.SetFloat(WindTexContrastID, grassData._WindTexContrast);

        grassData.computeShader.SetFloat(FrustumCullNearOffsetId, grassData._FrustumCullNearOffset);
        grassData.computeShader.SetFloat(FrustumCullEdgeOffsetId, grassData._FrustumCullEdgeOffset);
        grassData.computeShader.SetFloat(ClumpColorUniformityId, grassData._ClumpColorUniformity);
        grassData.computeShader.SetFloat(CentreColorSmoothStepLowerId, grassData._CentreColorSmoothStepLower);
        grassData.computeShader.SetFloat(CentreColorSmoothStepUpperId, grassData._CentreColorSmoothStepUpper);

        grassData.computeShader.SetFloat(BigWindSpeedID, grassData._BigWindSpeed);
        grassData.computeShader.SetFloat(BigWindScaleID, grassData._BigWindScale);
        grassData.computeShader.SetFloat(BigWindRotateAmountID, grassData._BigWindRotateAmount);

        grassData.computeShader.SetFloat(GlobalWindFacingAngleID, grassData._GlobalWindFacingAngle);
        grassData.computeShader.SetFloat(GlobalWindFacingContributionID, grassData._GlobalWindFacingContribution);
        grassData.computeShader.SetFloat(WindControlID, grassData._WindControl);

        grassData.computeShader.SetFloat(ClumpScaleID, grassData.ClumpScale);

        grassData.grassMesh.material.SetFloat(WindControlID, grassData._WindControl);
    }

    private void CreateBuffers(GameObject plane)
    {
        grassBladesBuffer = new ComputeBuffer(
            grassData.resolution * grassData.resolution,
            sizeof(float) * 18,
            ComputeBufferType.Append
        );
        grassBladesBuffer.SetCounterValue(0);

        Height(plane);
        Keyword();
        if(grassData.enableClumpDisabled)
            Voronoi();

        ClonedMesh();
        SetBufferMesh();

        bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);

        argsBuffer = new ComputeBuffer(1, ARGS_STRIDE, ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(new int[] { meshTriangles.count, 0, 0, 0 });
    }

    private void SetComputeShaderParameters()
    {
        var computeShader = grassData.computeShader;

        computeShader.SetFloat(jitterStrengthId, grassData.jitterStrength);
        computeShader.SetTexture(0, heightMapId, heightMap);
        computeShader.SetBuffer(0, grassBladesBufferID, grassBladesBuffer);

        computeShader.SetFloat("_HeightMapScale", _HeightMapScale);
        computeShader.SetFloat("_HeightMapMultiplier", _HeightMapMultiplier);

        if(grassData.enableWindDisabled)
            computeShader.SetTexture(0, windTexID, grassData.WindTex);
        computeShader.SetTexture(0, ClumpGradientMapId, grassData.texture);
    }

    private void SetClumpParameters()
    {
        clumpParametersBuffer = new ComputeBuffer(
            grassData.clumpParameters.Count,
            sizeof(float) * 10
        );

        UpdateGrassArtistParameters();
    }

    /// <summary>
    /// gpu buffers for the mesh
    /// </summary>
    private void SetBufferMesh()
    {
        int[] triangles = clonedMesh.triangles;
        meshTriangles = new ComputeBuffer(triangles.Length, sizeof(int));
        meshTriangles.SetData(triangles);

        Vector3[] positions = clonedMesh.vertices;
        meshPositions = new ComputeBuffer(positions.Length, sizeof(float) * 3);
        meshPositions.SetData(positions);

        Color[] colors = clonedMesh.colors;
        meshColors = new ComputeBuffer(colors.Length, sizeof(float) * 4);
        meshColors.SetData(colors);

        Vector2[] uvs = clonedMesh.uv;
        meshUvs = new ComputeBuffer(uvs.Length, sizeof(float) * 2);
        meshUvs.SetData(uvs);

        grassData.grassMesh.material.SetBuffer("Triangles", meshTriangles);
        grassData.grassMesh.material.SetBuffer("Positions", meshPositions);
        grassData.grassMesh.material.SetBuffer("Colors", meshColors);
        grassData.grassMesh.material.SetBuffer("Uvs", meshUvs);
    }

    private void ClonedMesh()
    {
        Mesh originalMesh = grassData.grassMesh.originalMesh;

        clonedMesh = new Mesh
        {
            name = "ClonedGrassMesh",
            vertices = originalMesh.vertices,
            triangles = originalMesh.triangles,
            normals = originalMesh.normals,
            uv = originalMesh.uv
        };

        Color[] newColors = originalMesh.colors.Length > 0
        ? originalMesh.colors
        : GenerateFallbackColors(originalMesh.vertices.Length);

        for (int i = 0; i < newColors.Length; i++)
        {
            Color col = newColors[i];

            col.r = Mathf.Pow(col.r, grassData._VertexPlacementPower);

            newColors[i] = col;
        }

        clonedMesh.colors = newColors;
    }

    private Color[] GenerateFallbackColors(int vertexCount)
    {
        Color[] colors = new Color[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            colors[i] = new Color(
                UnityEngine.Random.Range(0.5f, 1f),
                UnityEngine.Random.value > 0.5f ? 1f : 0f,
                0,
                1
            );
        }
        return colors;
    }

    private void Keyword()
    {
        if (grassData.DISTANCE_CULL_ENABLED)
            grassData.computeShader.EnableKeyword("DISTANCE_CULL_ENABLED");
        else
            grassData.computeShader.DisableKeyword("DISTANCE_CULL_ENABLED");

        if (grassData.enableClumpColoring)
            grassData.grassMesh.material.EnableKeyword("USE_CLUMP_COLORS");
        else
            grassData.grassMesh.material.DisableKeyword("USE_CLUMP_COLORS");

        if (grassData.enableClumpDisabled)
            grassData.computeShader.EnableKeyword("CLUMP_DISABLED");
        else
            grassData.computeShader.DisableKeyword("CLUMP_DISABLED");
            
        if (grassData.enableWindDisabled)
            grassData.computeShader.EnableKeyword("WIND_DISABLED");
        else
            grassData.computeShader.DisableKeyword("WIND_DISABLED");
    }

    private void Height(GameObject plane)
    {
        Terrain terrain = plane.GetComponent<Terrain>();
        heightMap = terrain.terrainData.heightmapTexture;
        _HeightMapScale = terrain.terrainData.size.x;

        grassData.computeShader.SetFloat("_TwiceTerrainHeight", terrain.terrainData.size.y * 2f);
        grassData.computeShader.SetFloat("_TerrainSize", terrain.terrainData.size.x);
        grassData.computeShader.SetVector("_terrainCenter", terrain.transform.position);

        _HeightMapMultiplier = terrain.terrainData.size.y;
    }

    private void Voronoi()
    {
        grassData.clumpingVoronoiMat.SetFloat("_NumClumps", grassData.clumpParameters.Count);
        Texture2D startTex = new(grassData.clumpTexWidth, grassData.clumpTexHeight, TextureFormat.RGBAFloat, false, true);
        RenderTexture clumpVoronoiTex = RenderTexture.GetTemporary(grassData.clumpTexWidth, grassData.clumpTexHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        Graphics.Blit(startTex, clumpVoronoiTex, grassData.clumpingVoronoiMat, 0);

        RenderTexture.active = clumpVoronoiTex;
        Texture2D clumpTex = new(grassData.clumpTexWidth, grassData.clumpTexHeight, TextureFormat.RGBAFloat, false, true)
        {
            filterMode = FilterMode.Point
        };

        clumpTex.ReadPixels(new Rect(0, 0, grassData.clumpTexWidth, grassData.clumpTexHeight), 0, 0, true);
        clumpTex.Apply();
        RenderTexture.active = null;

        grassData.computeShader.SetTexture(0, clumpTexID, clumpTex);

        RenderTexture.ReleaseTemporary(clumpVoronoiTex);
    }

    public void Render(GameObject gameObject)
    {
        Graphics.DrawProceduralIndirect(grassData.grassMesh.material, bounds, MeshTopology.Triangles, argsBuffer,
            0, null, null, UnityEngine.Rendering.ShadowCastingMode.Off, true, gameObject.layer);
    }

    private void UpdateGrassArtistParameters()
    {
        clumpParametersArray = new ClumpParametersStruct[grassData.clumpParameters.Count];

        for (int i = 0; i < grassData.clumpParameters.Count; i++)
            clumpParametersArray[i] = grassData.clumpParameters[i];

        clumpParametersBuffer.SetData(clumpParametersArray);
        grassData.computeShader.SetBuffer(0, clumpParametersId, clumpParametersBuffer);
    }

    public void UpdateGPUParams()
    {
        grassBladesBuffer.SetCounterValue(0);

        grassData.computeShader.SetVector(worldSpaceCameraPositionId, cam.transform.position);

        Matrix4x4 projMat = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
        Matrix4x4 VP = projMat * cam.worldToCameraMatrix;

        grassData.computeShader.SetMatrix(vpMatrixID, VP);

        if (chunkController.visibleChunks.Count > 0)
        {
            int chunkGroupsX = Mathf.Max(1, Mathf.CeilToInt(chunkController.visibleChunks.Count / 8f));
            int grassGroupsY = Mathf.Max(1, Mathf.CeilToInt(grassData.resolution / 8f));

            grassData.computeShader.Dispatch(0, chunkGroupsX, grassGroupsY, grassGroupsY);//start Main in comput shader
        }

        ComputeBuffer.CopyCount(grassBladesBuffer, argsBuffer, sizeof(int));

        grassData.grassMesh.material.SetBuffer(grassBladesBufferID, grassBladesBuffer);
        grassData.grassMesh.material.SetVector(worldSpaceCameraPositionId, cam.transform.position);
    }

    public void Dispose()
    {
        grassBladesBuffer.Dispose();
        clumpParametersBuffer.Dispose();
        argsBuffer.Dispose();
        meshTriangles.Dispose();
        meshPositions.Dispose();
        meshColors.Dispose();
        meshUvs.Dispose();
    }
}