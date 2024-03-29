using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public class PTK_ModItemConfigurator
{
    public const string rootPath = "Assets/Workshop_Content";
    private Regex Pattern_CharacterOnly = new Regex(@"Workshop_Content/Characters/(?<characterName>[^/]+)");
    private Regex Pattern_Character = new Regex(@"Workshop_Content/Characters/(?<characterName>[^/]+)/Outfits/(?<outfit>[^/]+)/Color Variations/(?<materialVar>[^/]+)");
    private static readonly Regex Pattern_AnimConfig = new Regex(@"Workshop_Content/Characters/(?<characterName>[^/]+)");

    private Regex Pattern_Vehicles = new Regex(@"Workshop_Content/Vehicles/(?<Name>[^/]+)/Color Variations/(?<materialVar>[^/]+)");
    private Regex Pattern_Wheels = new Regex(@"Workshop_Content/Wheels/(?<Name>[^/]+)/Color Variations/(?<materialVar>[^/]+)");
    private Regex Pattern_Stickers = new Regex(@"Workshop_Content/Stickers/(?<Name>[^/]+)/Color Variations/(?<materialVar>[^/]+)");

    private Regex Pattern_Tracks = new Regex(@"Workshop_Content/Tracks/(?<Name>[^/]+)/");

    public void ConfigureGameplayAddressableContentItem(PTK_PackageExporter exporter,string strFullPath, string strItemOrPrefabAddressableKey, PTK_Workshop_CharAnimConfig animConfig)
    {
        if (strFullPath.Contains("CharacterInfo"))
        {
            var match = Pattern_CharacterOnly.Match(strFullPath);
            string strCharacterName = match.Groups["characterName"].Value;

            string strCharacterInfoFilePath = strFullPath;// assetPathsInDir.Where(path => path.Contains("CharacterInfo")).ToArray().FirstOrDefault();
            PTK_CharacterInfoSO charInfo = AssetDatabase.LoadAssetAtPath<PTK_CharacterInfoSO>(strCharacterInfoFilePath);

            if (charInfo != null)
            {
                charInfo.CopyInfoTo(exporter.currentMod.modContentInfo, strCharacterName);
            }
        }
        else if (strFullPath.Contains("PTK_Workshop_Char Anim Config") == true)
        {
            var match = Pattern_AnimConfig.Match(strFullPath);

            if (match.Success)
            {
                string strCharacterName = match.Groups["characterName"].Value;
                exporter.currentMod.modContentInfo.GetCharacterFromDirectoryName(strCharacterName, true).strCharacterAnimConfigFileName = strItemOrPrefabAddressableKey;
            }
            else
            {
                Debug.LogError("Cant match PTK_Workshop_Char Anim Config file name!");
            }
        }
        else if (strFullPath.Contains("Workshop_Content/Characters"))
        {
            var match = Pattern_Character.Match(strFullPath);

            if (match.Success == true)
            {
                string strCharacterName = match.Groups["characterName"].Value;
                string strCharacterOutfit = match.Groups["outfit"].Value;
                string strMaterialVar = match.Groups["materialVar"].Value;

                string strPrefabAddressableKey = strItemOrPrefabAddressableKey;
                CPTK_ModContentInfoFile.CCharacter.CCharacterOutfit.CCharacterOutfit_Material charOutfitMaterial = exporter.currentMod.modContentInfo.GetCharacterFromDirectoryName(strCharacterName, true).GetOutfitFromName(strCharacterOutfit, true).GetMatVariantFromName(strMaterialVar, true);
                charOutfitMaterial.strPrefabFileName_AddressableKey = strPrefabAddressableKey;
                charOutfitMaterial.iGeneratedTargetUniqueConfigID = GetStringHashWithMD5(exporter.currentMod.UniqueModNameHashToGenerateItemsKeys + strPrefabAddressableKey);


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
                    PTK_ModCharAnimListRef myComponent = loadedPrefab.GetComponent<PTK_ModCharAnimListRef>();
                    if (myComponent == null)
                    {
                        myComponent = loadedPrefab.AddComponent<PTK_ModCharAnimListRef>();
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
                if (File.Exists(strTargetDirectory) == true && exporter.bRegenerateThumbnails == false)
                {
                }
                else
                {
                    if (exporter.boundingBoxCalculator == null)
                    {
                        Debug.LogError("Package need to be created with Workshop scene open!");
                        return;
                    }

                    if (Directory.Exists(Path.GetDirectoryName(strTargetDirectory)) == false)
                        Directory.CreateDirectory(Path.GetDirectoryName(strTargetDirectory));


                    GameObject instance = GameObject.Instantiate(prefabAsset);

                    if (animConfig.CharacterA.Menu.Count == 0)
                    {
                        Debug.LogError("Character doesnt have menu animation to render icon: " + strFullPath);
                    }
                    else
                    {
                        animConfig.CharacterA.Menu[0].SampleAnimation(instance, 0.0f);
                    }

                    var childWithBestPath = exporter.boundingBoxCalculator.GetChildWithBestPath(strFullPath);

                    if (childWithBestPath != null)
                    {
                        instance.transform.position = childWithBestPath.vPosition;
                        instance.transform.rotation = childWithBestPath.qRotation;
                        instance.transform.localScale = childWithBestPath.vLoosyScale;
                    }

                    ThumbnailGenerate.TakeScreenshoot(2048, 2048, Camera.main, true, strTargetDirectory, true, 512, 512);
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
                    exporter.currentMod.thumbnailsForObjects.Add(thumb);

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

                string strPrefabAddressableKey = strItemOrPrefabAddressableKey;
                CPTK_ModContentInfoFile.CItemWithColorVariant.CItemColorVariant itemColorVariant = exporter.currentMod.modContentInfo.GetVehicleFromDirectoryName(strName, true).GetColorVariantFromName(strMaterialVar, true);
                itemColorVariant.strPrefabFileName_AddressableKey = strPrefabAddressableKey;
                itemColorVariant.iGeneratedTargetUniqueConfigID = GetStringHashWithMD5(exporter.currentMod.UniqueModNameHashToGenerateItemsKeys + strPrefabAddressableKey);

                EnsureVehicleStickersHaveMeshReadWriteEnabled(strFullPath);
                UpdateModFileItemFor_CItemWithColorVariant(exporter, itemColorVariant, strFullPath, strPrefabAddressableKey);
            }
        }
        else if (strFullPath.Contains("Workshop_Content/Wheels"))
        {
            var match = Pattern_Wheels.Match(strFullPath);

            if (match.Success == true)
            {
                string strName = match.Groups["Name"].Value;
                string strMaterialVar = match.Groups["materialVar"].Value;

                string strPrefabAddressableKey = strItemOrPrefabAddressableKey;
                CPTK_ModContentInfoFile.CItemWithColorVariant.CItemColorVariant itemColorVariant = exporter.currentMod.modContentInfo.GetWheelFromDirectoryName(strName, true).GetColorVariantFromName(strMaterialVar, true);
                itemColorVariant.strPrefabFileName_AddressableKey = strPrefabAddressableKey;
                itemColorVariant.iGeneratedTargetUniqueConfigID = GetStringHashWithMD5(exporter.currentMod.UniqueModNameHashToGenerateItemsKeys + strPrefabAddressableKey);

                UpdateModFileItemFor_CItemWithColorVariant(exporter, itemColorVariant, strFullPath, strPrefabAddressableKey);
            }
        }
        else if (strFullPath.Contains("Workshop_Content/Stickers"))
        {
            var match = Pattern_Stickers.Match(strFullPath);

            if (match.Success == true)
            {
                string strName = match.Groups["Name"].Value;
                string strMaterialVar = match.Groups["materialVar"].Value;

                string strPrefabAddressableKey = strItemOrPrefabAddressableKey;
                CPTK_ModContentInfoFile.CItemWithColorVariant.CItemColorVariant itemColorVariant = exporter.currentMod.modContentInfo.GetStickerFromDirectoryName(strName, true).GetColorVariantFromName(strMaterialVar, true);
                itemColorVariant.strPrefabFileName_AddressableKey = strPrefabAddressableKey;
                itemColorVariant.iGeneratedTargetUniqueConfigID = GetStringHashWithMD5(exporter.currentMod.UniqueModNameHashToGenerateItemsKeys + strPrefabAddressableKey);

                UpdateModFileItemFor_CItemWithColorVariant(exporter, itemColorVariant, strFullPath, strPrefabAddressableKey);
            }
        }
        else if (strFullPath.Contains("Workshop_Content/Tracks"))
        {
            var match = Pattern_Tracks.Match(strFullPath);

            if (match.Success == true)
            {
                string strName = match.Groups["Name"].Value;

                string strPrefabAddressableKey = strItemOrPrefabAddressableKey;
                CPTK_ModContentInfoFile.CTrackInfo trackInfo = exporter.currentMod.modContentInfo.GetTrackFromDirectoryName(strName, true);
                trackInfo.strTrackSceneName_AddressableKey = strPrefabAddressableKey;
                trackInfo.iGeneratedTargetUniqueConfigID = GetStringHashWithMD5(exporter.currentMod.UniqueModNameHashToGenerateItemsKeys + strPrefabAddressableKey);

                UpdateTrackModFileItem(exporter,trackInfo, strFullPath, strPrefabAddressableKey);
            }
        }
    }

    void UpdateTrackModFileItem(PTK_PackageExporter exporter,CPTK_ModContentInfoFile.CTrackInfo trackInfo, string strFullPath, string strPrefabAddressableKey)
    {
        string strSceneDir = Path.GetDirectoryName(strFullPath);
        string strTargetFilePathForThumbPNG = strSceneDir + "/" + "TrackThumbnail" + ".png";


        if (File.Exists(strTargetFilePathForThumbPNG) == false)
        {
            Debug.LogError("Thumbnail not found for scene: " + strTargetFilePathForThumbPNG);
            return;
        }
        // THUMBNAILS
        // we dont want to generate again
        if (File.Exists(strTargetFilePathForThumbPNG) == true && exporter.bRegenerateThumbnails == false)
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

        if (exporter.currentMod.GetThumbnailForObject(strPrefabAddressableKey) == false)
        {
            Sprite createdSprite = AssetDatabase.LoadAssetAtPath<Sprite>(strTargetFilePathForThumbPNG);

            PTK_ModInfo.CThumbForObject thumb = new PTK_ModInfo.CThumbForObject();
            thumb.strObjDirName_AddressableKey = strPrefabAddressableKey;
            thumb.spriteThumbnail = createdSprite;
            exporter.currentMod.thumbnailsForObjects.Add(thumb);
        }
    }

    void UpdateModFileItemFor_CItemWithColorVariant(PTK_PackageExporter exporter, CPTK_ModContentInfoFile.CItemWithColorVariant.CItemColorVariant itemColorVariant, string strFullPath, string strPrefabAddressableKey)
    {
        string strDirSkin = Path.GetDirectoryName(strFullPath);
        string strTargetFilePathForThumbPNG = strDirSkin + "/" + strPrefabAddressableKey + ".png";



        // THUMBNAILS
        // we dont want to generate again
        if (File.Exists(strTargetFilePathForThumbPNG) == true && exporter.bRegenerateThumbnails == false)
        {
        }
        else
        {
            if (Directory.Exists(Path.GetDirectoryName(strTargetFilePathForThumbPNG)) == false)
                Directory.CreateDirectory(Path.GetDirectoryName(strTargetFilePathForThumbPNG));



            if (itemColorVariant.eItemType == CPTK_ModContentInfoFile.CItemWithColorVariant.EType.E_STICKER)
            {
                PTK_StickerTexures stickerTextures = AssetDatabase.LoadAssetAtPath<PTK_StickerTexures>(strFullPath);

                if (stickerTextures == null)
                {
                    Debug.LogError("There is no PTK_StickerTexures scriptable object inside path. Please make sure you duplicated CTRL+D directory for new one " + strFullPath);
                    return;
                }

                string[] strFilesInsideStickerDir = Directory.GetFiles(Path.GetDirectoryName(strFullPath));
                string strClampTexturePath = "";
                for (int i = 0; i < strFilesInsideStickerDir.Length; i++)
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

                if (strClampTexturePath == "")
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
                    if (textureClampImporter.maxTextureSize != 1024)
                    {
                        textureClampImporter.maxTextureSize = 1024;
                        bChanged = true;
                    }

                    // Set the wrap mode
                    if (textureClampImporter.wrapMode != TextureWrapMode.Clamp)
                    {
                        textureClampImporter.wrapMode = TextureWrapMode.Clamp;
                        bChanged = true;
                    }

                    if (textureClampImporter.alphaIsTransparency == false)
                    {
                        bChanged = true;
                        textureClampImporter.alphaIsTransparency = true;
                    }

                    // Apply the changes to the textureClampImporter

                    if (bChanged == true)
                        AssetDatabase.ImportAsset(strClampTexturePath, ImportAssetOptions.ForceUpdate);
                }
                else
                {
                    Debug.LogError("Could not find texture importer at path: " + strClampTexturePath);
                }


                stickerTextures.textureClamp = textureClamp;
                EditorUtility.SetDirty(stickerTextures);

                strTargetFilePathForThumbPNG = strDirSkin + "/" + "StickerThumbnail_" + strPrefabAddressableKey + ".png";

                if (File.Exists(strTargetFilePathForThumbPNG) == false || exporter.bRegenerateThumbnails == true)
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
                if (exporter.boundingBoxCalculator == null)
                {
                    Debug.LogError("Package need to be created with Workshop scene open!");
                    return;
                }

                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(strFullPath);
                GameObject instance = GameObject.Instantiate(prefabAsset);

                var childWithBestPath = exporter.boundingBoxCalculator.GetChildWithBestPath(strFullPath);

                if (childWithBestPath != null)
                {
                    instance.transform.position = childWithBestPath.vPosition;
                    instance.transform.rotation = childWithBestPath.qRotation;
                    instance.transform.localScale = childWithBestPath.vLoosyScale;
                }

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
            exporter.currentMod.thumbnailsForObjects.Add(thumb);

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
}
