using UnityEngine;

public class ClumpingVoronoi : MonoBehaviour//попытка реализовать генерацию шума вороного пока в разработке
{
    public int width;
    public int height;

    public Material clumpingVoronoiMat;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        RenderTexture src = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.R8);
        //RenderTexture dst = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.R8);
        Graphics.Blit(src, destination, clumpingVoronoiMat, 0);


        //Graphics.Blit(source,destination, clumpingVoronoiMat);

        //RenderTexture rt = Selection.activeObject as RenderTexture;

        //RenderTexture.active = dst;
        //Texture2D tex = new Texture2D(dst.width, dst.height, TextureFormat.RGB24, false);
        //tex.ReadPixels(new Rect(0, 0, dst.width, dst.height), 0, 0);
        //RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(src);
        //byte[] bytes;
        //bytes = tex.EncodeToPNG();

        //string path = AssetDatabase.GetAssetPath(dst) + ".png";
        //System.IO.File.WriteAllBytes(path, bytes);
        //AssetDatabase.ImportAsset(path);

    }
}
