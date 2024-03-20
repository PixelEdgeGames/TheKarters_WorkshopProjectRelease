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
    private SkinTreeView treeView;
    private TreeViewState treeViewState;

    private List<PTK_ModInfo> allMods = new List<PTK_ModInfo>();
    private PTK_ModInfo currentMod;
    private int selectedIndex = -1;
    private int iLastSelectedIndex = -1;


    [MenuItem("PixelTools/PTK Package Exporter")]
    public static void ShowWindow()
    {
        GetWindow<PTK_PackageExporter>("PTK Package Exporter");
    }

    string strModSO_Path = "Assets/Workshop_Project/LocalUserModsGenerationConfigs";

    private void OnEnable()
    {

        if (treeViewState == null)
            treeViewState = new TreeViewState();

        treeView = new SkinTreeView(treeViewState);
        treeView.LoadStateFromPrefs();  // Load state from EditorPrefs
        treeView.Reload();
        bRefreshDirectories = true;

        treeView.OnCheckedItemsChanged += HandleCheckedItemsChanged;

        // Register the callback when the editor window is enabled
        Undo.undoRedoPerformed += OnUndoRedo;

        RefreshMods();
    }
    void OnDisable()
    {
        if (treeView != null)
            treeView.SaveStateToPrefs();  // Save state when window is disabled

        treeView.OnCheckedItemsChanged -= HandleCheckedItemsChanged;

        // Unregister the callback when the editor window is disabled to avoid memory leaks
        Undo.undoRedoPerformed -= OnUndoRedo;
    }
    private void OnUndoRedo()
    {
        // This method is called after an undo or redo operation is performed.
        // Repaint the editor window to reflect the changes.
        Repaint();
    }

    void RefreshMods()
    {
        strUploadPassword = "";

        AssetDatabase.Refresh();

        allMods.Clear();

        string[] guids = AssetDatabase.FindAssets("t:PTK_ModInfo", new[] { strModSO_Path });
        foreach (var guid in guids)
        {
            PTK_ModInfo mod = AssetDatabase.LoadAssetAtPath<PTK_ModInfo>(AssetDatabase.GUIDToAssetPath(guid));

            if (mod != null)
                allMods.Add(mod);
        }
        allMods.RemoveAll(item => item == null);

        if (currentMod != null)
        {
            treeView.SetCheckedPaths(new HashSet<string>(currentMod.SelectedPaths));

            if (allMods.Contains(currentMod) == false)
            {
                if (allMods.Count > 0)
                {
                    currentMod = allMods[0];
                }
                else
                {
                    currentMod = null;
                }
                selectedIndex = 0;
            }
        }
        else if (allMods.Count > 0)
        {
            currentMod = allMods[0];
            selectedIndex = 0;
        }
        else
        {
            selectedIndex = 0;
        }
    }

    private void HandleCheckedItemsChanged(HashSet<string> checkedItems)
    {
        if (currentMod != null)
        {
            currentMod.SelectedPaths = checkedItems.ToList();
            EditorUtility.SetDirty(currentMod); // Mark the ScriptableObject as "dirty" so that changes are saved.
        }
    }

    private Vector2 scrollPosition;
    private Vector2 scrollPositionNames;

    bool bRefreshDirectories = false;

    private string GenerateUniqueModName(string baseName)
    {
        int count = 0;
        string potentialName = baseName + " " + UnityEngine.Random.Range(3445, 9999);
        while (AssetExists(potentialName))
        {
            count++;
            potentialName = baseName + " " + UnityEngine.Random.Range(3445, 9999) + "_" + count;
        }
        return potentialName;
    }

    private bool AssetExists(string name)
    {
        string assetPath = System.IO.Path.Combine(strModSO_Path, name + ".asset");
        return AssetDatabase.LoadAssetAtPath(assetPath, typeof(PTK_ModInfo)) != null;
    }

    void ModConfigGUI()
    {
        GUI.enabled = false;
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("TK2 Modding Version", PTK_ModInfo.GameModPluginVersion.ToString("v0.0"));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUI.enabled = true;





        GUI.enabled = false;
        EditorGUILayout.ObjectField("Selected Mod", currentMod, typeof(PTK_ModInfo), false);
        GUI.enabled = true;
        GUILayout.BeginHorizontal();
        // Dropdown for selecting a mod
        if (allMods.Count > 0)
        {
            string[] modNames = allMods.Select(mod => mod.ModName).ToArray();
            selectedIndex = EditorGUILayout.Popup("Select Mod", selectedIndex, modNames);

            if (selectedIndex >= 0)
            {
                currentMod = allMods[selectedIndex];
            }
        }else
        {
            EditorGUILayout.Popup("Select Mod", 0, new string[] { },GUILayout.Width(200));
        }


        if(iLastSelectedIndex != selectedIndex)
        {
            RefreshMods();
        }

        iLastSelectedIndex = selectedIndex;

        GUI.color = currentMod != null ? Color.red : Color.gray;
        // Delete mod button with confirmation
        if (GUILayout.Button("Delete Mod", GUILayout.Width(100)) && currentMod != null)
        {
            if (EditorUtility.DisplayDialog("Confirm Delete", "Are you sure you want to delete this mod?", "Yes", "No"))
            {
                allMods.Remove(currentMod);
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(currentMod));
                AssetDatabase.Refresh();
                currentMod = null;
                selectedIndex = -1;

                if (allMods.Count > 0)
                {
                    currentMod = allMods[0];
                    selectedIndex = 0;
                }
            }
        }
        GUI.color = Color.white;

        GUI.color = Color.yellow;
        GUILayout.Space(50);
        // Button to create a new mod
        if (GUILayout.Button("Create New Mod",GUILayout.Width(160)))
        {
            PTK_ModInfo newMod = CreateInstance<PTK_ModInfo>();
            string uniqueModName = GenerateUniqueModName("Mod ");
            newMod.ModName ="NEW " +  uniqueModName;
            AssetDatabase.CreateAsset(newMod,System.IO.Path.Combine( strModSO_Path , uniqueModName + ".asset"));
            AssetDatabase.Refresh();

            allMods.Add(newMod);
            currentMod = newMod;
            selectedIndex = allMods.Count - 1;


            Texture2D newTex1920 = new Texture2D(1920, 1080, TextureFormat.RGB24,false);
            Texture2D newTex960 = new Texture2D(960, 540,TextureFormat.RGB24,false);
            byte[] emptyTex1920 = newTex1920.EncodeToPNG();
            byte[] emptyTex960 = newTex960.EncodeToPNG();

            string strFileName = "";

            string strModTexturePreviewsPath = GetCurrentModEditorSO_LocationDirPath();
            strFileName = "Thumbnail.png";
            System.IO.File.WriteAllBytes(strModTexturePreviewsPath + strFileName, emptyTex960);

            strFileName = "Screen1.png";
            System.IO.File.WriteAllBytes(strModTexturePreviewsPath + strFileName, emptyTex1920);

            strFileName = "Screen2.png";
            System.IO.File.WriteAllBytes(strModTexturePreviewsPath + strFileName, emptyTex1920);

            strFileName = "Screen3.png";
            System.IO.File.WriteAllBytes(strModTexturePreviewsPath + strFileName, emptyTex1920);

            strFileName = "Screen4.png";
            System.IO.File.WriteAllBytes(strModTexturePreviewsPath + strFileName, emptyTex1920);


            AssetDatabase.Refresh();
        }



        GUI.color = Color.white;

        GUILayout.EndHorizontal();

        GUILayout.Space(20);

        // Only show these fields if a mod is selected
        if (currentMod != null)
        {

            // Mod name editing
            if(currentMod != null)
            {

                // configs id
                GUI.color = Color.yellow*2;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("Name HASH used to generate constant uqnique IDs for your items!");
                GUILayout.Label("Important to avoid conflicts with other mods." + " Do not change later to ensure item IDs constant");
              


                GUI.color = Color.white;
                if (currentMod.UniqueModNameHashToGenerateItemsKeys == "")
                    GUI.color = Color.red;
                else
                    GUI.color = Color.green;
                currentMod.UniqueModNameHashToGenerateItemsKeys = EditorGUILayout.TextField("Unique Mod Name Hash", currentMod.UniqueModNameHashToGenerateItemsKeys);

                GUILayout.EndVertical();

                // mod server dir name

                GUILayout.Space(5);
                GUI.color = Color.cyan*2;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("UPDATE KEY");
                GUILayout.Label("Server Mod Unique Update Key - used to find your mod and update it after initial upload.");


                GUI.color = Color.white;
                if (currentMod.strUniqueModServerUpdateKEY == "")
                    GUI.color = Color.red;
                else
                    GUI.color = Color.green;
                currentMod.strUniqueModServerUpdateKEY = EditorGUILayout.TextField("Mod Server Update Key", currentMod.strUniqueModServerUpdateKEY);
                
                GUILayout.EndVertical();


                // tips

                GUILayout.BeginVertical(GUI.skin.box);
                GUI.color = Color.white;
                GUILayout.Label("The directory name helps organize items into groups in the Main Menu.\nTo change items Menu group you can change directory name inside\nCharacters/Vehicles/Wheels and Outfit");
                GUI.color = Color.white;

                GUI.color = Color.white;
                GUILayout.EndVertical();
                GUILayout.Space(20);


                currentMod.ModName = EditorGUILayout.TextField("Mod Name", currentMod.ModName);
                currentMod.ModAuthor = EditorGUILayout.TextField("Mod Author", currentMod.ModAuthor);

                int iSelectedTag = Array.IndexOf(TagOptions, currentMod.strModTag);
                if (iSelectedTag == -1) iSelectedTag = 0;
                iSelectedTag = EditorGUILayout.Popup("Tag", iSelectedTag, TagOptions);
                currentMod.strModTag = TagOptions[iSelectedTag];


                int iSelectedVisibility = Array.IndexOf(VisibilityOptions, currentMod.strVisibility);
                if (iSelectedVisibility == -1) iSelectedVisibility = 0;
                GUILayout.BeginHorizontal();
                iSelectedVisibility = EditorGUILayout.Popup("Mod Visibility", iSelectedVisibility, VisibilityOptions,GUILayout.Width(250));
                currentMod.strVisibility = VisibilityOptions[iSelectedVisibility];

                switch (currentMod.strVisibility)
                {
                    case "Public":
                        GUILayout.Label("The mod is visible and accessible to all users");
                        break;
                    case "FriendsOnly":
                        GUILayout.Label("The mod is accessible only to the owner's friends list");
                        break;
                    case "Unlisted":
                        GUILayout.Label("The mod can only be accessed via a link");
                        break;
                    case "Private":
                        GUILayout.Label("The mod is visible and accessible only to the owner");
                        break;
                    default:
                        Debug.LogError("Visibility type unknown");
                        break;
                }

                GUILayout.EndHorizontal();

                

                GUILayout.Space(10);
                GUILayout.BeginHorizontal();


                string strModTexturePreviewsPath = GetCurrentModEditorSO_LocationDirPath();

                if(strLastPresentedModTexturePreviewsPath != strModTexturePreviewsPath)
                {
                    modThumbnailTexPreview = AssetDatabase.LoadAssetAtPath<Texture2D>(strModTexturePreviewsPath + "Thumbnail.png");
                    modScreen1 = AssetDatabase.LoadAssetAtPath<Texture2D>(strModTexturePreviewsPath + "Screen1.png");
                    modScreen2 = AssetDatabase.LoadAssetAtPath<Texture2D>(strModTexturePreviewsPath + "Screen2.png");
                    modScreen3 = AssetDatabase.LoadAssetAtPath<Texture2D>(strModTexturePreviewsPath + "Screen3.png");
                    modScreen4 = AssetDatabase.LoadAssetAtPath<Texture2D>(strModTexturePreviewsPath + "Screen4.png");

                }

                if(modThumbnailTexPreview != null)
                {
                    float fThumbnailSizeMB = new FileInfo(strModTexturePreviewsPath + "Thumbnail.png").Length / (1024.0f * 1024);
                    fCurrentMBThumbnailSize = fThumbnailSizeMB;
                }else
                {
                    fCurrentMBThumbnailSize = 0.0f;
                }

                strLastPresentedModTexturePreviewsPath = strModTexturePreviewsPath;
               
                GUILayout.BeginVertical();
                if (fCurrentMBThumbnailSize >= 1.0f || modThumbnailTexPreview == null)
                    GUI.color = Color.red;
                else
                    GUI.color = Color.green;

                GUILayout.Label("Under 1MB requirement. (" + fCurrentMBThumbnailSize.ToString("F1")+"MB)", GUI.skin.box);
                GUI.color = Color.white;
                GUI.enabled = false;
                modThumbnailTexPreview = (Texture2D)EditorGUILayout.ObjectField("Mod Thumbnail 16x9", modThumbnailTexPreview, typeof(Texture2D), false);
                GUI.enabled = true;
                GUILayout.BeginVertical();


                GUILayout.BeginVertical(GUI.skin.box);
                if (currentMod.bUploadAndReplaceScreenshootsOnServer)
                    GUI.color = Color.green;
                currentMod.bUploadAndReplaceScreenshootsOnServer = GUILayout.Toggle(currentMod.bUploadAndReplaceScreenshootsOnServer, "Override Current Steam Screenshoots");
                GUI.color = Color.white;
                EditorGUILayout.EndScrollView();

                
                GUILayout.Label("Screens ( under 1MB!)");
                GUI.enabled = false;
                modScreen1 = (Texture2D)EditorGUILayout.ObjectField(modScreen1, typeof(Texture2D), false);
                modScreen2 = (Texture2D)EditorGUILayout.ObjectField(modScreen2, typeof(Texture2D), false);
                modScreen3 = (Texture2D)EditorGUILayout.ObjectField(modScreen3, typeof(Texture2D), false);
                modScreen4 = (Texture2D)EditorGUILayout.ObjectField(modScreen4, typeof(Texture2D), false);
                GUI.enabled = true;
                GUILayout.EndVertical();
               
                GUILayout.EndVertical();


                // Optional: Display the assigned texture
                if (modThumbnailTexPreview != null)
                {
                    GUILayout.BeginHorizontal();
                    Rect rect = GUILayoutUtility.GetRect(-0, 0, 100, 100); // Adjust size as needed
                    rect.width = 192;
                    rect.height = 108;
                    GUI.DrawTexture(rect, modThumbnailTexPreview);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);
                }


                GUILayout.Space(10);
                GUILayout.BeginVertical(GUI.skin.box);
                if(currentMod.bUploadModDescriptionToServer)
                GUI.color = Color.green;
                currentMod.bUploadModDescriptionToServer = GUILayout.Toggle(currentMod.bUploadModDescriptionToServer, "Override Current Steam Mod Description");
                GUI.color = Color.white;
                GUILayout.Label("Description", GUILayout.Width(100));
                scrollPositionDescription = EditorGUILayout.BeginScrollView(scrollPositionDescription, GUILayout.Height(110));
                currentMod.strModDescription = EditorGUILayout.TextArea(currentMod.strModDescription, GUILayout.ExpandHeight(true),GUILayout.Width(300));
                EditorGUILayout.EndScrollView();
                GUILayout.EndVertical();
                GUILayout.Space(2);

                GUILayout.EndHorizontal();


                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();




                GUILayout.Space(10);
                currentMod.UserModVersion = EditorGUILayout.FloatField("Mod Version (User)", currentMod.UserModVersion);
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label("Changelog", GUILayout.Width(100));
                scrollPositionChangelog = EditorGUILayout.BeginScrollView(scrollPositionChangelog, GUILayout.Height(60));
                currentMod.strModChangelog = EditorGUILayout.TextArea(currentMod.strModChangelog, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
                GUILayout.EndHorizontal();
                GUILayout.Space(2);


            }
        }

    }

    string strUploadPassword = "";
    string strLastPresentedModTexturePreviewsPath = "";
    float fCurrentMBThumbnailSize = 0.0f;

    string GetCurrentModEditorSO_LocationDirPath()
    {
        string strModPath = AssetDatabase.GetAssetPath(currentMod);
        strModPath = strModPath.Replace(".asset", "");

        if(Directory.Exists(strModPath) == false)
            Directory.CreateDirectory(strModPath);

        return strModPath + "/";
    }

    Texture2D modThumbnailTexPreview = null; // local only, to not include in mod package (extra size, can be up to 3mb!)
    Texture2D modScreen1 = null; // local only, to not include in mod package (extra size, can be up to 3mb!)
    Texture2D modScreen2 = null; // local only, to not include in mod package (extra size, can be up to 3mb!)
    Texture2D modScreen3 = null; // local only, to not include in mod package (extra size, can be up to 3mb!)
    Texture2D modScreen4 = null; // local only, to not include in mod package (extra size, can be up to 3mb!)
    private Vector2 scrollPositionDescription;
    private Vector2 scrollPositionChangelog;
    string[] TagOptions = new string[] { "Characters", "Vehicles", "Wheels", "Tracks","Stickers" };
    string[] VisibilityOptions = new string[] { "Public", "FriendsOnly", "Unlisted", "Private" };
    private void OnGUI()
    {
        Undo.RecordObject(currentMod, "Mod Changed");
        EditorGUI.BeginChangeCheck();

        ModConfigGUI();

        string ignorePhrases = "Ctrl+D,Outfits,Blender,Color Variations,3D Models, GameplayPrefabBase,WeaponsAnimations";
         string noCheckboxPhrases = "Color Variations";



        scrollPosition = GUILayout.BeginScrollView(scrollPosition);




        GUILayout.Space(10);
        GUILayout.Label("Selected for Export: " + "(" + treeView.GetCheckedItems().Count + ")", EditorStyles.boldLabel);
        scrollPositionNames = GUILayout.BeginScrollView(scrollPositionNames,GUILayout.Height(100));
        GUILayout.BeginVertical("box");

        int iIndex = 1;
        foreach (var dirName in treeView.GetCheckedItems())
        {
            GUILayout.Label(iIndex.ToString() +": " +  dirName); iIndex++;
        }

        for (int i = 0; i < 10 - treeView.GetCheckedItems().Count; i++)
        {
            GUILayout.Label(iIndex.ToString() + ": "); iIndex++;
        }

        GUILayout.EndVertical();
        GUILayout.EndScrollView();



        // Draw the progress bar
    //    EditorGUI.ProgressBar(new Rect(3, GUILayoutUtility.GetLastRect().yMax + 5, position.width - 6, 20), fExportProgress, "Export Progress");

        GUILayout.Space(20);  // Add space after the progress bar

        GUI.enabled = currentMod != null;

        if(currentMod.UniqueModNameHashToGenerateItemsKeys == "")
        {
            GUI.color = Color.red;
            GUILayout.Label("Unique Mod Name is empty! Please assign unique Name before generating!");
            GUI.color = Color.white;
        }

        if (currentMod.strUniqueModServerUpdateKEY == "")
        {
            GUI.color = Color.red;
            GUILayout.Label("Update KEY is empty! Please assign unique key before generating!");
            GUI.color = Color.white;
        }

        if (modThumbnailTexPreview == null)
        {
            GUI.color = Color.red;
            GUILayout.Label("Thumbnail is required to generate mod!");
            GUI.color = Color.white;
        }

        if (strUploadPassword == "")
        {
            GUI.color = Color.red;
            GUILayout.Label("Upload Password is required to generate mod!");
            GUI.color = Color.white;
        }

        EditorGUILayout.LabelField("Last Build Date:", currentMod.LastBuildDateTime.ToString());


        GUILayout.Space(5);
        GUI.color = (Color.red + Color.yellow * 1.0f) * 1.5f;
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Ensures only you can upload this mod - others are blocked from re-uploading it after they download it.");

        if(strUploadPassword == "")
        {
            if(File.Exists(GetCurrentModEditorSO_LocationDirPath() + PTK_ModInfo.strUploadKey_FileName))
            {
                strUploadPassword = File.ReadAllText(GetCurrentModEditorSO_LocationDirPath() + PTK_ModInfo.strUploadKey_FileName);
            }
        }
        GUI.color = Color.white;
        if (strUploadPassword == "")
            GUI.color = Color.red;
        else
            GUI.color = Color.green;

        EditorGUI.BeginChangeCheck();
        strUploadPassword = EditorGUILayout.TextField("Upload Password: ", strUploadPassword);

        if(EditorGUI.EndChangeCheck() == true)
        {
            File.WriteAllText(GetCurrentModEditorSO_LocationDirPath() + PTK_ModInfo.strUploadKey_FileName, strUploadPassword);
        }

        GUILayout.EndVertical();
        GUI.color = Color.white;


        if (GUILayout.Button("Export & generate only new thumbnails (fast)"))
        {
            bRegenerateThumbnails = false;
            foreach (var dirName in treeView.GetCheckedItems())
            {
                OptimizeTextureSizesInDirectory(dirName);
            }

            ExportToAddressables();
        }

        GUILayout.Space(20);
        GUI.color = new Color(255/255.0f, 78/255.0f, 51/255.0f);

        if (GUILayout.Button("Export & Regenerate all thumbnails (slow)"))
        {
            bRegenerateThumbnails = true;

            foreach (var dirName in treeView.GetCheckedItems())
            {
                OptimizeTextureSizesInDirectory(dirName);
            }

            ExportToAddressables();
        }

        GUI.color = Color.white;

        GUI.enabled = true;

        GUILayout.Space(20);

        if (GUILayout.Button("Refresh Directories") || bRefreshDirectories)
        {
            bRefreshDirectories = false;

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

            treeView.SetIgnorePhrases(ignorePhrases.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            treeView.SetNoCheckboxPhrases(noCheckboxPhrases.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            treeView.Reload();
        }

        GUILayout.Space(30);
        // Draw tree view inside a flexible space so it takes up the rest of the scroll view
        GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
        Rect treeViewRect = GUILayoutUtility.GetRect(0, position.height, GUILayout.ExpandHeight(true));
        treeView.OnGUI(treeViewRect);
        GUILayout.EndVertical();

        GUILayout.EndScrollView();

        if(EditorGUI.EndChangeCheck() == true)
        {
            if(currentMod != null)
            {
                EditorUtility.SetDirty(currentMod);
            }
        }

    }




    bool bRegenerateThumbnails = false;

    public static void OptimizeTextureSizesInDirectory(string directoryPath)
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

    private static void SetTextureMaxSize(string texturePath, int maxSize)
    {
        TextureImporter textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (textureImporter && textureImporter.maxTextureSize != maxSize)
        {
            textureImporter.maxTextureSize = maxSize;
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
        }
    }

    ////
    /// Addressables
    ///
    string buildPathModDir;

    BoundingBoxCalculator boundingBoxCalculator;

    float fExportProgress = 0.0f;

    // will contain StandaloneWindows64 etc. (inside Mods/MiaMod/StandaloneWindows64
    string GetModFilesDirectoryForBuildPlatform(PTK_ModInfo modInfo)
    {
        string strDirectoryOfMod = System.IO.Path.Combine(Application.dataPath, "..", "Mods", modInfo.ModName, EditorUserBuildSettings.selectedStandaloneTarget.ToString());
        return strDirectoryOfMod;
    }
    AddressableAssetGroup CreateAndConfigureGroup(string strGroupName, string buildPath)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        string strUserCOnfigured_ModName = currentMod.ModName;

        AddressableAssetGroup  newGroup = settings.CreateGroup(strGroupName, false, false, true, new List<AddressableAssetGroupSchema>());

        // Attach necessary schemas
        var bundleSchema = newGroup.AddSchema<BundledAssetGroupSchema>();
        var contentUpdateSchema = newGroup.AddSchema<ContentUpdateGroupSchema>();

        // Set some default values for the schemas if required
        //  bundleSchema.BuildPath.SetVariableByName(settings, "LocalBuildPath");
        //    bundleSchema.LoadPath.SetVariableByName(settings, "LocalLoadPath");
        string buildPathVariableName = "CustomBuildPath";
        string loadPathVariableName = "CustomLoadPath";



        string strDirectoryOfModForPlatform = GetModFilesDirectoryForBuildPlatform(currentMod);
        if (System.IO.Directory.Exists(strDirectoryOfModForPlatform))
            System.IO.Directory.Delete(strDirectoryOfModForPlatform, true);

        Directory.CreateDirectory(strDirectoryOfModForPlatform);

        settings.profileSettings.CreateValue(buildPathVariableName, buildPath);
        settings.profileSettings.SetValue(settings.activeProfileId, buildPathVariableName, buildPath);

        // string loadPath = "file://./Mods/{LOCAL_FILE_NAME}/[BuildTarget]";
        //string loadPath = "file://./Mods/"+ strUserCOnfigured_ModName  + "/[BuildTarget]";
        //string loadPath = "../Mods/" + strUserCOnfigured_ModName + "/[BuildTarget]";
        //string loadPath = "file://{DATA_PATH}/Mods/" + strUserCOnfigured_ModName + "/[BuildTarget]";

        string strUserConfiguredModName = strUserCOnfigured_ModName;  // Replace with your actual mod name
                                                                      //   string loadPath = $"file://{{DATA_PATH}}\\Mods\\{strUserConfiguredModName}\\[BuildTarget]";
        string loadPath = "file://{DATA_PATH}";

        settings.profileSettings.CreateValue(loadPathVariableName, loadPath);
        settings.profileSettings.SetValue(settings.activeProfileId, loadPathVariableName, loadPath);

        settings.RemoteCatalogBuildPath.SetVariableByName(settings, buildPathVariableName);
        settings.RemoteCatalogLoadPath.SetVariableByName(settings, loadPathVariableName);


        bundleSchema.BuildPath.SetVariableByName(settings, buildPathVariableName);
        bundleSchema.LoadPath.SetVariableByName(settings, loadPathVariableName);

        bundleSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
        bundleSchema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.OnlyHash;
        // Add more default settings if needed

        return newGroup;
    }
    private void ExportToAddressables()
    {
        AssetDatabase.SaveAssets();

        string strUserCOnfigured_ModName = currentMod.ModName;
        string projectPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
        buildPathModDir = System.IO.Path.Combine(projectPath, "Mods", strUserCOnfigured_ModName);

        // Ensure the target directory exists
        Directory.CreateDirectory(buildPathModDir);

        if (currentMod.UniqueModNameHashToGenerateItemsKeys == "")
        {
            Debug.LogError("Unique Mod Name is empty. Please assign unique mod name first!");
            return;
        }

        if (currentMod.strUniqueModServerUpdateKEY == "")
        {
            Debug.LogError("Update KEY is empty! Please assign unique key before generating!");
            return;
        }

        if(modThumbnailTexPreview == null)
        {
            Debug.LogError("Thumbnail is required to generate mod!");
            return;
        }

        if (strUploadPassword == "")
        {
            Debug.LogError("Upload Password is required to generate mod!");
            return;
        }

        string strModTexturesPreviewsPath = GetCurrentModEditorSO_LocationDirPath();

        string strThumbnailPath1MBCheck = Path.Combine(strModTexturesPreviewsPath, "Thumbnail.png");
        if (File.Exists(strThumbnailPath1MBCheck) == false)
        {
            Debug.LogError("File thumbnail not found in path: " + strThumbnailPath1MBCheck);
            return;
        }

      string strDirectoryOfModWithFilesForTargetBuildPlatform =  GetModFilesDirectoryForBuildPlatform(currentMod);

        float fThumbnailSizeMB = new FileInfo(strThumbnailPath1MBCheck).Length/(1024 * 1024.0f);
        fCurrentMBThumbnailSize = fThumbnailSizeMB;
        if (fThumbnailSizeMB > 1.0f)
        {
            Debug.LogError("File thumnail size is higher than 1MB!");
            return;
        }

        if(currentMod.bUploadAndReplaceScreenshootsOnServer == true)
        {
            for (int i = 0; i < 4; i++)
            {
                string strScreenName = "Screen" + (i + 1).ToString() + ".png";
                string strScreenPath = Path.Combine(strModTexturesPreviewsPath, strScreenName);
                if (File.Exists(strScreenPath) == false)
                {
                    Debug.LogError("File screenshot not found in path: " + strScreenPath);
                    continue;
                }

                float fScreenMB = new FileInfo(strScreenPath).Length / (1024 * 1024.0f);
                if (fScreenMB > 1.0f)
                {
                    Debug.LogError("File thumnail size is higher than 1MB!" + fScreenMB.ToString());
                    return;
                }
            }
        }
       

        fExportProgress = 0.0f;
        if (currentMod == null)
        {
            return;
        }

        // simple guard to make sure uploading other mods won't be super easy
        File.WriteAllText(Path.Combine(buildPathModDir, PTK_ModInfo.strUploadKey_FileName), strUploadPassword);

        // save in unity project
        File.WriteAllText(Path.Combine(GetCurrentModEditorSO_LocationDirPath() + PTK_ModInfo.strUploadKey_FileName), strUploadPassword);

        ///////////// ENCRYPT UPLOAD KEY
        string original = PTK_ModInfo.strNameToDecrypt_UploadPassword;
        string encrypted = EncryptString(original, strUploadPassword);
        currentMod.strUploadHashedKey = encrypted;
        //////

        if (bRegenerateThumbnails == true)
            currentMod.thumbnailsForObjects.Clear();

        currentMod.modContentInfo = new CPTK_ModContentInfoFile();

        // Reference to the AddressableAssetSettings
        var settings = AddressableAssetSettingsDefaultObject.Settings;

        if (settings == null)
        {
            Debug.LogError("Failed to access AddressableAssetSettings.");
            return;
        }

        // Clear all groups
        var allGroups = new List<AddressableAssetGroup>(settings.groups);
        foreach (var group in allGroups)
        {
            if(group.name.ToLower().Contains("PTK_EnviroAssetsGroup".ToLower()) == true)
            {
                // we dont want to remove enviro asset group
                continue;
            }

            settings.RemoveGroup(group);
        }

        boundingBoxCalculator = GameObject.FindObjectOfType<BoundingBoxCalculator>(true);

        if(boundingBoxCalculator != null)
        {
            boundingBoxCalculator.RefreshOffsets();
            boundingBoxCalculator.gameObject.SetActive(false);
        }


        Dictionary<AddressableAssetEntry, AddressableAssetGroup> entries = new Dictionary<AddressableAssetEntry, AddressableAssetGroup>();

        currentMod.LastBuildDateTime = DateTime.Now.ToString();


        string buildPath = System.IO.Path.Combine(buildPathModDir, "[BuildTarget]");


        AddressableAssetGroup modInfoGroup = settings.FindGroup("ModInfoGroup");
        if (modInfoGroup == null)
            modInfoGroup = CreateAndConfigureGroup("ModInfoGroup", buildPath);

        var currentModGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(currentMod));

        var modInfoEntry = settings.CreateOrMoveEntry(currentModGuid, modInfoGroup);
        modInfoEntry.SetAddress("ModInfo", false);


        int totalDirectories = treeView.GetCheckedItems().Count;
        int totalAssets = 0;

        foreach (var dirName in treeView.GetCheckedItems())
        {
            totalAssets += GetAssetsInDirectoryNonRecursive(dirName).Length;
        }

        int totalSteps = totalDirectories + totalAssets;


        List<PTK_Workshop_CharAnimConfig> alreadyInitializedAnimConfigs = new List<PTK_Workshop_CharAnimConfig>();
        int currentStep = 0;
        foreach (var dirName in treeView.GetCheckedItems())
        {
            // Create a group for the directory
            // Create a group for the directory
            var groupName = Path.GetFileNameWithoutExtension(dirName);  // Assuming you want the directory name as the group name
            AddressableAssetGroup newGroup = settings.FindGroup(groupName);
            if (newGroup == null)
            {
                newGroup = CreateAndConfigureGroup(groupName, buildPath);
            }


            // Fetch all asset paths from the directory
            string[] assetPathsInDir = GetAssetsInDirectoryNonRecursive(dirName);

            string[] filteredPaths = assetPathsInDir.Where(path => path.Contains("PTK_Workshop_Char Anim Config")).ToArray();
            string strAnimConfigFilePath = filteredPaths.FirstOrDefault();

            PTK_Workshop_CharAnimConfig animConfig = null;
            if (strAnimConfigFilePath != null && strAnimConfigFilePath != "")
            {
                animConfig = AssetDatabase.LoadAssetAtPath<PTK_Workshop_CharAnimConfig>(strAnimConfigFilePath);

                if (alreadyInitializedAnimConfigs.Contains(animConfig) == false)
                {
                    PTK_Workshop_CharAnimConfigEditor.InitializeFromDirectory(animConfig);
                    alreadyInitializedAnimConfigs.Add(animConfig);
                }
            }

            foreach (string assetPath in assetPathsInDir)
            {
                var fullPath = assetPath;

                // If it's a directory, skip this iteration.
                if (AssetDatabase.IsValidFolder(fullPath))
                {
                  //  continue;
                }

                // Check if the asset is a .fbx and resides inside 'Color Variants' directory or its subdirectories
                if (fullPath.EndsWith(".fbx") && IsInsideColorVariantsDirectory(fullPath))
                {
                    continue; // Skip this asset and move to the next one
                }

                if (fullPath.Contains("GameplayPrefabBase") == true)
                    continue; // we don't need to include it (it will take extra space in mod)

                if (fullPath.Contains("ModelPreviewWithSuspension_DoNotIncludeInMod") == true)
                    continue; // we don't need to include it (it will take extra space in mod)

                var guid = AssetDatabase.AssetPathToGUID(fullPath);
                var entry = settings.CreateOrMoveEntry(guid, newGroup);

                string strAddressFileKey = ConstructName(fullPath, groupName);
                entry.SetAddress(strAddressFileKey, false);

                UpdateModFileInfo(fullPath, groupName, strAddressFileKey, animConfig);

                entries[entry] = newGroup;

                currentStep++;
                fExportProgress = (float)currentStep / totalSteps;
                EditorUtility.DisplayProgressBar("Exporting", "Exporting...", fExportProgress);
                Repaint();
            }
        }

        if(currentMod.modContentInfo.tracks.Count > 0)
        {
            if(currentMod.modContentInfo.tracks.Count != 1)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError("Single Mod should contain only one track!");
                return;
            }


            if (currentMod.modContentInfo.characters.Count != 0 ||
                currentMod.modContentInfo.stickers.Count != 0 ||
                currentMod.modContentInfo.vehicles.Count != 0 ||
                currentMod.modContentInfo.wheels.Count != 0)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError("Race Track Mod can't have other items like characters, wheels,vehicles or stickers!");
                return;
            }
        }
        EditorUtility.SetDirty(currentMod);
        AssetDatabase.SaveAssets();

        EditorUtility.ClearProgressBar();
        SimplifyAddresses(entries);

        AddressableAssetSettings.BuildPlayerContent();

        EditorUtility.SetDirty(settings); 
        AssetDatabase.SaveAssets();
        treeView.SaveStateToPrefs();  // Save state after changes

        // after mod generated - copy textures

        // Copy the texture to the new path
        try
        {
            string strFileName = "";

            strFileName = "Thumbnail.png";
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(buildPathModDir, strFileName), true); // copy into main mod directory (without platforms)
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(strDirectoryOfModWithFilesForTargetBuildPlatform, strFileName),true); // copy isnide Paltform type (StandaloneWIndows64) so offline loading will have thumbnail and screenshots to load

            strFileName = "Screen1.png";
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(buildPathModDir, strFileName), true); // copy into main mod directory (without platforms)
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(strDirectoryOfModWithFilesForTargetBuildPlatform, strFileName), true); // copy isnide Paltform type (StandaloneWIndows64) so offline loading will have thumbnail and screenshots to load

            strFileName = "Screen2.png";
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(buildPathModDir, strFileName), true); // copy into main mod directory (without platforms)
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(strDirectoryOfModWithFilesForTargetBuildPlatform, strFileName), true); // copy isnide Paltform type (StandaloneWIndows64) so offline loading will have thumbnail and screenshots to load

            strFileName = "Screen3.png";
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(buildPathModDir, strFileName), true); // copy into main mod directory (without platforms)
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(strDirectoryOfModWithFilesForTargetBuildPlatform, strFileName), true); // copy isnide Paltform type (StandaloneWIndows64) so offline loading will have thumbnail and screenshots to load

            strFileName = "Screen4.png";
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(buildPathModDir, strFileName), true); // copy into main mod directory (without platforms)
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(strDirectoryOfModWithFilesForTargetBuildPlatform, strFileName), true); // copy isnide Paltform type (StandaloneWIndows64) so offline loading will have thumbnail and screenshots to load
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to copy texture: {ex.Message}");
        }


        if (boundingBoxCalculator != null)
            boundingBoxCalculator.gameObject.SetActive(true);
    }


    private Regex Pattern_CharacterOnly = new Regex(@"Workshop_Content/Characters/(?<characterName>[^/]+)");
    private  Regex Pattern_Character = new Regex(@"Workshop_Content/Characters/(?<characterName>[^/]+)/Outfits/(?<outfit>[^/]+)/Color Variations/(?<materialVar>[^/]+)");
    private static readonly Regex Pattern_AnimConfig = new Regex(@"Workshop_Content/Characters/(?<characterName>[^/]+)");


    private Regex Pattern_Vehicles = new Regex(@"Workshop_Content/Vehicles/(?<Name>[^/]+)/Color Variations/(?<materialVar>[^/]+)");
    private Regex Pattern_Wheels = new Regex(@"Workshop_Content/Wheels/(?<Name>[^/]+)/Color Variations/(?<materialVar>[^/]+)");
    private Regex Pattern_Stickers = new Regex(@"Workshop_Content/Stickers/(?<Name>[^/]+)/Color Variations/(?<materialVar>[^/]+)");

    private Regex Pattern_Tracks = new Regex(@"Workshop_Content/Tracks/(?<Name>[^/]+)/");

    void UpdateModFileInfo(string strFullPath, string strGroupName, string strFileKey, PTK_Workshop_CharAnimConfig animConfig)
    {
        if(strFullPath.Contains("CharacterInfo"))
        {
            var match = Pattern_CharacterOnly.Match(strFullPath);
            string strCharacterName = match.Groups["characterName"].Value;

            string strCharacterInfoFilePath = strFullPath;// assetPathsInDir.Where(path => path.Contains("CharacterInfo")).ToArray().FirstOrDefault();
            PTK_CharacterInfoSO charInfo = AssetDatabase.LoadAssetAtPath<PTK_CharacterInfoSO>(strCharacterInfoFilePath);

            if (charInfo != null)
            {
                charInfo.CopyInfoTo(currentMod.modContentInfo, strCharacterName);
            }
        }
        else if(strFullPath.Contains("PTK_Workshop_Char Anim Config") == true)
        {
            var match = Pattern_AnimConfig.Match(strFullPath);

            if (match.Success)
            {
                string strCharacterName = match.Groups["characterName"].Value;
                currentMod.modContentInfo.GetCharacterFromDirectoryName(strCharacterName, true).strCharacterAnimConfigFileName = strFileKey;
            }
            else
            {
                Debug.LogError("Cant match PTK_Workshop_Char Anim Config file name!");
            }
        }
        else if (strFullPath.Contains("Workshop_Content/Characters"))
        {
            var match = Pattern_Character.Match(strFullPath);

            if(match.Success == true)
            {
                string strCharacterName = match.Groups["characterName"].Value;
                string strCharacterOutfit = match.Groups["outfit"].Value;
                string strMaterialVar = match.Groups["materialVar"].Value;

                string strPrefabAddressableKey = strFileKey;
                CPTK_ModContentInfoFile.CCharacter.CCharacterOutfit.CCharacterOutfit_Material charOutfitMaterial = currentMod.modContentInfo.GetCharacterFromDirectoryName(strCharacterName, true).GetOutfitFromName(strCharacterOutfit, true).GetMatVariantFromName(strMaterialVar, true);
                charOutfitMaterial.strPrefabFileName_AddressableKey = strPrefabAddressableKey;
                charOutfitMaterial.iGeneratedTargetUniqueConfigID = GetStringHashWithMD5(currentMod.UniqueModNameHashToGenerateItemsKeys + strPrefabAddressableKey);

                //string strTargetDirectory = Path.Combine(buildPathModDir,"Thumbnails", strCharacterName, strCharacterOutfit, strFileKey + ".png");

                string strDirSkin = Path.GetDirectoryName(strFullPath);
                string strTargetDirectory = strDirSkin + "/" + strPrefabAddressableKey + ".png";


                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(strFullPath);

                // lets see if prefab contains reference to anim list - if not, add it
                // we are adding this so they are auto referenced and we dont need to load them seperately
                PTK_Workshop_CharAnimConfig animListScriptableObj = animConfig;
                if (prefabAsset != null && animListScriptableObj != null)
                {
                    // Open the prefab for editing
                    string prefabAssetPath = AssetDatabase.GetAssetPath(prefabAsset);
                    GameObject loadedPrefab = PrefabUtility.LoadPrefabContents(prefabAssetPath);

                    // Add PTK_CharacterPrefabAnimations to the prefab if it doesn't already have it
                    PTK_ModCharAnimListRef myComponent = loadedPrefab.GetComponentInChildren<PTK_ModCharAnimListRef>();
                    if (myComponent == null)
                    {
                        GameObject charAnimRef = new GameObject("ModAutoGenerated - AnimRefs");
                        charAnimRef.transform.SetParent(loadedPrefab.transform);
                        myComponent = charAnimRef.AddComponent<PTK_ModCharAnimListRef>();
                    }

                    // Assign the ScriptableObject to the component
                    myComponent.charAnimationsListSO = animListScriptableObj;

                    // Save the modified prefab
                    PrefabUtility.SaveAsPrefabAsset(loadedPrefab, prefabAssetPath);
                    // Unload the prefab to free up memory
                    PrefabUtility.UnloadPrefabContents(loadedPrefab);
                }
                else
                {
                    Debug.LogError("Failed to load prefab or ScriptableObject.");
                }


                // THUMBNAILS
                // we dont want to generate again
                if (File.Exists(strTargetDirectory) == true && bRegenerateThumbnails == false)
                {
                }
                else
                {
                    if (boundingBoxCalculator == null)
                    {
                        Debug.LogError("Package need to be created with Workshop scene open!");
                        return;
                    }

                    if (Directory.Exists(Path.GetDirectoryName(strTargetDirectory)) == false)
                        Directory.CreateDirectory(Path.GetDirectoryName(strTargetDirectory));


                    GameObject instance = Instantiate(prefabAsset);

                    if(animConfig.CharacterA.Menu.Count == 0)
                    {
                        Debug.LogError("Character doesnt have menu animation to render icon: " + strFullPath);
                    }else
                    {
                        animConfig.CharacterA.Menu[0].SampleAnimation(instance, 0.0f);
                    }

                    var childWithBestPath = boundingBoxCalculator.GetChildWithBestPath(strFullPath);

                    instance.transform.position = childWithBestPath.vPosition;
                    instance.transform.rotation = childWithBestPath.qRotation;
                    instance.transform.localScale = childWithBestPath.vLoosyScale;

                    ThumbnailGenerate.TakeScreenshoot(2048, 2048, Camera.main, true, strTargetDirectory,true,512,512);
                    AssetDatabase.Refresh();
                    TextureImporter importer = AssetImporter.GetAtPath(strTargetDirectory) as TextureImporter;
                    if (importer != null && importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.mipmapEnabled = true;
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                    }

                    Sprite createdSprite = AssetDatabase.LoadAssetAtPath<Sprite>(strTargetDirectory);

                    PTK_ModInfo.CThumbForObject thumb = new PTK_ModInfo.CThumbForObject();
                    thumb.strObjDirName_AddressableKey = strPrefabAddressableKey;
                    thumb.spriteThumbnail = createdSprite;
                    currentMod.thumbnailsForObjects.Add(thumb);

                    GameObject.DestroyImmediate(instance.gameObject);
                }
            }
        }
        else if (strFullPath.Contains("Workshop_Content/Vehicles"))
        {
            var match = Pattern_Vehicles.Match(strFullPath);

            if (match.Success == true)
            {
                string strName = match.Groups["Name"].Value;
                string strMaterialVar = match.Groups["materialVar"].Value;

                string strPrefabAddressableKey = strFileKey;
                CPTK_ModContentInfoFile.CItemWithColorVariant.CItemColorVariant itemColorVariant = currentMod.modContentInfo.GetVehicleFromDirectoryName(strName, true).GetColorVariantFromName(strMaterialVar, true);
                itemColorVariant.strPrefabFileName_AddressableKey = strPrefabAddressableKey;
                itemColorVariant.iGeneratedTargetUniqueConfigID = GetStringHashWithMD5(currentMod.UniqueModNameHashToGenerateItemsKeys + strPrefabAddressableKey);

                EnsureVehicleStickersHaveMeshReadWriteEnabled(strFullPath);
                UpdateModFileItemFor_CItemWithColorVariant(itemColorVariant, strFullPath, strPrefabAddressableKey);
            }
        }
        else if (strFullPath.Contains("Workshop_Content/Wheels"))
        {
            var match = Pattern_Wheels.Match(strFullPath);

            if (match.Success == true)
            {
                string strName = match.Groups["Name"].Value;
                string strMaterialVar = match.Groups["materialVar"].Value;

                string strPrefabAddressableKey = strFileKey;
                CPTK_ModContentInfoFile.CItemWithColorVariant.CItemColorVariant itemColorVariant = currentMod.modContentInfo.GetWheelFromDirectoryName(strName, true).GetColorVariantFromName(strMaterialVar, true);
                itemColorVariant.strPrefabFileName_AddressableKey = strPrefabAddressableKey;
                itemColorVariant.iGeneratedTargetUniqueConfigID = GetStringHashWithMD5(currentMod.UniqueModNameHashToGenerateItemsKeys + strPrefabAddressableKey);

                UpdateModFileItemFor_CItemWithColorVariant(itemColorVariant, strFullPath, strPrefabAddressableKey);
            }
        }
        else if (strFullPath.Contains("Workshop_Content/Stickers"))
        {
            var match = Pattern_Stickers.Match(strFullPath);

            if (match.Success == true)
            {
                string strName = match.Groups["Name"].Value;
                string strMaterialVar = match.Groups["materialVar"].Value;

                string strPrefabAddressableKey = strFileKey;
                CPTK_ModContentInfoFile.CItemWithColorVariant.CItemColorVariant itemColorVariant = currentMod.modContentInfo.GetStickerFromDirectoryName(strName, true).GetColorVariantFromName(strMaterialVar, true);
                itemColorVariant.strPrefabFileName_AddressableKey = strPrefabAddressableKey;
                itemColorVariant.iGeneratedTargetUniqueConfigID = GetStringHashWithMD5(currentMod.UniqueModNameHashToGenerateItemsKeys + strPrefabAddressableKey);

                UpdateModFileItemFor_CItemWithColorVariant(itemColorVariant, strFullPath, strPrefabAddressableKey);
            }
        }
        else if (strFullPath.Contains("Workshop_Content/Tracks"))
        {
            var match = Pattern_Tracks.Match(strFullPath);

            if (match.Success == true)
            {
                string strName = match.Groups["Name"].Value;

                string strPrefabAddressableKey = strFileKey;
                CPTK_ModContentInfoFile.CTrackInfo trackInfo = currentMod.modContentInfo.GetTrackFromDirectoryName(strName, true);
                trackInfo.strTrackSceneName_AddressableKey = strPrefabAddressableKey;
                trackInfo.iGeneratedTargetUniqueConfigID = GetStringHashWithMD5(currentMod.UniqueModNameHashToGenerateItemsKeys + strPrefabAddressableKey);

                UpdateTrackModFileItem(trackInfo, strFullPath, strPrefabAddressableKey);
            }
        }
    }

    void EnsureVehicleStickersHaveMeshReadWriteEnabled(string strPathToVehiclePrefab)
    {
        // change stickers to readable so we can calcualte their position if they position is 0 in unity (offset is in mesh)
        PTK_ModVehicle modVehicle = AssetDatabase.LoadAssetAtPath<PTK_ModVehicle>(strPathToVehiclePrefab);
        if (modVehicle == null)
        {
            Debug.LogError("Cant load and edit stickers for mod vehicle: " + strPathToVehiclePrefab);
        }
        else
        {
            for (int i = 0; i < modVehicle.stickersManager.vehicleStickers.Length; i++)
            {
                var stickerMeshRenderers = modVehicle.stickersManager.vehicleStickers[i].GetComponentsInChildren<MeshRenderer>();
                for (int iMeshRenderer = 0; iMeshRenderer < stickerMeshRenderers.Length; iMeshRenderer++)
                {
                    var meshFilter = stickerMeshRenderers[iMeshRenderer].GetComponent<MeshFilter>();
                    string strPathToMeshModel = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
                    ModelImporter modelImporter = AssetImporter.GetAtPath(strPathToMeshModel) as ModelImporter;
                    if (modelImporter == null)
                    {
                        Debug.LogError("Asset is not a model or not found: " + strPathToMeshModel);
                    }
                    else
                    {
                        // Check if the mesh is readable
                        if (!modelImporter.isReadable)
                        {
                            Debug.Log("Mesh is not readable, changing to readable...");

                            // Change mesh to readable
                            modelImporter.isReadable = true;

                            // Apply changes
                            modelImporter.SaveAndReimport();

                        }
                    }

                }
            }
        }
    }

    void UpdateTrackModFileItem(CPTK_ModContentInfoFile.CTrackInfo trackInfo, string strFullPath, string strPrefabAddressableKey)
    {
        string strSceneDir = Path.GetDirectoryName(strFullPath);
        string strTargetFilePathForThumbPNG = strSceneDir + "/" + "TrackThumbnail" + ".png";


        if(File.Exists(strTargetFilePathForThumbPNG) == false)
        {
            Debug.LogError("Thumbnail not found for scene: " + strTargetFilePathForThumbPNG);
            return;
        }
        // THUMBNAILS
        // we dont want to generate again
        if (File.Exists(strTargetFilePathForThumbPNG) == true && bRegenerateThumbnails == false)
        {
        }
        else
        {
            TextureImporter importer = AssetImporter.GetAtPath(strTargetFilePathForThumbPNG) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.mipmapEnabled = false;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }

        if (currentMod.GetThumbnailForObject(strPrefabAddressableKey) == false)
        {
            Sprite createdSprite = AssetDatabase.LoadAssetAtPath<Sprite>(strTargetFilePathForThumbPNG);

            PTK_ModInfo.CThumbForObject thumb = new PTK_ModInfo.CThumbForObject();
            thumb.strObjDirName_AddressableKey = strPrefabAddressableKey;
            thumb.spriteThumbnail = createdSprite;
            currentMod.thumbnailsForObjects.Add(thumb);
        }
    }

    void UpdateModFileItemFor_CItemWithColorVariant( CPTK_ModContentInfoFile.CItemWithColorVariant.CItemColorVariant itemColorVariant,string strFullPath,string strPrefabAddressableKey)
    {
        string strDirSkin = Path.GetDirectoryName(strFullPath);
        string strTargetFilePathForThumbPNG = strDirSkin + "/" + strPrefabAddressableKey + ".png";



        // THUMBNAILS
        // we dont want to generate again
        if (File.Exists(strTargetFilePathForThumbPNG) == true && bRegenerateThumbnails == false)
        {
        }
        else
        {
            if (Directory.Exists(Path.GetDirectoryName(strTargetFilePathForThumbPNG)) == false)
                Directory.CreateDirectory(Path.GetDirectoryName(strTargetFilePathForThumbPNG));

            

            if(itemColorVariant.eItemType == CPTK_ModContentInfoFile.CItemWithColorVariant.EType.E_STICKER)
            {
                PTK_StickerTexures stickerTextures = AssetDatabase.LoadAssetAtPath<PTK_StickerTexures>(strFullPath);

                if(stickerTextures == null)
                {
                    Debug.LogError("There is no PTK_StickerTexures scriptable object inside path. Please make sure you duplicated CTRL+D directory for new one " + strFullPath);
                    return;
                }
                
               string[] strFilesInsideStickerDir = Directory.GetFiles(Path.GetDirectoryName(strFullPath));
                string strClampTexturePath = "";
                for(int i=0;i< strFilesInsideStickerDir.Length;i++)
                {
                    if (strFilesInsideStickerDir[i].ToLower().Contains(".meta") == true)
                        continue;

                    if (strFilesInsideStickerDir[i].ToLower().Contains("thumbnail") == true)
                        continue;

                    if (strFilesInsideStickerDir[i].ToLower().Contains(".png"))
                    {
                        strClampTexturePath = strFilesInsideStickerDir[i];
                        break;
                    }

                }

                if(strClampTexturePath == "")
                {
                    Debug.LogError("Clamp sticker texture not found in path: " + strFullPath);
                    return;
                }


                // to ensure correct textures are assigned to SO
                Texture2D textureClamp = AssetDatabase.LoadAssetAtPath<Texture2D>(strClampTexturePath);

                // Access the texture importer
                TextureImporter textureClampImporter = AssetImporter.GetAtPath(strClampTexturePath) as TextureImporter;

                if (textureClampImporter != null)
                {
                    bool bChanged = false;
                    // Set the max size
                    if(textureClampImporter.maxTextureSize != 1024)
                    {
                        textureClampImporter.maxTextureSize = 1024;
                        bChanged = true;
                    }

                    // Set the wrap mode
                    if(textureClampImporter.wrapMode != TextureWrapMode.Clamp)
                    {
                        textureClampImporter.wrapMode = TextureWrapMode.Clamp;
                        bChanged = true;
                    }

                    if(textureClampImporter.alphaIsTransparency == false)
                    {
                        bChanged = true;
                        textureClampImporter.alphaIsTransparency = true;
                    }

                    // Apply the changes to the textureClampImporter

                    if(bChanged == true)
                        AssetDatabase.ImportAsset(strClampTexturePath, ImportAssetOptions.ForceUpdate);
                }
                else
                {
                    Debug.LogError("Could not find texture importer at path: " + strClampTexturePath);
                }


                stickerTextures.textureClamp = textureClamp;
                EditorUtility.SetDirty(stickerTextures);

                strTargetFilePathForThumbPNG = strDirSkin + "/" + "StickerThumbnail_" + strPrefabAddressableKey + ".png";

                if(File.Exists(strTargetFilePathForThumbPNG) == false || bRegenerateThumbnails == true)
                {
                    File.Copy(strClampTexturePath, strTargetFilePathForThumbPNG, true);

                    AssetDatabase.Refresh();
                    TextureImporter textureClampImporterThumbnail = AssetImporter.GetAtPath(strTargetFilePathForThumbPNG) as TextureImporter;

                    if (textureClampImporterThumbnail != null)
                    {
                        // Set the max size
                        textureClampImporterThumbnail.maxTextureSize = 256;

                        textureClampImporter.alphaIsTransparency = true;
                        // Apply the changes to the textureClampImporter
                        AssetDatabase.ImportAsset(strTargetFilePathForThumbPNG, ImportAssetOptions.ForceUpdate);
                    }
                    else
                    {
                        Debug.LogError("Could not find texture importer at path: " + strClampTexturePath);
                    }
                }
                

                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
            }
            else
            {
                if (boundingBoxCalculator == null)
                {
                    Debug.LogError("Package need to be created with Workshop scene open!");
                    return;
                }

                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(strFullPath);
                GameObject instance = Instantiate(prefabAsset);

                var childWithBestPath = boundingBoxCalculator.GetChildWithBestPath(strFullPath);

                instance.transform.position = childWithBestPath.vPosition;
                instance.transform.rotation = childWithBestPath.qRotation;
                instance.transform.localScale = childWithBestPath.vLoosyScale;

                ThumbnailGenerate.TakeScreenshoot(2048, 2048, Camera.main, true, strTargetFilePathForThumbPNG, true, 512, 512);
                AssetDatabase.Refresh();

                GameObject.DestroyImmediate(instance.gameObject);
            }

            TextureImporter importer = AssetImporter.GetAtPath(strTargetFilePathForThumbPNG) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.mipmapEnabled = true;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }

            Sprite createdSprite = AssetDatabase.LoadAssetAtPath<Sprite>(strTargetFilePathForThumbPNG);

            PTK_ModInfo.CThumbForObject thumb = new PTK_ModInfo.CThumbForObject();
            thumb.strObjDirName_AddressableKey = strPrefabAddressableKey;
            thumb.spriteThumbnail = createdSprite;
            currentMod.thumbnailsForObjects.Add(thumb);

        }
    }
    public static int GetStringHashWithMD5(string input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        using (MD5 md5Hash = MD5.Create())
        {
            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Use a portion of the hash to create an int.
            // Here, we're using the first 4 bytes of the MD5 hash.
            // Note: This simplification increases the risk of collision.
            int hashCode = BitConverter.ToInt32(data, 0);

            return hashCode;
        }
    }

    public static string ConstructName(string assetPath,string strGroupName)
    {

        string[] forbiddenPhrases = { "Color Variations", "Assets","Workshop" ,"Outfits"};

        // Split the path into parts.
        string[] parts = assetPath.Split(new char[] { '/', '\\' }, System.StringSplitOptions.RemoveEmptyEntries);

        // Filter out parts that contain any forbidden phrases.
        List<string> partsList = parts.Where(part => !forbiddenPhrases.Any(phrase => part.Contains(phrase))).ToList();

        if(partsList[partsList.Count-1].Contains(".prefab"))
        {
            partsList.RemoveAt(partsList.Count - 1); // we dont want prefab name at the end
        }

        if (partsList[partsList.Count - 1].Contains(".asset"))
        {
            partsList.RemoveAt(partsList.Count - 1); // we dont want prefab name at the end
        }

        if (partsList[partsList.Count - 1].Contains(".unity"))
        {
            partsList.RemoveAt(partsList.Count - 1); // we dont want scene name at the end
        }

        return string.Join("_", partsList);//.Replace(" ", "");
    }

   

    private string[] GetAssetsInDirectoryNonRecursive(string directory)
    {
        // 1. Get all asset paths in the directory (this includes subdirectories)
        string[] allAssetPaths = AssetDatabase.FindAssets("", new[] { directory })
            .Select(AssetDatabase.GUIDToAssetPath)
            .ToArray();

        string strTargetDirPath = (Application.dataPath + directory.Substring("Assets".Length, directory.Length - "Assets".Length));
        strTargetDirPath = strTargetDirPath.Replace("\\", "/");
        // 2. Filter out the directories and assets in subdirectories
        List<string> filteredAssets = new List<string>();
        foreach (string assetPath in allAssetPaths)
        {
            FileInfo info = new FileInfo(Application.dataPath + assetPath.Substring("Assets".Length, assetPath.Length - "Assets".Length));

            string strDirFullName = info.Directory.FullName.Replace('\\', '/');
            bool bOnlyThisDirectory = false;
            if (info.Exists  )
            {
                if (bOnlyThisDirectory == true && strDirFullName != strTargetDirPath)
                    continue;

                if (assetPath.ToLower().Contains("ctrl+d"))
                    continue;

                if(assetPath.ToLower().Contains("tracks") == true)
                {
                    if (assetPath.ToLower().Contains(".unity") == true)
                    {
                        filteredAssets.Add(assetPath);
                    }

                    // we dont want add any other files from tracks directory other than scene
                    continue;
                }

                if (assetPath.Contains(".prefab") || assetPath.Contains("PTK_Workshop_Char") || assetPath.Contains("Info") || (assetPath.Contains("Blender") == false && assetPath.Contains(".fbx") == true)
                    || (assetPath.Contains("StickerTextures.asset") == true ))
                    filteredAssets.Add(assetPath);
            }
            
        }

        return filteredAssets.ToArray();
    }

    public static void SimplifyAddresses(Dictionary<AddressableAssetEntry, AddressableAssetGroup> entries)
    {
        foreach (var group in entries)
            group.Value.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, group.Key, false, true);
        AddressableAssetSettingsDefaultObject.Settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entries, true, false);
    }



    private bool IsInsideColorVariantsDirectory(string assetPath)
    {
        var directories = assetPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < directories.Length; i++)
        {
            if (directories[i] == "Color Variations")
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// ///// Tree View
    /// </summary>
    class SkinTreeView : TreeView
    {
        public event Action<HashSet<string>> OnCheckedItemsChanged;
        private HashSet<string> ignoreSet = new HashSet<string>();

        public void SetIgnorePhrases(string[] phrases)
        {
            ignoreSet.Clear();
            foreach (var phrase in phrases)
            {
                ignoreSet.Add(phrase.Trim());
            }
        }
        private HashSet<string> noCheckboxSet = new HashSet<string>();

        public void SetNoCheckboxPhrases(string[] phrases)
        {
            noCheckboxSet.Clear();
            foreach (var phrase in phrases)
            {
                noCheckboxSet.Add(phrase.Trim());
            }
        }

        private const string rootPath = "Assets/Workshop_Content";

        public SkinTreeView(TreeViewState state) : base(state)
        {
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            int id = 1;
            var items = CreateChildrenForDirectory(rootPath, ref id);
            SetupParentsAndChildrenFromDepths(root, items);
            return root;
        }
        private List<TreeViewItem> CreateChildrenForDirectory(string path, ref int id, int depth = 0)
        {
            var items = new List<TreeViewItem>();

            // Skip directories that match ignore phrases
            if (ShouldIgnore(path))
                return items;

            // Add current directory (skip for the root)
            if (depth > 0)
                items.Add(new MyTreeViewItem { id = id++, depth = depth, displayName = Path.GetFileName(path), pathSegment = Path.GetFileName(path), fullPath = path });

            var subDirs = Directory.GetDirectories(path);
            foreach (var dir in subDirs)
            {
                items.AddRange(CreateChildrenForDirectory(dir, ref id, depth + 1));
            }

            return items;
        }

        private bool ShouldIgnore(string path)
        {
            foreach (var ignore in ignoreSet)
            {
                if (path.Contains(ignore))
                    return true;
            }
            return false;
        }
        HashSet<string> checkedItems_ = new HashSet<string>();
        public HashSet<string> GetCheckedItems()
        {
            return checkedItems_;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            MyTreeViewItem myItem = args.item as MyTreeViewItem;
            if (!ShouldNotRenderCheckbox(myItem.displayName) && ShouldRenderCheckbox_IfParent(myItem))
            {
                EditorGUI.BeginChangeCheck();
                bool wasChecked = checkedItems_.Contains(myItem.fullPath);
                bool isChecked = EditorGUI.Toggle(new Rect(args.rowRect.x + 2, args.rowRect.y, 16, args.rowRect.height), wasChecked);
                if (EditorGUI.EndChangeCheck())
                {
                    if (isChecked && !wasChecked)
                    {
                        checkedItems_.Add(myItem.fullPath);
                    }
                    else if (!isChecked && wasChecked)
                    {
                        checkedItems_.Remove(myItem.fullPath);
                    }

                    OnCheckedItemsChanged?.Invoke(checkedItems_);
                }
            }

            base.RowGUI(args);


        }
        private bool ShouldRenderCheckbox_IfParent(MyTreeViewItem item)
        {
            if (item == null)
                return false;

           // if ((item as MyTreeViewItem).pathSegment == "Characters")
            //    return true;

            if (item.parent as MyTreeViewItem == null)
                return false;

          //  if ((item.parent as MyTreeViewItem).pathSegment == "Outfits")
        //        return true;

            if ((item.parent as MyTreeViewItem).pathSegment == "Characters")
                return true;

            if ((item.parent as MyTreeViewItem).pathSegment == "Vehicles")
                return true;

            if ((item.parent as MyTreeViewItem).pathSegment == "Tracks")
                return true;

            if ((item.parent as MyTreeViewItem).pathSegment == "Wheels")
                return true;

            if ((item.parent as MyTreeViewItem).pathSegment == "Stickers")
                return true;

            return false;
        }

        public HashSet<string> GetCheckedPaths()
        {
            return checkedItems_;
        }

        public void SetCheckedPaths(HashSet<string> paths)
        {
            checkedItems_.Clear();
            foreach (var path in paths)
            {
                checkedItems_.Add(path);
            }
        }

        public void RemovePath(string strPath)
        {
            checkedItems_.Remove(strPath);
        }

        public void SaveStateToPrefs()
        {
            string checkedPaths = string.Join(";", checkedItems_);
            EditorPrefs.SetString("SkinTreeView_Checked_Items", checkedPaths);
        }

        public void LoadStateFromPrefs()
        {
            if (EditorPrefs.HasKey("SkinTreeView_Checked_Items"))
            {
                var savedPaths = EditorPrefs.GetString("SkinTreeView_Checked_Items").Split(';');
                checkedItems_ = new HashSet<string>(savedPaths);
            }
        }

        private class MyTreeViewItem : TreeViewItem
        {
            public string pathSegment;
            public string fullPath; // New field
        }

        private bool ShouldNotRenderCheckbox(string path)
        {
            foreach (var noCheckbox in noCheckboxSet)
            {
                if (path.Contains(noCheckbox))
                    return true;
            }
            return false;
        }

    }

    //https://stackoverflow.com/questions/10168240/encrypting-decrypting-a-string-in-c-sharp

    private const string initVector = "tu89geji340t89u2";

    private const int keysize = 256;

    public static string EncryptString(string Text, string Key)
    {
        byte[] initVectorBytes = Encoding.UTF8.GetBytes(initVector);
        byte[] plainTextBytes = Encoding.UTF8.GetBytes(Text);
        PasswordDeriveBytes password = new PasswordDeriveBytes(Key, null);
        byte[] keyBytes = password.GetBytes(keysize / 8);
        RijndaelManaged symmetricKey = new RijndaelManaged();
        symmetricKey.Mode = CipherMode.CBC;
        ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes);
        MemoryStream memoryStream = new MemoryStream();
        CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
        cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
        cryptoStream.FlushFinalBlock();
        byte[] Encrypted = memoryStream.ToArray();
        memoryStream.Close();
        cryptoStream.Close();
        return Convert.ToBase64String(Encrypted);
    }

    public static string DecryptString(string EncryptedText, string Key)
    {
        byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
        byte[] DeEncryptedText = Convert.FromBase64String(EncryptedText);
        PasswordDeriveBytes password = new PasswordDeriveBytes(Key, null);
        byte[] keyBytes = password.GetBytes(keysize / 8);
        RijndaelManaged symmetricKey = new RijndaelManaged();
        symmetricKey.Mode = CipherMode.CBC;
        ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes);
        MemoryStream memoryStream = new MemoryStream(DeEncryptedText);
        CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
        byte[] plainTextBytes = new byte[DeEncryptedText.Length];
        int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
        memoryStream.Close();
        cryptoStream.Close();
        return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
    }

}



