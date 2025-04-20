using System;
using System.IO;
using UnityEngine;

public static class DTX
{
    private static string GetDefaultTexturePath(string projectPath)
    {
        //Check if WorldTextures\invisible.dtx exists, if not then check Tex\invisible.dtx
        //This should cover most lithtech games
        string newPath = Path.Combine(projectPath, "WorldTextures\\invisible.dtx");
        if (File.Exists(newPath))
        {
            return newPath;
        }

        newPath = Path.Combine(projectPath, "Tex\\invisible.dtx");
        if (File.Exists(newPath))
        {
            return newPath;
        }

        // Support for LoMM
        newPath = Path.Combine(projectPath, "Textures\\LevelTextures\\Misc\\invisible.dtx");
        if (File.Exists(newPath))
        {
            return newPath;
        }

        return null;
    }

    private static string GetFullTexturePathToDTX(string relativePath, string projectPath)
    {
        string filenameAndFullPathToDTX;
        if (Path.GetExtension(relativePath).ToLower() == ".spr")
        {
            // Should be a sprite.
            // Load the sprite and pull offthe first texture (index 0).
            var unitySPR = SPRReader.LoadSPRModel(projectPath, relativePath);
            if (unitySPR == null || unitySPR.DTXPaths == null || unitySPR.DTXPaths.Length == 0)
            {
                return null;
            }

            filenameAndFullPathToDTX = Path.Combine(projectPath, unitySPR.DTXPaths[0]);
        }
        else
        {
            filenameAndFullPathToDTX = Path.Combine(projectPath, relativePath);
        }

        // If above is not found, use default texture (if it exists).
        if (!File.Exists(filenameAndFullPathToDTX))
        {
            filenameAndFullPathToDTX = GetDefaultTexturePath(projectPath);
        }

        return filenameAndFullPathToDTX;
    }

    public static DTXReturn LoadDTXIntoLibrary(string relativePath, DTXMaterialLibrary dtxMaterial, string projectPath)
    {
        if (dtxMaterial.textures.ContainsKey(relativePath))
        {
            return DTXReturn.ALREADYEXISTS;
        }

        // Fix the path to be a valid path - OR return with Failed.
        string filenameAndFullPathToDTX = GetFullTexturePathToDTX(relativePath, projectPath);
        if (filenameAndFullPathToDTX == null)
        {
            Debug.LogError("Exiting LoadDTXIntoLibrary with Failed - filenameAndFullPathToDTX is null");
            return DTXReturn.FAILED;
        }

        var dtxModel = DTXReader.LoadDTXModel(filenameAndFullPathToDTX, relativePath);
        if (dtxModel == null)
        {
            Debug.LogError("Exiting LoadDTXIntoLibrary with Failed - dtxModel is null");
            return DTXReturn.FAILED;
        }

        var unityDTX = DTXConverter.ConvertDTX(dtxModel);
        if (unityDTX == null)
        {
            Debug.LogError("Exiting LoadDTXIntoLibrary with Failed - unityDTX is null");
            return DTXReturn.FAILED;
        }

        // Index off of relativePath. This will use the path to the DTX or the SPR.
        // Prevents issues with using filename which might be duplicated in subfolders.
        AddTextureToMaterialDictionary(relativePath, unityDTX.Texture2D, dtxMaterial);
        AddMaterialToMaterialDictionary(relativePath, unityDTX.Material, dtxMaterial);
        AddTexSizeToDictionary(relativePath, unityDTX.TextureSize, dtxMaterial);

        return DTXReturn.SUCCESS;
    }

    private static void AddTextureToMaterialDictionary(string filename, Texture2D texture2D, DTXMaterialLibrary dtxMaterial)
    {
        if (!dtxMaterial.textures.ContainsKey(filename))
        {
            dtxMaterial.textures.Add(filename, texture2D);
        }
    }

    public static void AddMaterialToMaterialDictionary(string filename, Material mat, DTXMaterialLibrary dtxMaterial)
    {
        if (!dtxMaterial.materials.ContainsKey(filename))
        {
            mat.name = filename;

            String[] splitName;
            if (mat.name.Contains("_Chromakey"))
            {
                splitName = mat.name.Split("_Chromakey");
                try
                {
                    mat.mainTexture = dtxMaterial.textures[splitName[0]];
                }
                catch (Exception)
                {

                    return;
                }
               
                mat.SetFloat("_Metallic", 0.9f);
                mat.SetFloat("_Smoothness", 0.8f);
                mat.SetColor("_Color", Color.white);
                dtxMaterial.materials.Add(filename, mat);
                return;
            }
            
            mat.mainTexture = dtxMaterial.textures[filename];
            mat.SetFloat("_Metallic", 0.9f);
            mat.SetFloat("_Smoothness", 0.8f);

            dtxMaterial.materials.Add(filename, mat);
        }
    }

    private static void AddTexSizeToDictionary(string filename, TextureSize texInfo, DTXMaterialLibrary dtxMaterial)
    {
        if (!dtxMaterial.texSize.ContainsKey(filename))
        {
            dtxMaterial.texSize.Add(filename, texInfo);
        }
    }
}