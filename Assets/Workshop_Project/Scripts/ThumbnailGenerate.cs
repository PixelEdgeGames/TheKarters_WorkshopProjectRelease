using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode]
public class ThumbnailGenerate : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    public UnityEngine.UI.Image imageToSetSprite;

    public bool bGenerate = false;

    public Camera camera;
    public int iWidth = 2048;
    public int iHeight = 2048;
    public bool bIsTransparent = true;
    public string strFilePath = "";

    private void Update()
    {
        if(bGenerate == true )
        {
            bGenerate = false;

            imageToSetSprite.sprite = TakeScreenshootInsideAssetsDirAndReturnSprite(iWidth, iHeight, camera, bIsTransparent, strFilePath);
        }
    }
    // Function to resize the texture
   static Texture2D SupersampleResize(Texture2D sourceTex, int targetWidth, int targetHeight)
    {
        Texture2D result = new Texture2D(targetWidth, targetHeight, sourceTex.format, false);
        int scaleX = sourceTex.width / targetWidth;
        int scaleY = sourceTex.height / targetHeight;

        for (int y = 0; y < result.height; y++)
        {
            for (int x = 0; x < result.width; x++)
            {
                Color avgColor = Color.black;
                for (int sy = 0; sy < scaleY; sy++)
                {
                    for (int sx = 0; sx < scaleX; sx++)
                    {
                        avgColor += sourceTex.GetPixel((x * scaleX) + sx, (y * scaleY) + sy);
                    }
                }
                avgColor /= (scaleX * scaleY);
                result.SetPixel(x, y, LinearToSRGB( avgColor));
            }
        }

        float fadeWidth = 128.0f;
        // Fade the edges
        for (int y = 0; y < result.height; y++)
        {
            for (int x = 0; x < result.width; x++)
            {
                float alphaMultiplier = 1f;

                // Determine the shortest distance to the edge
                float minDist = Mathf.Min(x, y, result.width - 1 - x, result.height - 1 - y);

                if (minDist < fadeWidth)
                {
                    alphaMultiplier = minDist / fadeWidth;
                }

                Color currentColor = result.GetPixel(x, y);
                if(currentColor.a < 1.0f)
                    currentColor.a *= alphaMultiplier;
                result.SetPixel(x, y, currentColor);
            }
        }

        result.Apply();
        return result;
    }

   

    public static void TakeScreenshoot(int iWidth, int iHeight, Camera myCamera, bool bTransparent, string strFilePath,bool bResize = false,int iResizeWidth = 512, int iResizeHeight = 512)
    {
        int resWidthN = iWidth;
        int resHeightN = iHeight;
        RenderTexture rt = new RenderTexture(iWidth, iHeight, 24, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat);
        RenderTexture original = myCamera.targetTexture;
       myCamera.targetTexture = rt;

        TextureFormat tFormat;
        if (bTransparent)
            tFormat = TextureFormat.RGBAFloat;
        else
            tFormat = TextureFormat.RGB24;
       

        Texture2D screenShot = new Texture2D(resWidthN, resHeightN, tFormat, false);
        myCamera.Render();
        System.Threading.Thread.Sleep((int)(1 * 1000.0f));
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, resWidthN, resHeightN), 0, 0);

        byte[] bytes = null;

        if (bResize == true)
        { // 2. Resize the screenshot
            Texture2D resizedTexture = SupersampleResize(screenShot, iResizeWidth, iResizeHeight);

            bytes = resizedTexture.EncodeToPNG();
        }else
        {
            bytes = screenShot.EncodeToPNG();
        }

        myCamera.targetTexture = null;
        RenderTexture.active = null;
        string filename = strFilePath;

       // byte[] srgbBytes = ;

        System.IO.File.WriteAllBytes(filename, bytes);

        myCamera.targetTexture = original;
    }

  

    static Color LinearToSRGB(Color linearColor)
    {
        return new Color(
            LinearToSRGBComponent(linearColor.r),
            LinearToSRGBComponent(linearColor.g),
            LinearToSRGBComponent(linearColor.b),
            linearColor.a  // alpha stays the same
        );
    }

    static float LinearToSRGBComponent(float linearValue)
    {
        if (linearValue <= 0.0031308f)
            return 12.92f * linearValue;
        else
            return 1.055f * Mathf.Pow(linearValue, 1.0f / 2.4f) - 0.055f;
    }

    public static Sprite TakeScreenshootInsideAssetsDirAndReturnSprite(int iWidth, int iHeight, Camera myCamera, bool bTransparent, string strFilePath)
    {
        TakeScreenshoot(iWidth, iHeight, myCamera, bTransparent, strFilePath);

#if UNITY_EDITOR
        if (strFilePath.Contains("Assets"))
        {
            string path = strFilePath;
            UnityEditor.AssetDatabase.ImportAsset(path);
            string strPathFromAssets = path.Substring(path.IndexOf("Assets"));
            UnityEditor.TextureImporter importer = (UnityEditor.TextureImporter)UnityEditor.TextureImporter.GetAtPath(strPathFromAssets);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureType = UnityEditor.TextureImporterType.Sprite;
            // importer.mipmapEnabled = true;
          //  importer.sRGBTexture = false;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();

            return (Sprite)UnityEditor.AssetDatabase.LoadAssetAtPath(strPathFromAssets, typeof(Sprite));
        }else
        {
            Debug.LogError("path is not inside assets directory");
            return null;
        }
#endif

        return null;
    }
}
