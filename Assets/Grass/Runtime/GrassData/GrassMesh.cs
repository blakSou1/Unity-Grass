using UnityEngine;

[CreateAssetMenu(fileName = "GrassMesh", menuName = "Grass/GrassMesh")]
public class GrassMesh : ScriptableObject
{
    public Material material;
    public Mesh originalMesh;

}