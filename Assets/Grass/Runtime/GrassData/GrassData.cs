using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GrassData", menuName = "Grass/GrassData")]
public class GrassData : ScriptableObject
{
    public TerrainLayer[] IncludeLayers;


    [Header("Linked objects")]
    public ComputeShader computeShader;
    public GrassMesh grassMesh;

    [Header("Grass placement")]
    [Range(10, 4000)]
    public int resolution = 10;
    public float jitterStrength = 5;

    [Header("Wind")]
    public Texture WindTex;
    public float _GlobalWindFacingContribution = 0;
    public float _GlobalWindFacingAngle = 0;
    [Range(0, 1)]
    public float _WindControl = .2f;
    public float _BigWindSpeed = .01f;
    public float _BigWindScale = 0.005f;
    public float _BigWindRotateAmount = .6f;
    public float _WindTexContrast = 1;

    [Header("Grass shape")]
    public float _VertexPlacementPower = .6f;

    [Header("Culling")]
    public float _DistanceCullStartDist = 30;
    public float _DistanceCullEndDist = 86.93f;
    public float _DistanceCullMinimumGrassAmount = .2f;
    public bool DISTANCE_CULL_ENABLED = true;
    public float _FrustumCullNearOffset = -5;
    public float _FrustumCullEdgeOffset = -2;

    [Header("Clumping")]
    public int clumpTexHeight = 512;
    public int clumpTexWidth = 512;
    public Material clumpingVoronoiMat;

    public float ClumpScale = 0.1f;

    public List<ClumpParametersStruct> clumpParameters;

    [Header("Clump gradient map")]
    public bool enableClumpColoring = true;
    public float _CentreColorSmoothStepLower = 0;
    public float _CentreColorSmoothStepUpper = 0.41f;
    public float _ClumpColorUniformity = .5f;
    public Vector2Int gradientMapDimensions = new(128, 32);

#if UNITY_EDITOR
    public bool testing = false;
    public Gradient gradientClump;
#endif

    public Texture2D texture;
}

[Serializable]
public struct ClumpParametersStruct
{
    public float pullToCentre;
    public float pointInSameDirection;
    public float baseHeight;
    public float heightRandom;
    public float baseWidth;
    public float widthRandom;
    public float baseTilt;
    public float tiltRandom;
    public float baseBend;
    public float bendRandom;
};