using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public class PTK_AddressableAssetsHandler
{
    PTK_ModItemConfigurator modItemConfigurator = new PTK_ModItemConfigurator();

    string buildPathModDir;
    float fExportProgress = 0.0f;

    public void ExportToAddressables(PTK_PackageExporter exporter)
    {
        PrepareExportToAddressables(exporter);

        bool bExportSettingsValid = ValidateIfExportSettingsAreCorrect(exporter);

        if (bExportSettingsValid == false)
            return;


        SaveUploadPasswordKey(exporter);

        ClearExistingAddressablesGroups();


        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var addressableGroupsEntries = GenerateAddressablesGroupsAndContentItems(exporter);

        bool bAreSelectedItemsForExportAreValidToExportAsSingleModPackage = AreSelectedItemsForExportAreValidToExportAsSingleModPackage(exporter);
        

        EditorUtility.SetDirty(exporter.currentMod);
        AssetDatabase.SaveAssets();

        EditorUtility.ClearProgressBar();
        SimplifyAddresses(addressableGroupsEntries);

        // run addressables build
        AddressableAssetSettings.BuildPlayerContent();

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        exporter.treeView.SaveStateToPrefs();  // Save state after changes

        // after scene save/reload we may miss reference
        if(exporter.boundingBoxCalculator == null)
        {
            exporter.boundingBoxCalculator = GameObject.FindObjectOfType<BoundingBoxCalculator>(true);
        }

        if (exporter.boundingBoxCalculator != null)
            exporter.boundingBoxCalculator.gameObject.SetActive(true);

        // after mod generated - copy textures
        CopyThumbnailAndScreensToExportedModDirectory(exporter);

        EditorUtility.RevealInFinder(Path.Combine(buildPathModDir, ""));
    }

    void PrepareExportToAddressables(PTK_PackageExporter exporter)
    {
        AssetDatabase.SaveAssets();
        fExportProgress = 0.0f;


        if (exporter.boundingBoxCalculator != null)
        {
            exporter.boundingBoxCalculator.RefreshOffsets();
            exporter.boundingBoxCalculator.gameObject.SetActive(false);
        }

        string strUserCOnfigured_ModName = exporter.currentMod.ModName;
        string projectPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
        buildPathModDir = System.IO.Path.Combine(projectPath, "Mods", strUserCOnfigured_ModName);

        // Ensure the target directory exists
        Directory.CreateDirectory(buildPathModDir);

        if (exporter.bRegenerateThumbnails == true)
            exporter.currentMod.thumbnailsForObjects.Clear();

        exporter.currentMod.modContentInfo = new CPTK_ModContentInfoFile();
    }

    void ClearExistingAddressablesGroups()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        // Clear all groups
        var allGroups = new List<AddressableAssetGroup>(settings.groups);
        foreach (var group in allGroups)
        {
            if (group.name.ToLower().Contains("PTK_EnviroAssetsGroup".ToLower()) == true)
            {
                // we dont want to remove enviro asset group
                continue;
            }

            settings.RemoveGroup(group);
        }
    }

    Dictionary<AddressableAssetEntry, AddressableAssetGroup> GenerateAddressablesGroupsAndContentItems(PTK_PackageExporter exporter)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        Dictionary<AddressableAssetEntry, AddressableAssetGroup> entries = new Dictionary<AddressableAssetEntry, AddressableAssetGroup>();

        exporter.currentMod.LastBuildDateTime = DateTime.Now.ToString();

        string buildPath = System.IO.Path.Combine(buildPathModDir, "[BuildTarget]");

        AddressableAssetGroup modInfoGroup = settings.FindGroup("ModInfoGroup");
        if (modInfoGroup == null)
            modInfoGroup = CreateAndConfigureGroup("ModInfoGroup", buildPath, exporter.currentMod);

        settings.DefaultGroup = modInfoGroup; // important because default built in shaders will land in this group (and not in enviro group that is not included in mod package)
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        var currentModGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(exporter.currentMod));

        var modInfoEntry = settings.CreateOrMoveEntry(currentModGuid, modInfoGroup);
        modInfoEntry.SetAddress("ModInfo", false);


        int totalDirectories = exporter.treeView.GetCheckedItems().Count;
        int totalAssets = 0;

        foreach (var dirName in exporter.treeView.GetCheckedItems())
        {
            totalAssets += GetAssetsInDirectoryNonRecursive(dirName).Length;
        }

        int totalSteps = totalDirectories + totalAssets;

        List<PTK_Workshop_CharAnimConfig> alreadyInitializedAnimConfigs = new List<PTK_Workshop_CharAnimConfig>();
        int currentStep = 0;
        foreach (var dirName in exporter.treeView.GetCheckedItems())
        {
            // Create a group for the directory
            // Create a group for the directory
            var groupName = Path.GetFileNameWithoutExtension(dirName);  // Assuming you want the directory name as the group name
            AddressableAssetGroup newGroup = settings.FindGroup(groupName);
            if (newGroup == null)
            {
                newGroup = CreateAndConfigureGroup(groupName, buildPath, exporter.currentMod);
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

                if (ShouldAddItemInPathToAddressableGroup(fullPath) == false)
                    continue;

                var guid = AssetDatabase.AssetPathToGUID(fullPath);
                var entry = settings.CreateOrMoveEntry(guid, newGroup);

                string strItemOrPrefabAddressableKey = ConstructAddressableName(fullPath, groupName);
                entry.SetAddress(strItemOrPrefabAddressableKey, false);

                modItemConfigurator.ConfigureGameplayAddressableContentItem(exporter, fullPath, strItemOrPrefabAddressableKey, animConfig);

                entries[entry] = newGroup;

                currentStep++;
                fExportProgress = (float)currentStep / totalSteps;
                EditorUtility.DisplayProgressBar("Exporting", "Exporting...", fExportProgress);
                exporter.Repaint();
            }
        }

        return entries;
    }

    bool AreSelectedItemsForExportAreValidToExportAsSingleModPackage(PTK_PackageExporter exporter)
    {
        if (exporter.currentMod.modContentInfo.tracks.Count > 0)
        {
            if (exporter.currentMod.modContentInfo.tracks.Count != 1)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError("Single Mod should contain only one track!");
                return false;
            }


            if (exporter.currentMod.modContentInfo.characters.Count != 0 ||
                exporter.currentMod.modContentInfo.stickers.Count != 0 ||
                exporter.currentMod.modContentInfo.vehicles.Count != 0 ||
                exporter.currentMod.modContentInfo.wheels.Count != 0)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError("Race Track Mod can't have other items like characters, wheels,vehicles or stickers!");
                return false;
            }
        }

        return true;
    }
   
   
    AddressableAssetGroup CreateAndConfigureGroup(string strGroupName, string buildPath, PTK_ModInfo currentMod)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        string strUserCOnfigured_ModName = currentMod.ModName;

        AddressableAssetGroup newGroup = settings.CreateGroup(strGroupName, false, false, true, new List<AddressableAssetGroupSchema>());

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

    public static void SimplifyAddresses(Dictionary<AddressableAssetEntry, AddressableAssetGroup> entries)
    {
        foreach (var group in entries)
            group.Value.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, group.Key, false, true);
        AddressableAssetSettingsDefaultObject.Settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entries, true, false);
    }

    public string ConstructAddressableName(string assetPath, string strGroupName)
    {

        string[] forbiddenPhrases = { "Color Variations", "Assets", "Workshop", "Outfits" };

        // Split the path into parts.
        string[] parts = assetPath.Split(new char[] { '/', '\\' }, System.StringSplitOptions.RemoveEmptyEntries);

        // Filter out parts that contain any forbidden phrases.
        List<string> partsList = parts.Where(part => !forbiddenPhrases.Any(phrase => part.Contains(phrase))).ToList();

        if (partsList[partsList.Count - 1].Contains(".prefab"))
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


    //
    // Addressables Helper Functions
    //


    bool ValidateIfExportSettingsAreCorrect(PTK_PackageExporter exporter)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;

        if (settings == null)
        {
            Debug.LogError("Failed to access AddressableAssetSettings.");
            return false;
        }

        if (exporter.currentMod.UniqueModNameHashToGenerateItemsKeys == "")
        {
            Debug.LogError("Unique Mod Name is empty. Please assign unique mod name first!");
            return false;
        }

        if (exporter.currentMod.strUniqueModServerUpdateKEY == "")
        {
            Debug.LogError("Update KEY is empty! Please assign unique key before generating!");
            return false;
        }

        if (exporter.packageExporterGUI.modThumbnailTexPreview == null)
        {
            Debug.LogError("Thumbnail is required to generate mod!");
            return false;
        }

        if (exporter.packageExporterGUI.strUploadPassword == "")
        {
            Debug.LogError("Upload Password is required to generate mod!");
            return false;
        }

        string strModTexturesPreviewsPath = exporter.packageExporterGUI.GetCurrentModEditorSO_LocationDirPath(exporter);

        string strThumbnailPath1MBCheck = Path.Combine(strModTexturesPreviewsPath, "Thumbnail" + PTK_ModInfo.strThumbScreenImageExt);
        if (File.Exists(strThumbnailPath1MBCheck) == false)
        {
            Debug.LogError("File thumbnail not found in path: " + strThumbnailPath1MBCheck);
            return false;
        }

        string strDirectoryOfModWithFilesForTargetBuildPlatform = GetModFilesDirectoryForBuildPlatform(exporter.currentMod);

        float fThumbnailSizeMB = new FileInfo(strThumbnailPath1MBCheck).Length / (1024 * 1024.0f);
        exporter.packageExporterGUI.fCurrentMBThumbnailSize = fThumbnailSizeMB;
        if (fThumbnailSizeMB > 1.0f)
        {
            Debug.LogError("File thumnail size is higher than 1MB! Size: " + fThumbnailSizeMB.ToString() + " MB");
            return false;
        }

        if (exporter.currentMod.bUploadAndReplaceScreenshootsOnServer == true)
        {
            for (int i = 0; i < 4; i++)
            {
                string strScreenName = "Screen" + (i + 1).ToString() + PTK_ModInfo.strThumbScreenImageExt;
                string strScreenPath = Path.Combine(strModTexturesPreviewsPath, strScreenName);
                if (File.Exists(strScreenPath) == false)
                {
                    Debug.LogError("File screenshot not found in path: " + strScreenPath);
                    continue;
                }

                float fScreenMB = new FileInfo(strScreenPath).Length / (1024 * 1024.0f);
                if (fScreenMB > 1.0f)
                {
                    Debug.LogError("File screen size is higher than 1MB! Size: " + fScreenMB.ToString() + " MB");
                    return false;
                }
            }
        }


        if (exporter.currentMod == null)
        {
            return false;
        }

        return true;
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
            if (info.Exists)
            {
                if (bOnlyThisDirectory == true && strDirFullName != strTargetDirPath)
                    continue;

                if (assetPath.ToLower().Contains("ctrl+d"))
                    continue;

                if (assetPath.ToLower().Contains("tracks") == true)
                {
                    if (assetPath.ToLower().Contains(".unity") == true)
                    {
                        filteredAssets.Add(assetPath);
                    }

                    // we dont want add any other files from tracks directory other than scene
                    continue;
                }

                if (assetPath.Contains(".prefab") || assetPath.Contains("PTK_Workshop_Char") || assetPath.Contains("Info") || (assetPath.Contains("Blender") == false && assetPath.Contains(".fbx") == true)
                    || (assetPath.Contains("StickerTextures.asset") == true))
                    filteredAssets.Add(assetPath);
            }

        }

        return filteredAssets.ToArray();
    }


    bool ShouldAddItemInPathToAddressableGroup(string fullPath)
    {
        // Check if the asset is a .fbx and resides inside 'Color Variants' directory or its subdirectories
        if (fullPath.EndsWith(".fbx") && IsInsideColorVariantsDirectory(fullPath))
        {
            return false; // Skip this asset and move to the next one
        }

        if (fullPath.Contains("GameplayPrefabBase") == true)
            return false; // we don't need to include it (it will take extra space in mod)

        if (fullPath.Contains("ModelPreviewWithSuspension_DoNotIncludeInMod") == true)
            return false; // we don't need to include it (it will take extra space in mod)

        if (fullPath.Contains("Preview3DModels_IgnoreInBuild") == true)
            return false; // we don't need to include it (it will take extra space in mod)

        return true;
    }


    private void CopyThumbnailAndScreensToExportedModDirectory(PTK_PackageExporter exporter)
    {
        string strDirectoryOfModWithFilesForTargetBuildPlatform = GetModFilesDirectoryForBuildPlatform(exporter.currentMod);
        string strModTexturesPreviewsPath = exporter.packageExporterGUI.GetCurrentModEditorSO_LocationDirPath(exporter);

        // Copy the texture to the new path
        try
        {
            string strFileName = "";

            strFileName = "Thumbnail" + PTK_ModInfo.strThumbScreenImageExt;
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(buildPathModDir, strFileName), true); // copy into main mod directory (without platforms)
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(strDirectoryOfModWithFilesForTargetBuildPlatform, strFileName), true); // copy isnide Paltform type (StandaloneWIndows64) so offline loading will have thumbnail and screenshots to load

            strFileName = "Screen1"+ PTK_ModInfo.strThumbScreenImageExt;
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(buildPathModDir, strFileName), true); // copy into main mod directory (without platforms)
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(strDirectoryOfModWithFilesForTargetBuildPlatform, strFileName), true); // copy isnide Paltform type (StandaloneWIndows64) so offline loading will have thumbnail and screenshots to load

            strFileName = "Screen2"+ PTK_ModInfo.strThumbScreenImageExt;
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(buildPathModDir, strFileName), true); // copy into main mod directory (without platforms)
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(strDirectoryOfModWithFilesForTargetBuildPlatform, strFileName), true); // copy isnide Paltform type (StandaloneWIndows64) so offline loading will have thumbnail and screenshots to load

            strFileName = "Screen3"+ PTK_ModInfo.strThumbScreenImageExt;
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(buildPathModDir, strFileName), true); // copy into main mod directory (without platforms)
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(strDirectoryOfModWithFilesForTargetBuildPlatform, strFileName), true); // copy isnide Paltform type (StandaloneWIndows64) so offline loading will have thumbnail and screenshots to load

            strFileName = "Screen4"+ PTK_ModInfo.strThumbScreenImageExt;
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(buildPathModDir, strFileName), true); // copy into main mod directory (without platforms)
            File.Copy(strModTexturesPreviewsPath + strFileName, Path.Combine(strDirectoryOfModWithFilesForTargetBuildPlatform, strFileName), true); // copy isnide Paltform type (StandaloneWIndows64) so offline loading will have thumbnail and screenshots to load
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to copy texture: {ex.Message}");
        }
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

    // will contain StandaloneWindows64 etc. (inside Mods/MiaMod/StandaloneWindows64
    string GetModFilesDirectoryForBuildPlatform(PTK_ModInfo modInfo)
    {
        string strDirectoryOfMod = System.IO.Path.Combine(Application.dataPath, "..", "Mods", modInfo.ModName, EditorUserBuildSettings.selectedStandaloneTarget.ToString());
        return strDirectoryOfMod;
    }



    private void SaveUploadPasswordKey(PTK_PackageExporter exporter)
    {
        // simple guard to make sure uploading other mods won't be super easy
        File.WriteAllText(Path.Combine(buildPathModDir, PTK_ModInfo.strUploadKey_FileName), exporter.packageExporterGUI.strUploadPassword);

        // save in unity project
        File.WriteAllText(Path.Combine(exporter.packageExporterGUI.GetCurrentModEditorSO_LocationDirPath(exporter) + PTK_ModInfo.strUploadKey_FileName), exporter.packageExporterGUI.strUploadPassword);

        ///////////// ENCRYPT UPLOAD KEY
        string original = PTK_ModInfo.strNameToDecrypt_UploadPassword;
        string encrypted = EncryptString(original, exporter.packageExporterGUI.strUploadPassword);
        exporter.currentMod.strUploadHashedKey = encrypted;
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
