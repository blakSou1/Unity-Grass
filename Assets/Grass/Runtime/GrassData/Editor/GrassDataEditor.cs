using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GrassData))]
public class GrassDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return;

        GrassData grassData = (GrassData)target;

        if (GUILayout.Button("UpdateParamInRuntime"))
        {
            grassData.computeShader.SetFloat(GrassComputeManager.distanceCullStartDistId, grassData._DistanceCullStartDist);
            grassData.computeShader.SetFloat(GrassComputeManager.distanceCullEndDistId, grassData._DistanceCullEndDist);
            grassData.computeShader.SetFloat(GrassComputeManager.DistanceCullMinimumGrassAmountlID, grassData._DistanceCullMinimumGrassAmount);

            grassData.computeShader.SetFloat(GrassComputeManager.WindTexContrastID, grassData._WindTexContrast);

            grassData.computeShader.SetFloat(GrassComputeManager.FrustumCullNearOffsetId, grassData._FrustumCullNearOffset);
            grassData.computeShader.SetFloat(GrassComputeManager.FrustumCullEdgeOffsetId, grassData._FrustumCullEdgeOffset);
            grassData.computeShader.SetFloat(GrassComputeManager.ClumpColorUniformityId, grassData._ClumpColorUniformity);
            grassData.computeShader.SetFloat(GrassComputeManager.CentreColorSmoothStepLowerId, grassData._CentreColorSmoothStepLower);
            grassData.computeShader.SetFloat(GrassComputeManager.CentreColorSmoothStepUpperId, grassData._CentreColorSmoothStepUpper);

            grassData.computeShader.SetFloat(GrassComputeManager.BigWindSpeedID, grassData._BigWindSpeed);
            grassData.computeShader.SetFloat(GrassComputeManager.BigWindScaleID, grassData._BigWindScale);
            grassData.computeShader.SetFloat(GrassComputeManager.BigWindRotateAmountID, grassData._BigWindRotateAmount);

            grassData.computeShader.SetFloat(GrassComputeManager.GlobalWindFacingAngleID, grassData._GlobalWindFacingAngle);
            grassData.computeShader.SetFloat(GrassComputeManager.GlobalWindFacingContributionID, grassData._GlobalWindFacingContribution);
            grassData.computeShader.SetFloat(GrassComputeManager.WindControlID, grassData._WindControl);

            grassData.computeShader.SetFloat(GrassComputeManager.ClumpScaleID, grassData.ClumpScale);

            grassData.grassMesh.material.SetFloat(GrassComputeManager.WindControlID, grassData._WindControl);
        }

    }

}
