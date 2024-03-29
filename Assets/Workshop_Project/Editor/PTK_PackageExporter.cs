using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography;

public class PTK_PackageExporter : EditorWindow
{
    public PTK_ModInfo currentMod;
    public PTK_AddressableAssetsHandler addressableHelper = new PTK_AddressableAssetsHandler();
    public PTK_PackageExporterGUI packageExporterGUI = new PTK_PackageExporterGUI();

    public BoundingBoxCalculator boundingBoxCalculator;

    public PTK_SkinTreeView treeView;
    private TreeViewState treeViewState;

    public bool bRegenerateThumbnails = false;

    [MenuItem("PixelTools/PTK Package Exporter", false, 333)]
    public static void ShowWindow()
    {
        GetWindow<PTK_PackageExporter>("PTK Package Exporter");
    }


    [MenuItem("PixelTools/Open Scene: Workshop Content Gen", false, -333)]
    public static void WorkshopItemsScene()
    {
        if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Workshop_Project/Scenes/WorkshopGenScene.unity");
        }
    }


    private void OnEnable()
    {
        if (treeViewState == null)
            treeViewState = new TreeViewState();

        treeView = new PTK_SkinTreeView(treeViewState);
        treeView.LoadStateFromPrefs();  // Load state from EditorPrefs
        treeView.Reload();

        packageExporterGUI.OnEnable(this);


        treeView.OnCheckedItemsChanged += HandleCheckedItemsChanged;

        // Register the callback when the editor window is enabled
        Undo.undoRedoPerformed += OnUndoRedo;

    }

    void OnDisable()
    {
        if (treeView != null)
            treeView.SaveStateToPrefs();  // Save state when window is disabled

        treeView.OnCheckedItemsChanged -= HandleCheckedItemsChanged;

        // Unregister the callback when the editor window is disabled to avoid memory leaks
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    private void OnFocus()
    {
        RefreshTreeViewDirectories();
    }

    private void OnUndoRedo()
    {
        // This method is called after an undo or redo operation is performed.
        // Repaint the editor window to reflect the changes.
        Repaint();
    }

    private void HandleCheckedItemsChanged(HashSet<string> checkedItems)
    {
        if (currentMod != null)
        {
            currentMod.SelectedPaths = checkedItems.ToList();
            EditorUtility.SetDirty(currentMod); // Mark the ScriptableObject as "dirty" so that changes are saved.
        }
    }

    private void OnGUI()
    {
        packageExporterGUI.EventOnGUI(this);
    }

    public void ExportModPackage(bool _bRegenerateThumbnails)
    {
        bRegenerateThumbnails = _bRegenerateThumbnails;

        foreach (var dirName in treeView.GetCheckedItems())
        {
            OptimizeTextureSizesInDirectory(dirName);
        }

        boundingBoxCalculator = GameObject.FindObjectOfType<BoundingBoxCalculator>(true);

        addressableHelper.ExportToAddressables(this);
    }

    public void RefreshTreeViewDirectories()
    {
        if (currentMod != null)
        {
            for (int i = 0; i < currentMod.SelectedPaths.Count; i++)
            {
                if (Directory.Exists(currentMod.SelectedPaths[i]) == false)
                {
                    treeView.RemovePath(currentMod.SelectedPaths[i]);
                    currentMod.SelectedPaths.RemoveAt(i);
                    i--;
                }
            }
        }

        if(treeView != null)
        {
            treeView.RefreshIgnorePhrases();
            treeView.Reload();
        }
    }

    public  void OptimizeTextureSizesInDirectory(string directoryPath)
    {
        // Ensure the directory path starts with "Assets"
        if (!directoryPath.StartsWith("Assets"))
        {
            Debug.LogError("The directory path should start with 'Assets'.");
            return;
        }

        // Get all the material paths in the directory
        string[] materialPaths = AssetDatabase.FindAssets("t:Material", new string[] { directoryPath });

        for (int i = 0; i < materialPaths.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(materialPaths[i]);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat.HasProperty("_MainTex")) // Assuming "_MainTex" is the property name for diffuse textures
            {
                Texture2D diffuseTexture = mat.GetTexture("_MainTex") as Texture2D;
                if (diffuseTexture)
                {
                    SetTextureMaxSize(AssetDatabase.GetAssetPath(diffuseTexture), 2048);
                }
            }

            // Iterate through other textures in the material
            foreach (string texturePropertyName in mat.GetTexturePropertyNames())
            {
                if (texturePropertyName != "_MainTex")
                {
                    Texture2D texture = mat.GetTexture(texturePropertyName) as Texture2D;
                    if (texture)
                    {
                        SetTextureMaxSize(AssetDatabase.GetAssetPath(texture), 1024);
                    }
                }
            }

            // Display progress bar
            float progress = (float)i / materialPaths.Length;
           
        }

    }

    private  void SetTextureMaxSize(string texturePath, int maxSize)
    {
        TextureImporter textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (textureImporter && textureImporter.maxTextureSize != maxSize)
        {
            textureImporter.maxTextureSize = maxSize;
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
        }
    }
}



