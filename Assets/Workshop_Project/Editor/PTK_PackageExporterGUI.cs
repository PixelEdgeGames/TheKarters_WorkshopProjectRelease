using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class PTK_PackageExporterGUI
{
    private List<PTK_ModInfo> allMods = new List<PTK_ModInfo>();


    string[] TagOptions = new string[] { "Characters", "Vehicles", "Wheels", "Tracks", "Stickers","Duo Characters" };
    string[] VisibilityOptions = new string[] { "Public", "FriendsOnly", "Unlisted", "Private" };
    string strModSO_Path = "Assets/Workshop_Project/LocalUserModsGenerationConfigs";

    public Texture2D menuTrackThumbnail = null; // local only, to not include in mod package (extra size, can be up to 3mb!)
    public Texture2D modThumbnailTexPreview = null; // local only, to not include in mod package (extra size, can be up to 3mb!)
    Texture2D modScreen1 = null; // local only, to not include in mod package (extra size, can be up to 3mb!)
    Texture2D modScreen2 = null; // local only, to not include in mod package (extra size, can be up to 3mb!)
    Texture2D modScreen3 = null; // local only, to not include in mod package (extra size, can be up to 3mb!)
    Texture2D modScreen4 = null; // local only, to not include in mod package (extra size, can be up to 3mb!)



    private Vector2 scrollPosition;
    private Vector2 scrollPositionSettings;
    private Vector2 scrollPositionNames;
    private Vector2 scrollPositionThumbnails;
    bool bRefreshDirectories = false;

    private Vector2 scrollPositionDescription;
    private Vector2 scrollPositionChangelog;


    public string strUploadPassword = "";
    public string strLastPresentedModTexturePreviewsPath = "";
    public float fCurrentMBThumbnailSize = 0.0f;

    private string[] tabOptions = new string[] { "Settings","Thumbnails","VO & Music", "Export" };
    private int selectedTabIndex = 0;
    private int iSelectedImageTabIndex = 0;
    private string[] tabImageOptions = new string[] { "Steam Workshop & Mods.IO", "Track Selection Game Menu" };

    internal void OnEnable(PTK_PackageExporter exporter)
    {
        bRefreshDirectories = true;
        strUploadPassword = "";
        bShowPassword = false;

        RefreshModListInProject(exporter);
    }

    internal void EventOnGUI(PTK_PackageExporter exporter)
    {
        if (exporter.currentMod != null)
            Undo.RecordObject(exporter.currentMod, "Mod Changed");




        EditorGUI.BeginChangeCheck();

        RenderModVersionInfo();

        RenderSelectedAndManageModInfo(exporter);

        GUILayout.Space(20);


        if (exporter.currentMod == null)
        {
            EditorGUI.EndChangeCheck();
            return;
        }

        EditorGUI.BeginChangeCheck();
        selectedTabIndex = GUILayout.SelectionGrid(selectedTabIndex, tabOptions, tabOptions.Length);

        if (EditorGUI.EndChangeCheck())
            bShowPassword = false;

        RefreshTrackModCheck(exporter);

        RefreshTrackThumbnails(exporter);

        switch (selectedTabIndex)
        {
            case 0:
                ModConfigGUI(exporter);
                break;
            case 1:

                RenderModThumbnailsAndScreens(exporter);
                break;
            case 2:
                RenderModMusicInfo(exporter);
                break;
            case 3:

                RenderModGenerationGUI(exporter);
                break;
        }



    }

    private void RefreshTrackThumbnails(PTK_PackageExporter exporter)
    {
        string strSceneThumbnailPath = strSelectedTrackToExportDir + "\\TrackThumbnail.png";
        string strModTexturePreviewsPath = GetCurrentModEditorSO_LocationDirPath(exporter);

        if (strLastPresentedModTexturePreviewsPath != strModTexturePreviewsPath)
        {
            modThumbnailTexPreview = AssetDatabase.LoadAssetAtPath<Texture2D>(strModTexturePreviewsPath + "Thumbnail" + PTK_ModInfo.strThumbScreenImageExt);
            modScreen1 = AssetDatabase.LoadAssetAtPath<Texture2D>(strModTexturePreviewsPath + "Screen1" + PTK_ModInfo.strThumbScreenImageExt);
            modScreen2 = AssetDatabase.LoadAssetAtPath<Texture2D>(strModTexturePreviewsPath + "Screen2" + PTK_ModInfo.strThumbScreenImageExt);
            modScreen3 = AssetDatabase.LoadAssetAtPath<Texture2D>(strModTexturePreviewsPath + "Screen3" + PTK_ModInfo.strThumbScreenImageExt);
            modScreen4 = AssetDatabase.LoadAssetAtPath<Texture2D>(strModTexturePreviewsPath + "Screen4" + PTK_ModInfo.strThumbScreenImageExt);

        }
        strLastPresentedModTexturePreviewsPath = strModTexturePreviewsPath;
    }

    void RenderLeaderboardVersion(PTK_PackageExporter exporter)
    {
        GUI.enabled = bIsTrackMod;
        GUILayout.BeginHorizontal();
        GUILayout.Label("Track Leaderboard Version - Increase version to reset leaderboard");
        exporter.currentMod.iTrackLeaderboardVersion = EditorGUILayout.IntField("", exporter.currentMod.iTrackLeaderboardVersion);
        GUILayout.EndHorizontal();
        GUI.color = Color.white;
        GUI.enabled = true;
        GUILayout.Space(15);
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

        RenderModChangelogInfo(exporter);

        RenderLeaderboardVersion(exporter);

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

    enum ETrackModError
    {
        E_NONE,
        E_MULTIPLE_ITEMS_SELECTED_FOR_TRACK_TAG,
        E_TRACK_TAG_BUT_NO_TRACK_SELECTED
    }

    static ETrackModError eCurrentTrackModError = ETrackModError.E_NONE;

    void RefreshTrackModCheck(PTK_PackageExporter exporter)
    {
        bIsTrackMod = exporter.currentMod.strModTag.ToLower().Contains("track") == true;


        if (bIsTrackMod == true)
        {
            if (exporter.currentMod.SelectedPaths.Count > 1)
            {
                eCurrentTrackModError = ETrackModError.E_MULTIPLE_ITEMS_SELECTED_FOR_TRACK_TAG;

                bIsTrackMod = false;
            }
            else
            {
                if (exporter.currentMod.SelectedPaths.Count == 0 || exporter.currentMod.SelectedPaths[0].ToLower().Contains("tracks") == false)
                {
                    eCurrentTrackModError = ETrackModError.E_TRACK_TAG_BUT_NO_TRACK_SELECTED;

                    bIsTrackMod = false;
                }
                else
                {
                    strSelectedTrackToExportDir = exporter.currentMod.SelectedPaths[0];
                    eCurrentTrackModError = ETrackModError.E_NONE;
                }
            }
        }else
        {
            eCurrentTrackModError = ETrackModError.E_NONE;
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

    static void DisplayTrackErrors()
    {
        if (eCurrentTrackModError == ETrackModError.E_TRACK_TAG_BUT_NO_TRACK_SELECTED)
        {
            GUI.color = Color.red;
            GUILayout.Label("You choosen Track tag but no Track selected for export!!");
            GUI.color = Color.white;
        }
        else if (eCurrentTrackModError == ETrackModError.E_MULTIPLE_ITEMS_SELECTED_FOR_TRACK_TAG)
        {
            GUI.color = Color.red;
            GUILayout.Label("Only One item should be selected for Track Mod package!");
            GUI.color = Color.white;
        }
    }
    private static void RenderExportButtons(PTK_PackageExporter exporter)
    {
        DisplayTrackErrors();


        if (eCurrentTrackModError != ETrackModError.E_NONE)
            GUI.enabled = false;

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
        GUI.enabled = true;
    }

    public static string strPlayerPrefsGameDirKey = "TargetGameDir";
    public static string strPlayerPrefsGameDirCopyKey = "TargetGameDirCopyInt";

    bool bShowPassword = false;
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
        GUILayout.BeginHorizontal();

        if (bShowPassword == false)
            strUploadPassword = EditorGUILayout.PasswordField("Upload Password: ", strUploadPassword);
        else
            strUploadPassword = EditorGUILayout.TextField("Upload Password: ", strUploadPassword);

        GUI.color = Color.white;
        bShowPassword = EditorGUILayout.Toggle("Show Password", bShowPassword, GUILayout.Width(200));
        GUILayout.EndHorizontal();

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


        GUILayout.Space(10);
        GUI.color = Color.green*0.3f + Color.white*0.7f;
        GUILayout.Label("RaceTrack Hot Reload Supported - Game/Track Restart not required");
        GUILayout.BeginHorizontal();
        string strGameModsDirPath = PlayerPrefs.GetString(strPlayerPrefsGameDirKey);
        bool bCopyToGameDir = PlayerPrefs.GetInt(strPlayerPrefsGameDirCopyKey) == 1;
        if (PlayerPrefs.HasKey(strPlayerPrefsGameDirCopyKey) == false)
            bCopyToGameDir = true;

        GUI.color = Color.cyan;
        bCopyToGameDir = EditorGUILayout.Toggle("Copy Export to Game Dir", bCopyToGameDir, GUILayout.Width(180));
        GUI.color = Color.white;
        PlayerPrefs.SetInt(strPlayerPrefsGameDirCopyKey, bCopyToGameDir ? 1 : 0);

        GUI.enabled = bCopyToGameDir;

        bool bCorrectModsDir = strGameModsDirPath != "" && strGameModsDirPath.ToLower().Contains("mods") == true;

        if (bCopyToGameDir && bCorrectModsDir == false)
            GUI.color = Color.red;

        if (GUILayout.Button("Select Game 'Mods' Dir", GUILayout.Width(200)))
        {
            string strSelectedDir = EditorUtility.OpenFolderPanel("Select Game 'Mods' directory", "", "");
            PlayerPrefs.SetString(strPlayerPrefsGameDirKey, strSelectedDir);
        }

        if(bCorrectModsDir == true)
            GUI.color = Color.green;

        GUILayout.Label(strGameModsDirPath);

        GUI.color = Color.white;
        GUI.enabled = true;

        GUILayout.EndHorizontal();

        GUILayout.Space(5);
    }

    void ModConfigGUI(PTK_PackageExporter exporter)
    {


        // Only show these fields if a mod is selected
        if (exporter.currentMod != null)
        {
            scrollPositionSettings = GUILayout.BeginScrollView(scrollPositionSettings);
            // configs id
            RenderModConfigurations(exporter);

           // RenderModDescription(exporter);

            GUILayout.Space(10);

            RenderLeaderboardVersion(exporter);

            GUILayout.Space(2);

            GUILayout.EndScrollView();

        }

    }

    private void RenderModMusicInfo(PTK_PackageExporter exporter)
    {
        bool bIsModTrackType = exporter.currentMod.strModTag == "Tracks";

        GUILayout.Space(10);
        GUI.color = Color.yellow;
        GUILayout.Label("Select Tag in settings :  Character Tag for customVO | Tracks tag for Custom Music");
        GUI.color = Color.white;

        if (bIsModTrackType == true)
            RenderTrackCustomMusic(exporter);

        bool bIsCharactersMod = exporter.currentMod.strModTag == "Characters";

        if(bIsCharactersMod == true)
            RenderCustomVoiceOvers(exporter);

    }

    public (string,string) GetTrackCustomMusicSoundBankPath(PTK_PackageExporter exporter)
    {
        string strMusicSoundBank = "";

       string strSelectedTrackToExport =  exporter.currentMod.SelectedPaths[0];

        if (strSelectedTrackToExportDir != "" && strSelectedTrackToExport.Contains("Tracks"))
        {
            DirectoryInfo dirInfoMusic = new DirectoryInfo(strSelectedTrackToExportDir);
            var trackDirFiles = dirInfoMusic.GetFiles();
            for (int i = 0; i < trackDirFiles.Length; i++)
            {
                if (trackDirFiles[i].Name.Contains(".bnk"))
                {
                    strMusicSoundBank = trackDirFiles[i].Name;
                    break;
                }
            }
        }
        if (strMusicSoundBank == "")
            return ("", "");


        string strMusicSoundbankPath = strSelectedTrackToExportDir + "\\" + strMusicSoundBank;

        return (strMusicSoundbankPath, strMusicSoundBank);
    }

    public (string[], string[],string[]) GetCharactersSoundBankPaths(PTK_PackageExporter exporter)
    {
        List<string> voPaths = new List<string>();
        List<string> voSoundBankNames = new List<string>();
        List<string> characterPaths = new List<string>();


        for (int iPathIndex = 0; iPathIndex < exporter.currentMod.SelectedPaths.Count; iPathIndex++)
        {

            if (exporter.currentMod.SelectedPaths[iPathIndex].Contains("Characters"))
            {
                GUILayout.Space(5);

                string strCharVOSoundBank = "";
                string strCharacterPath = exporter.currentMod.SelectedPaths[iPathIndex];
                //exporter.currentMod
                if (strCharacterPath != "")
                {
                    DirectoryInfo dirInfoVO = new DirectoryInfo(strCharacterPath);
                    var trackDirFiles = dirInfoVO.GetFiles();
                    bool bContainBnkFile = false;

                    for (int i = 0; i < trackDirFiles.Length; i++)
                    {
                        if (trackDirFiles[i].Name.Contains(".meta"))
                            continue;


                        if (trackDirFiles[i].Name.Contains(".bnk"))
                        {
                            strCharVOSoundBank = trackDirFiles[i].Name;

                            string strVOSoundbankPath = strCharacterPath + "\\" + strCharVOSoundBank;

                            characterPaths.Add(strCharacterPath);
                            voPaths.Add(strVOSoundbankPath);
                            voSoundBankNames.Add(strCharVOSoundBank);

                            bContainBnkFile = true;
                        }
                    }

                    if(bContainBnkFile == false)
                    {
                        characterPaths.Add(strCharacterPath);
                        voPaths.Add("");
                        voSoundBankNames.Add("");
                    }
                }
            }
        }

        return (voPaths.ToArray(), voSoundBankNames.ToArray(), characterPaths.ToArray());
    }

    void RenderCustomVOBox()
    {
        GUILayout.Space(15);
        GUI.color = Color.cyan;
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.FlexibleSpace();
        GUILayout.Label("Custom Voice Overs - Not Required");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUI.color = Color.white;
    }
    void RenderCustomMusicTracksBox()
    {
        GUILayout.Space(15);
        GUI.color = Color.yellow;
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.FlexibleSpace();
        GUILayout.Label("Track Custom Music - Not Required");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUI.color = Color.white;
    }
    private void RenderTrackCustomMusic(PTK_PackageExporter exporter)
    {
        GUI.enabled = false; RenderCustomVOBox(); GUI.enabled = true;// to show it is supported

        RenderCustomMusicTracksBox();

        if (exporter.currentMod.SelectedPaths.Count == 0)
        {
            GUILayout.Label("Please select scene in Export tab first");
            return;
        }

        bool bTargetModSceneTrackIsOpen = true;

        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        // Get the path of the active scene
        string scenePathDir = activeScene.path != "" ? Path.GetDirectoryName(activeScene.path) : "";


        (string strMusicSoundbankPath, string strSoundBankName) = GetTrackCustomMusicSoundBankPath(exporter);

        if (scenePathDir == "" || scenePathDir.Replace('\\', '/') != exporter.currentMod.SelectedPaths[0].Replace('\\', '/'))
        {
            GUILayout.Label("Soundbank Exist: " +( (strMusicSoundbankPath != "") ? "TRUE" : "FALSE"));

            GUILayout.BeginHorizontal();
            GUILayout.Label("To ensure soundbank setup is correct, please open correct Mod Track scene");
            bTargetModSceneTrackIsOpen = false;

            GUI.color = Color.green;
            if (GUILayout.Button("Open Mod Target Track Scene"))
            {
                DirectoryInfo dirInfo = new DirectoryInfo(exporter.currentMod.SelectedPaths[0]);
                var filesInDir = dirInfo.GetFiles();
                for (int iFile = 0; iFile < filesInDir.Length;iFile++)
                {
                    if(filesInDir[iFile].FullName.Contains(".unity"))
                    {
                        EditorSceneManager.OpenScene(filesInDir[iFile].FullName, OpenSceneMode.Single);
                        break;
                    }
                }
            }

            GUILayout.EndHorizontal();

            GUI.enabled = false;
            GUI.color = Color.white;
            return; // to not confuse with presenting soundbank from other scene
        }

        bool bIsModTrackType = exporter.currentMod.strModTag == "Tracks";

        if (bIsModTrackType == false)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Custom Music is only supported for Tracks tag");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUI.enabled = false;
        }
        else
        {

        }


        string strTrackThumbnailPath = strSelectedTrackToExportDir + "\\" + "TrackThumbnail.png";
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUI.color = Color.yellow; 
        GUILayout.Label("Custom Music", GUILayout.Width(100));
        GUI.color = Color.white;
        var soundBank = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(strMusicSoundbankPath);
        EditorGUILayout.ObjectField(" SoundBank    :", soundBank, typeof(UnityEngine.Object), false);
        if (GUILayout.Button("Show In Explorer"))
        {
            EditorUtility.RevealInFinder(strTrackThumbnailPath);
        }
        GUILayout.EndHorizontal();
        GUILayout.Label(strMusicSoundbankPath);
        GUILayout.EndVertical();

        GUI.enabled = bTargetModSceneTrackIsOpen;

        if (bIsModTrackType == true)
        {
            var vModTrack = GameObject.FindObjectOfType<PTK_ModTrack>();

            if (vModTrack == null || vModTrack.strMusicSoundBank != strSoundBankName)
            {
                GUI.color = Color.red;
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Ensure to set Music Soundbank name to PTK_ModTrack in scene!");
                GUILayout.FlexibleSpace();


                GUI.color = Color.white;
                if (vModTrack != null)
                {
                    GUI.color = Color.green;
                    if (GUILayout.Button("Auto FIX: Set Music Soundbank name to PTK_ModTrack object scene") == true)
                    {
                        vModTrack.strMusicSoundBank = strSoundBankName;
                        EditorUtility.SetDirty(vModTrack);
                        if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        {
                            AssetDatabase.SaveAssets();
                        }
                        else
                        {
                        }
                    }
                }
                GUI.color = Color.white;
                GUILayout.EndHorizontal();

            }
            else
            {
                if(bTargetModSceneTrackIsOpen == true)
                {
                    GUI.color = Color.green + Color.white * 0.5f;
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Music Soundbank name is correctly set in PTK_ModTrack scene object");
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }
        }

        GUI.color = Color.white;
        GUILayout.Space(5);
        GUI.enabled = true;
    }

    private void RenderCustomVoiceOvers(PTK_PackageExporter exporter)
    {

        RenderCustomVOBox();

        GUILayout.Space(5);

        bool bIsCharactersMod = exporter.currentMod.strModTag == "Characters";

        if (bIsCharactersMod == false)
        {
            GUI.color = Color.yellow + Color.red*2.5f;
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Custom Voice Overs are only supported for Characters tag");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUI.color = Color.white;
            GUI.enabled = false;
        }
        else
        {
            if (exporter.currentMod.SelectedPaths.Count == 0)
            {
                GUILayout.Label("Please select character in Export tab first");
                GUI.enabled = false; RenderCustomMusicTracksBox(); GUI.enabled = true;// to show it is supported
                return;
            }

        }

        (string[] soundBankVOPaths, string[] soundBankVONames,string[] characterPaths) = GetCharactersSoundBankPaths(exporter);
        for (int iPathIndex=0;iPathIndex< soundBankVOPaths.Length; iPathIndex++)
        {
            GUILayout.Space(5);

            string strCharVOSoundBank = soundBankVONames[iPathIndex];
            string strVOSoundbankPath = soundBankVOPaths[iPathIndex];
            string strCharacterPath = characterPaths[iPathIndex];

            string strCharacterInfoPath = strCharacterPath + "\\" + "CharacterInfo.asset";
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUI.color = Color.cyan;
            GUILayout.Label(new FileInfo(strCharacterPath).Name);
            GUI.color = Color.white;
            var soundBank = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(strVOSoundbankPath);
            EditorGUILayout.ObjectField(" SoundBank    :", soundBank, typeof(UnityEngine.Object), false);
            if (GUILayout.Button("Show In Explorer"))
            {
                EditorUtility.RevealInFinder(strCharacterInfoPath);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(strCharacterPath);
            GUILayout.EndVertical();

            if (soundBank == null)
                continue;

            if (bIsCharactersMod == true)
            {
                var charInfoObject = AssetDatabase.LoadAssetAtPath<PTK_CharacterInfoSO>(strCharacterPath + "\\" + "CharacterInfo.asset");

                if (charInfoObject == null || charInfoObject.charInfo.strCustomVOSoundBankName != strCharVOSoundBank)
                {
                    GUI.color = Color.red;
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Ensure to set VoiceOver Soundbank name to CharacterInfo in project!");
                    GUILayout.FlexibleSpace();


                    GUI.color = Color.white;
                    if (charInfoObject != null)
                    {
                        if (GUILayout.Button("Set VoiceOver Soundbank name to CharacterInfo object") == true)
                        {
                            charInfoObject.charInfo.strCustomVOSoundBankName = strCharVOSoundBank;

                            EditorUtility.SetDirty(charInfoObject);
                            AssetDatabase.SaveAssets();
                        }
                    }
                    GUILayout.EndHorizontal();

                }
                else
                {
                    if (strCharVOSoundBank != "")
                    {
                        GUI.color = Color.green + Color.white * 0.5f;
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (charInfoObject != null)
                            GUILayout.Label("VO Soundbank name is correctly set in CharacterInfo object");
                        else
                            GUILayout.Label("CharacterInfo object not found in directory");
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }
                }
            }
        }
       

        GUI.color = Color.white;
        GUILayout.Space(5);


        GUI.enabled = false; RenderCustomMusicTracksBox(); GUI.enabled = true;// to show it is supported

    }
    private void RenderModChangelogInfo(PTK_PackageExporter exporter)
    {
        exporter.currentMod.UserModVersion = EditorGUILayout.FloatField("Mod Version (User)", exporter.currentMod.UserModVersion);
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label("Changelog", GUILayout.Width(100));
        scrollPositionChangelog = EditorGUILayout.BeginScrollView(scrollPositionChangelog, GUILayout.Height(100));
        exporter.currentMod.strModChangelog = EditorGUILayout.TextArea(exporter.currentMod.strModChangelog, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
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
        exporter.currentMod.strModDescription = EditorGUILayout.TextArea(exporter.currentMod.strModDescription, GUILayout.Height(300), GUILayout.Width(300));
        GUILayout.EndVertical();
        GUILayout.Space(2);

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
    }

    bool bIsTrackMod = false;
    string strSelectedTrackToExportDir = "";
    private void RenderModThumbnailsAndScreens(PTK_PackageExporter exporter)
    {
        GUILayout.Space(10);
        scrollPositionThumbnails = GUILayout.BeginScrollView(scrollPositionThumbnails);

        string strModTexturePreviewsPath = GetCurrentModEditorSO_LocationDirPath(exporter);

        string strSceneThumbnailPath = strSelectedTrackToExportDir + "\\TrackThumbnail.png";

        if (modThumbnailTexPreview != null)
        {
            float fThumbnailSizeMB = new FileInfo(strModTexturePreviewsPath + "Thumbnail" + PTK_ModInfo.strThumbScreenImageExt).Length / (1024.0f * 1024);
            fCurrentMBThumbnailSize = fThumbnailSizeMB;
        }
        else
        {
            fCurrentMBThumbnailSize = 0.0f;
        }


        GUILayout.BeginVertical();

        GUILayout.Space(20);

        GUILayout.BeginHorizontal();
        GUI.color = Color.yellow;
        GUILayout.FlexibleSpace();
        GUILayout.Label("Steam / Mods.io Thumbnails & Screenshots", GUI.skin.box);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUI.color = Color.white;

        GUILayout.Space(10);


        GUI.color = Color.yellow;
        if (GUILayout.Button("edit STEAM/Mods.IO Thumbnail & Screens (show in windows Explorer)"))
        {
            EditorUtility.RevealInFinder(strModTexturePreviewsPath + "Thumbnail" + PTK_ModInfo.strThumbScreenImageExt);
        }
        GUILayout.Label(strModTexturePreviewsPath + "Thumbnail" + PTK_ModInfo.strThumbScreenImageExt);
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
        EditorGUILayout.EndVertical();


        GUILayout.Label("Screens ( under 1MB!)");
        GUI.enabled = false;
        modScreen1 = (Texture2D)EditorGUILayout.ObjectField(modScreen1, typeof(Texture2D), false);
        modScreen2 = (Texture2D)EditorGUILayout.ObjectField(modScreen2, typeof(Texture2D), false);
        modScreen3 = (Texture2D)EditorGUILayout.ObjectField(modScreen3, typeof(Texture2D), false);
        modScreen4 = (Texture2D)EditorGUILayout.ObjectField(modScreen4, typeof(Texture2D), false);
        GUI.enabled = true;
        GUILayout.EndVertical();


        // track thumbnails


        GUILayout.Space(20);

        GUILayout.BeginHorizontal();
        GUI.color = Color.cyan;
        GUILayout.FlexibleSpace();
        GUILayout.Label("Race Track Game Menu Thumbnail", GUI.skin.box);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUI.color = Color.white;

        GUILayout.Space(20);
        if (bIsTrackMod == false)
        {
            GUI.color = Color.gray;
            GUILayout.Label("DISABLED - This is not a Track Mod (either the 'Track' tag is not selected or the track is not marked for export).");
            GUI.enabled = false;

        }

        if (menuTrackThumbnail == null || strLastPresentedModTexturePreviewsPath != strModTexturePreviewsPath)
            menuTrackThumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(strSceneThumbnailPath);


        GUI.color = Color.cyan;
        GUILayout.Space(10);
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

        GUI.enabled = true;


        GUILayout.EndVertical();


        GUILayout.EndScrollView();

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

     

        DisplayTrackErrors();

       
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
            List<string> strModNames = new List<string>();
            for(int i=0;i< allMods.Count;i++)
            {
                // esnure no empty
                if (allMods[i].ModName == "")
                    allMods[i].ModName = i.ToString();

                // to ensure there are no duplicate names
                if (strModNames.Contains(allMods[i].ModName) == true)
                    allMods[i].ModName += i.ToString();

                strModNames.Add(allMods[i].ModName);
            }


             List<PTK_ModInfo> sortedMods = allMods;

             if(exporter.iModsSortMode == 1)
                sortedMods = allMods.OrderBy(mod => mod.ModName).ToList();
             else if (exporter.iModsSortMode == 2)
                    sortedMods = allMods.OrderByDescending(mod => mod.LastBuildDateTime).ToList();

                int iSelectedModIndex = 0;
            for (int i=0;i< sortedMods.Count;i++)
            {
                if(sortedMods[i] == exporter.currentMod)
                {
                    iSelectedModIndex = i;
                    break;
                }
            }
            string[] modNames = sortedMods.Select(mod => mod.ModName).ToArray();

            EditorGUI.BeginChangeCheck();
            iSelectedModIndex = EditorGUILayout.Popup("Select Mod", iSelectedModIndex, modNames);

            if(EditorGUI.EndChangeCheck())
                bShowPassword = false;

            exporter.iModsSortMode = EditorGUILayout.Popup("", exporter.iModsSortMode, new string[] { "Sort: None", "Sort: Mod Name", "Sort: Build Date" }, GUILayout.Width(150));

            if (iSelectedModIndex >= 0)
            {
                exporter.currentMod = sortedMods[iSelectedModIndex];
            }
        }
        else
        {
            EditorGUILayout.Popup("Select Mod", 0, new string[] { }, GUILayout.Width(200));
        }


        if (exporter.lastSelectedMod != exporter.currentMod)
        {
            RefreshModListInProject(exporter);
        }

        exporter.lastSelectedMod = exporter.currentMod;

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

                if (allMods.Count > 0)
                {
                    exporter.currentMod = allMods[0];
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
            }
        }
        else if (allMods.Count > 0)
        {
            exporter.currentMod = allMods[0];
        }
        else
        {
            exporter.currentMod = null;
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
