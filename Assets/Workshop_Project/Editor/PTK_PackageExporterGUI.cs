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

public class PTK_PackageExporterGUI
{
    private List<PTK_ModInfo> allMods = new List<PTK_ModInfo>();

    private int iSelectedModIndex = -1;
    private int iLastSelectedIndex = -1;

    string[] TagOptions = new string[] { "Characters", "Vehicles", "Wheels", "Tracks", "Stickers" };
    string[] VisibilityOptions = new string[] { "Public", "FriendsOnly", "Unlisted", "Private" };
    string strModSO_Path = "Assets/Workshop_Project/LocalUserModsGenerationConfigs";

    public Texture2D menuTrackThumbnail = null; // local only, to not include in mod package (extra size, can be up to 3mb!)
    public Texture2D modThumbnailTexPreview = null; // local only, to not include in mod package (extra size, can be up to 3mb!)
    Texture2D modScreen1 = null; // local only, to not include in mod package (extra size, can be up to 3mb!)
    Texture2D modScreen2 = null; // local only, to not include in mod package (extra size, can be up to 3mb!)
    Texture2D modScreen3 = null; // local only, to not include in mod package (extra size, can be up to 3mb!)
    Texture2D modScreen4 = null; // local only, to not include in mod package (extra size, can be up to 3mb!)



    private Vector2 scrollPosition;
    private Vector2 scrollPositionNames;
    bool bRefreshDirectories = false;

    private Vector2 scrollPositionDescription;
    private Vector2 scrollPositionChangelog;


    public string strUploadPassword = "";
    public string strLastPresentedModTexturePreviewsPath = "";
    public float fCurrentMBThumbnailSize = 0.0f;

    internal void OnEnable(PTK_PackageExporter exporter)
    {
        bRefreshDirectories = true;
        strUploadPassword = "";

        RefreshModListInProject(exporter);
    }

    internal void EventOnGUI(PTK_PackageExporter exporter)
    {
        if (exporter.currentMod != null)
            Undo.RecordObject(exporter.currentMod, "Mod Changed");

        EditorGUI.BeginChangeCheck();

        ModConfigGUI(exporter);

        if (exporter.currentMod == null)
        {
            EditorGUI.EndChangeCheck();
            return;
        }


        RenderModGenerationGUI(exporter);
    }

    void RenderModGenerationGUI(PTK_PackageExporter exporter)
    {
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        GUILayout.Space(10);

        RenderContentSelectedForExportView(exporter);

        GUILayout.Space(20);  // Add space after the progress bar

        RenderModConfigurationCheckInfo(exporter);

        GUILayout.Space(5);

        RenderExportPasswordView(exporter);

        RenderExportButtons(exporter);

        GUI.enabled = true;
        GUILayout.Space(20);

        if (GUILayout.Button("Refresh Directories") || bRefreshDirectories)
        {
            bRefreshDirectories = false;
            exporter.RefreshTreeViewDirectories();
        }

        GUILayout.Space(30);

        RenderSelectedForExportTreeView(exporter);

        GUILayout.EndScrollView();

        if (EditorGUI.EndChangeCheck() == true)
        {
            if (exporter.currentMod != null)
            {
                EditorUtility.SetDirty(exporter.currentMod);
            }
        }
    }

    private static void RenderSelectedForExportTreeView(PTK_PackageExporter exporter)
    {

        // Draw tree view inside a flexible space so it takes up the rest of the scroll view
        GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
        Rect treeViewRect = GUILayoutUtility.GetRect(0, exporter.position.height, GUILayout.ExpandHeight(true));
        exporter.treeView.OnGUI(treeViewRect);
        GUILayout.EndVertical();
    }

    private static void RenderExportButtons(PTK_PackageExporter exporter)
    {
        if (GUILayout.Button("Export & generate only new thumbnails (fast)"))
        {
            exporter.ExportModPackage(false);

        }

        GUILayout.Space(20);
        GUI.color = new Color(255 / 255.0f, 78 / 255.0f, 51 / 255.0f);

        if (GUILayout.Button("Export & Regenerate all thumbnails (slow)"))
        {
            exporter.ExportModPackage(true);
        }

        GUI.color = Color.white;
    }

    private void RenderExportPasswordView(PTK_PackageExporter exporter)
    {
        GUI.color = (Color.red + Color.yellow * 1.0f) * 1.5f;
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Ensures only you can upload this mod - others are blocked from re-uploading it after they download it.");

        if (strUploadPassword == "")
        {
            if (File.Exists(GetCurrentModEditorSO_LocationDirPath(exporter) + PTK_ModInfo.strUploadKey_FileName))
            {
                strUploadPassword = File.ReadAllText(GetCurrentModEditorSO_LocationDirPath(exporter) + PTK_ModInfo.strUploadKey_FileName);
            }
        }

        GUI.color = Color.white;
        if (strUploadPassword == "")
            GUI.color = Color.red;
        else
            GUI.color = Color.green;

        EditorGUI.BeginChangeCheck();
        strUploadPassword = EditorGUILayout.TextField("Upload Password: ", strUploadPassword);

        if (EditorGUI.EndChangeCheck() == true)
        {
            File.WriteAllText(GetCurrentModEditorSO_LocationDirPath(exporter) + PTK_ModInfo.strUploadKey_FileName, strUploadPassword);
        }

        GUILayout.EndVertical();
        GUI.color = Color.white;
    }

    private void RenderModConfigurationCheckInfo(PTK_PackageExporter exporter)
    {
        GUI.enabled = exporter.currentMod != null;

        if (exporter.currentMod.UniqueModNameHashToGenerateItemsKeys == "")
        {
            GUI.color = Color.red;
            GUILayout.Label("Unique Mod Name is empty! Please assign unique Name before generating!");
            GUI.color = Color.white;
        }

        if (exporter.currentMod.strUniqueModServerUpdateKEY == "")
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

        EditorGUILayout.LabelField("Last Build Date:", exporter.currentMod.LastBuildDateTime.ToString());
    }

    private void RenderContentSelectedForExportView(PTK_PackageExporter exporter)
    {
        GUILayout.Label("Selected for Export: " + "(" + exporter.treeView.GetCheckedItems().Count + ")", EditorStyles.boldLabel);
        scrollPositionNames = GUILayout.BeginScrollView(scrollPositionNames, GUILayout.Height(100));
        GUILayout.BeginVertical("box");

        int iIndex = 1;
        foreach (var dirName in exporter.treeView.GetCheckedItems())
        {
            GUILayout.Label(iIndex.ToString() + ": " + dirName); iIndex++;
        }

        for (int i = 0; i < 10 - exporter.treeView.GetCheckedItems().Count; i++)
        {
            GUILayout.Label(iIndex.ToString() + ": "); iIndex++;
        }

        GUILayout.EndVertical();
        GUILayout.EndScrollView();
    }

    void ModConfigGUI(PTK_PackageExporter exporter)
    {
        RenderModVersionInfo();

        RenderSelectedAndManageModInfo(exporter);

        GUILayout.Space(20);


        // Only show these fields if a mod is selected
        if (exporter.currentMod != null)
        {
            // configs id
            RenderModConfigurations(exporter);

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            RenderModThumbnailsAndScreens(exporter);
            RenderModDescription(exporter);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            RenderModChangelogInfo(exporter);

            GUILayout.Space(2);

        }

    }

    private void RenderModChangelogInfo(PTK_PackageExporter exporter)
    {
        exporter.currentMod.UserModVersion = EditorGUILayout.FloatField("Mod Version (User)", exporter.currentMod.UserModVersion);
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label("Changelog", GUILayout.Width(100));
        scrollPositionChangelog = EditorGUILayout.BeginScrollView(scrollPositionChangelog, GUILayout.Height(60));
        exporter.currentMod.strModChangelog = EditorGUILayout.TextArea(exporter.currentMod.strModChangelog, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        GUILayout.EndHorizontal();
    }

    private void RenderModDescription(PTK_PackageExporter exporter)
    {
        GUILayout.Space(10);
        GUILayout.BeginVertical(GUI.skin.box);
        if (exporter.currentMod.bUploadModDescriptionToServer)
            GUI.color = Color.green;
        exporter.currentMod.bUploadModDescriptionToServer = GUILayout.Toggle(exporter.currentMod.bUploadModDescriptionToServer, "Override Current Steam Mod Description");
        GUI.color = Color.white;
        GUILayout.Label("Description", GUILayout.Width(100));
        scrollPositionDescription = EditorGUILayout.BeginScrollView(scrollPositionDescription, GUILayout.Height(110));
        exporter.currentMod.strModDescription = EditorGUILayout.TextArea(exporter.currentMod.strModDescription, GUILayout.ExpandHeight(true), GUILayout.Width(300));
        EditorGUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUILayout.Space(2);

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
    }

    private void RenderModThumbnailsAndScreens(PTK_PackageExporter exporter)
    {
        string strModTexturePreviewsPath = GetCurrentModEditorSO_LocationDirPath(exporter);

        string strSceneDir = Path.GetDirectoryName(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        string strSceneThumbnailPath = strSceneDir + "\\TrackThumbnail.png";

        if (strLastPresentedModTexturePreviewsPath != strModTexturePreviewsPath)
        {
            modThumbnailTexPreview = AssetDatabase.LoadAssetAtPath<Texture2D>(strModTexturePreviewsPath + "Thumbnail" + PTK_ModInfo.strThumbScreenImageExt);
            modScreen1 = AssetDatabase.LoadAssetAtPath<Texture2D>(strModTexturePreviewsPath + "Screen1"+ PTK_ModInfo.strThumbScreenImageExt);
            modScreen2 = AssetDatabase.LoadAssetAtPath<Texture2D>(strModTexturePreviewsPath + "Screen2"+ PTK_ModInfo.strThumbScreenImageExt);
            modScreen3 = AssetDatabase.LoadAssetAtPath<Texture2D>(strModTexturePreviewsPath + "Screen3"+ PTK_ModInfo.strThumbScreenImageExt);
            modScreen4 = AssetDatabase.LoadAssetAtPath<Texture2D>(strModTexturePreviewsPath + "Screen4"+ PTK_ModInfo.strThumbScreenImageExt);

        }

        if(menuTrackThumbnail == null || strLastPresentedModTexturePreviewsPath != strModTexturePreviewsPath)
            menuTrackThumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(strSceneThumbnailPath);

        if (modThumbnailTexPreview != null)
        {
            float fThumbnailSizeMB = new FileInfo(strModTexturePreviewsPath + "Thumbnail" + PTK_ModInfo.strThumbScreenImageExt).Length / (1024.0f * 1024);
            fCurrentMBThumbnailSize = fThumbnailSizeMB;
        }
        else
        {
            fCurrentMBThumbnailSize = 0.0f;
        }

        strLastPresentedModTexturePreviewsPath = strModTexturePreviewsPath;

        GUILayout.BeginVertical();

        if(exporter.currentMod.strModTag.ToLower().Contains("track") == true)
        {
            GUILayout.Label("Increase version to reset leaderboard");
            exporter.currentMod.iTrackLeaderboardVersion = EditorGUILayout.IntField("Track Leaderboard Version", exporter.currentMod.iTrackLeaderboardVersion);

            GUI.color = Color.cyan;
            if (GUILayout.Button("edit RACE TRACK Hi-Res Thumbnail (show in windows Explorer)"))
            {
                EditorUtility.RevealInFinder(strSceneThumbnailPath);
            }
            GUILayout.Label(strSceneThumbnailPath + "   ");
            GUI.color = Color.white;

            GUI.color = Color.white;
            GUI.enabled = false;
            menuTrackThumbnail = (Texture2D)EditorGUILayout.ObjectField("MENU 16x9 Hi-Resolution", menuTrackThumbnail, typeof(Texture2D), false);
            GUI.enabled = true;

            GUILayout.Space(10);
        }
      


        GUI.color = Color.yellow;
        if (GUILayout.Button("edit STEAM/Mods.IO Thumbnail & Screens (show in windows Explorer)"))
        {
            EditorUtility.RevealInFinder(strModTexturePreviewsPath + "Thumbnail" + PTK_ModInfo.strThumbScreenImageExt);
        }
        GUILayout.Label(strModTexturePreviewsPath +"Thumbnail" + PTK_ModInfo.strThumbScreenImageExt);
        GUI.color = Color.white;

        if (fCurrentMBThumbnailSize >= 1.0f || modThumbnailTexPreview == null)
            GUI.color = Color.red;
        else
            GUI.color = Color.green;

        GUILayout.Label("Under 1MB requirement. (" + fCurrentMBThumbnailSize.ToString("F1") + "MB)", GUI.skin.box);

        GUI.color = Color.white;
        GUI.enabled = false;
        modThumbnailTexPreview = (Texture2D)EditorGUILayout.ObjectField("Steam Thumbnail 16x9", modThumbnailTexPreview, typeof(Texture2D), false);
        GUI.enabled = true;


        GUILayout.BeginVertical();


        GUILayout.BeginVertical(GUI.skin.box);
        if (exporter.currentMod.bUploadAndReplaceScreenshootsOnServer)
            GUI.color = Color.green;
        exporter.currentMod.bUploadAndReplaceScreenshootsOnServer = GUILayout.Toggle(exporter.currentMod.bUploadAndReplaceScreenshootsOnServer, "Override Current Steam Screenshoots");
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

    }

    private void RenderModConfigurations(PTK_PackageExporter exporter)
    {
        GUI.color = Color.yellow * 2;
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Name HASH used to generate constant uqnique IDs for your items!");
        GUILayout.Label("Important to avoid conflicts with other mods." + " Do not change later to ensure item IDs constant");

        GUI.color = Color.white;
        if (exporter.currentMod.UniqueModNameHashToGenerateItemsKeys == "")
            GUI.color = Color.red;
        else
            GUI.color = Color.green;
        exporter.currentMod.UniqueModNameHashToGenerateItemsKeys = EditorGUILayout.TextField("Unique Mod Name Hash", exporter.currentMod.UniqueModNameHashToGenerateItemsKeys);

        GUILayout.EndVertical();

        // mod server dir name

        GUILayout.Space(5);
        GUI.color = Color.cyan * 2;
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("UPDATE KEY");
        GUILayout.Label("Server Mod Unique Update Key - used to find your mod and update it after initial upload.");

        GUI.color = Color.white;
        if (exporter.currentMod.strUniqueModServerUpdateKEY == "")
            GUI.color = Color.red;
        else
            GUI.color = Color.green;
        exporter.currentMod.strUniqueModServerUpdateKEY = EditorGUILayout.TextField("Mod Server Update Key", exporter.currentMod.strUniqueModServerUpdateKEY);

        GUILayout.EndVertical();

        // tips

        GUILayout.BeginVertical(GUI.skin.box);
        GUI.color = Color.white;
        GUILayout.Label("The directory name helps organize items into groups in the Main Menu.\nTo change items Menu group you can change directory name inside\nCharacters/Vehicles/Wheels and Outfit");
        GUI.color = Color.white;

        GUI.color = Color.white;
        GUILayout.EndVertical();
        GUILayout.Space(20);

        exporter.currentMod.ModName = EditorGUILayout.TextField("Mod Name", exporter.currentMod.ModName);
        exporter.currentMod.ModAuthor = EditorGUILayout.TextField("Mod Author", exporter.currentMod.ModAuthor);

        int iSelectedTag = Array.IndexOf(TagOptions, exporter.currentMod.strModTag);
        if (iSelectedTag == -1) iSelectedTag = 0;
        iSelectedTag = EditorGUILayout.Popup("Tag", iSelectedTag, TagOptions);
        exporter.currentMod.strModTag = TagOptions[iSelectedTag];


        int iSelectedVisibility = Array.IndexOf(VisibilityOptions, exporter.currentMod.strVisibility);
        if (iSelectedVisibility == -1) iSelectedVisibility = 0;
        GUILayout.BeginHorizontal();
        iSelectedVisibility = EditorGUILayout.Popup("Mod Visibility", iSelectedVisibility, VisibilityOptions, GUILayout.Width(250));
        exporter.currentMod.strVisibility = VisibilityOptions[iSelectedVisibility];

        switch (exporter.currentMod.strVisibility)
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
    }

    private void RenderSelectedAndManageModInfo(PTK_PackageExporter exporter)
    {
        GUI.enabled = false;
        EditorGUILayout.ObjectField("Selected Mod", exporter.currentMod, typeof(PTK_ModInfo), false);
        GUI.enabled = true;

        GUILayout.BeginHorizontal();
        // Dropdown for selecting a mod
        if (allMods.Count > 0)
        {
            string[] modNames = allMods.Select(mod => mod.ModName).ToArray();
            iSelectedModIndex = EditorGUILayout.Popup("Select Mod", iSelectedModIndex, modNames);

            if (iSelectedModIndex >= 0)
            {
                exporter.currentMod = allMods[iSelectedModIndex];
            }
        }
        else
        {
            EditorGUILayout.Popup("Select Mod", 0, new string[] { }, GUILayout.Width(200));
        }


        if (iLastSelectedIndex != iSelectedModIndex)
        {
            RefreshModListInProject(exporter);
        }

        iLastSelectedIndex = iSelectedModIndex;

        GUI.color = exporter.currentMod != null ? Color.red : Color.gray;
        // Delete mod button with confirmation
        if (GUILayout.Button("Delete Mod", GUILayout.Width(100)) && exporter.currentMod != null)
        {
            if (EditorUtility.DisplayDialog("Confirm Delete", "Are you sure you want to delete this mod?", "Yes", "No"))
            {
                allMods.Remove(exporter.currentMod);
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(exporter.currentMod));
                AssetDatabase.Refresh();
                exporter.currentMod = null;
                iSelectedModIndex = -1;

                if (allMods.Count > 0)
                {
                    exporter.currentMod = allMods[0];
                    iSelectedModIndex = 0;
                }
            }
        }
        GUI.color = Color.white;


        GUI.color = Color.yellow;
        GUILayout.Space(50);
        // Button to create a new mod
        if (GUILayout.Button("Create New Mod", GUILayout.Width(160)))
        {
            PTK_ModInfo newMod = ScriptableObject.CreateInstance<PTK_ModInfo>();
            string uniqueModName = GenerateUniqueModName("Mod ");
            newMod.ModName = "NEW " + uniqueModName;
            AssetDatabase.CreateAsset(newMod, System.IO.Path.Combine(strModSO_Path, uniqueModName + ".asset"));
            AssetDatabase.Refresh();

            allMods.Add(newMod);
            exporter.currentMod = newMod;
            iSelectedModIndex = allMods.Count - 1;


            // create empty textures that user can edit with their own images
            Texture2D newTex1920 = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            Texture2D newTex960 = new Texture2D(960, 540, TextureFormat.RGB24, false);
            byte[] emptyTex1920 = newTex1920.EncodeToPNG();
            byte[] emptyTex960 = newTex960.EncodeToPNG();

            string strFileName = "";

            string strModTexturePreviewsPath = GetCurrentModEditorSO_LocationDirPath(exporter);
            strFileName = "Thumbnail" + PTK_ModInfo.strThumbScreenImageExt;
            System.IO.File.WriteAllBytes(strModTexturePreviewsPath + strFileName, emptyTex960);

            strFileName = "Screen1"+ PTK_ModInfo.strThumbScreenImageExt;
            System.IO.File.WriteAllBytes(strModTexturePreviewsPath + strFileName, emptyTex1920);

            strFileName = "Screen2"+ PTK_ModInfo.strThumbScreenImageExt;
            System.IO.File.WriteAllBytes(strModTexturePreviewsPath + strFileName, emptyTex1920);

            strFileName = "Screen3"+ PTK_ModInfo.strThumbScreenImageExt;
            System.IO.File.WriteAllBytes(strModTexturePreviewsPath + strFileName, emptyTex1920);

            strFileName = "Screen4"+ PTK_ModInfo.strThumbScreenImageExt;
            System.IO.File.WriteAllBytes(strModTexturePreviewsPath + strFileName, emptyTex1920);


            AssetDatabase.Refresh();
        }

        GUI.color = Color.white;
        GUILayout.EndHorizontal();
    }

    private static void RenderModVersionInfo()
    {
        GUI.enabled = false;
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("TK2 Modding Version", PTK_ModInfo.GameModPluginVersion.ToString("v0.0"));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUI.enabled = true;
    }

    //////////////////


    void RefreshModListInProject(PTK_PackageExporter exporter)
    {
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

        if (exporter.currentMod != null)
        {
            exporter.treeView.SetCheckedPaths(new HashSet<string>(exporter.currentMod.SelectedPaths));

            if (allMods.Contains(exporter.currentMod) == false)
            {
                if (allMods.Count > 0)
                {
                    exporter.currentMod = allMods[0];
                }
                else
                {
                    exporter.currentMod = null;
                }
                iSelectedModIndex = 0;
            }
        }
        else if (allMods.Count > 0)
        {
            exporter.currentMod = allMods[0];
            iSelectedModIndex = 0;
        }
        else
        {
            iSelectedModIndex = 0;
        }
    }

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


    public string GetCurrentModEditorSO_LocationDirPath(PTK_PackageExporter exporter)
    {
        string strModPath = AssetDatabase.GetAssetPath(exporter.currentMod);
        strModPath = strModPath.Replace(".asset", "");

        if (Directory.Exists(strModPath) == false)
            Directory.CreateDirectory(strModPath);

        return strModPath + "/";
    }



    private bool AssetExists(string name)
    {
        string assetPath = System.IO.Path.Combine(strModSO_Path, name + ".asset");
        return AssetDatabase.LoadAssetAtPath(assetPath, typeof(PTK_ModInfo)) != null;
    }

    //////////////////
}
