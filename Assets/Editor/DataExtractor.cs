using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using LithFAQ;
using Utility;
using UnityEditor.Connect;

public class DataExtractor : EditorWindow
{
    private static bool ShowLogErrors = false;

    private static readonly string ProjectFolder = "C:\\lomm\\data\\";
    private static readonly string GeneratedAssetsFolder = "Assets/GeneratedAssets";
    private static readonly string ABCMeshPath = $"{GeneratedAssetsFolder}/Meshes/ABCModels";
    private static readonly string TexturePath = $"{GeneratedAssetsFolder}/Textures";
    private static readonly string ModelMaterialPath = $"{GeneratedAssetsFolder}/ModelMaterials";
    private static readonly string ABCPrefabPath = $"{GeneratedAssetsFolder}/Prefabs/ABCModels";
    private static readonly string BSPPrefabPath = $"{GeneratedAssetsFolder}/Prefabs/BSPModels";
    private static readonly string ScenePath = $"{GeneratedAssetsFolder}/Scenes";

    private static readonly string ModelMaterialChildFolder_FromDAT = "FromDAT";
    private static readonly string ModelMaterialChildFolder_FromNameMatch = "FromNameMatch";
    private static readonly string ModelMaterialChildFolder_NoTexture = "NoTexture";

    private static readonly string DefaultMaterialPath = $"Assets/Materials/DefaultMaterial.mat";

    private static Material DefaultMaterial { get; set; }

    [MenuItem("Tools/Clear Progress Bar")]
    public static void Clear()
    {
        EditorUtility.ClearProgressBar();
        EditorApplication.isPlaying = false;
        Debug.Log("Progress bar cleared manually.");
    }

    private static bool GetVal()
    {
        return true;
    }

    [MenuItem("Tools/Generate All Assets")]
    public static void ExtractAll()
    {
        CreateDefaultMaterial();
        CreateGeneratedPaths();

        //if (GetVal())
        //{
        //    Debug.Log("Early out!");
        //    return;
        //}

        var unityDTXModels = GetAllUnityDTXModels();
        var abcModels = GetABCModels();
        var sprModels = GetAllSPRModels();
        var datModels = GetAllDATModels();

        // Get list of abcModels referenced by a DAT file. The DAT defines the "skins" for the ABC model.
        var abcWithSkinsModels = GetABCWithSkins(abcModels, datModels);
        var abcWithoutSkinsModels = abcModels.Where(
            abcModel => !abcWithSkinsModels.Any(
                abcWithSkinsModel => abcWithSkinsModel.ABCModel.RelativePathToABCFileLowercase == abcModel.RelativePathToABCFileLowercase))
            .ToList();

        var pngFiles = CreateTextures(unityDTXModels);

        // Get list of abcModels that have a matching DTX/PNG by name.
        // For example, If there's a file "cow.abc" that has no reference in any DAT or has no skins defined
        // but there's a "cow.dtx" file, then match those up.
        var abcModelsWithMatchingPNG = abcWithoutSkinsModels.Select(
            abcModel => new ABCWithPNGModel
            {
                ABCModel = abcModel,
                PNGFullPathAndFilename = pngFiles
                .Where(png => png.NameLowercase == abcModel.Name.ToLower())
                    .Select(png => png.RelativeTextureFilePath)
                    .FirstOrDefault()
            })
            .Where(x => x.PNGFullPathAndFilename != null)
            .ToList();

        var abcModelsWithNoReferences = abcWithoutSkinsModels.Where(
            abcWithoutSkinsModel => !abcModelsWithMatchingPNG.Any(
                x => x.ABCModel.Name.ToLower() == abcWithoutSkinsModel.Name.ToLower()))
            .ToList();

        CreateMaterials(abcWithSkinsModels, sprModels, unityDTXModels, ModelMaterialChildFolder_FromDAT);
        CreateMaterials(abcModelsWithMatchingPNG, unityDTXModels, ModelMaterialChildFolder_FromNameMatch);
        CreateABCPrefabs(abcWithSkinsModels);
        CreateABCPrefabs(abcModelsWithMatchingPNG);
        CreateABCPrefabs(abcModelsWithNoReferences);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("All data extracted.");
    }

    private static void CreateDefaultMaterial()
    {
        DefaultMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
    }

    private static string GetPNGLocation(string skin, List<SPRModel> sprModels)
    {
        string dtxPath;
        var extension = Path.GetExtension(skin);
        if (extension.Equals(".spr", StringComparison.OrdinalIgnoreCase))
        {
            var sprModel = sprModels.FirstOrDefault(x => x.RelativePathToSprite.Equals(skin, StringComparison.OrdinalIgnoreCase));
            if (sprModel == null)
            {
                Debug.LogWarning("GetPNGLocation could not find a sprite for location: " + skin);
                return null;
            }

            dtxPath = sprModel.DTXPaths[0];
        }
        else 
        {
            dtxPath = skin;
        }

        string relativePath = Path.GetDirectoryName(dtxPath);
        string justFilename = Path.GetFileNameWithoutExtension(dtxPath);

        return Path.Combine(TexturePath, relativePath, justFilename + ".png");
    }

    private static void CreateMaterials(List<ABCWithSkinModel> abcWithSkinsModels, List<SPRModel> sprModels, List<UnityDTX> unityDTXModels, string childFolderName)
    {
        int i = 0;
        foreach (var abcWithSkinsModel in abcWithSkinsModels)
        {
            foreach (string skinLowercase in abcWithSkinsModel.WorldObjectModel.SkinsLowercase)
            {
                i++;
                float progress = (float)i / abcWithSkinsModels.Count;
                EditorUtility.DisplayProgressBar("Creating materials", $"Item {i} of {abcWithSkinsModels.Count}", progress);

                string relativePath = Path.GetDirectoryName(skinLowercase);
                string skinName = Path.GetFileNameWithoutExtension(skinLowercase);

                string pathToPNG = GetPNGLocation(skinLowercase, sprModels);
                if (File.Exists(pathToPNG))
                {
                    string pathToMaterial = Path.Combine((Path.Combine(ModelMaterialPath, childFolderName, relativePath)).ConvertFolderSeperators().ToLower(), skinName + ".mat");
                    Directory.CreateDirectory(Path.GetDirectoryName(pathToMaterial));

                    // TODO Find matching DTX to get "Full Bright" from the header?
                    //var unityDTX = unityDTXModels.FirstOrDefault(x => x.RelativePathToDTX != null && x.RelativePathToDTX.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
                    bool useFullBright = false; // unityDTX == null ? false : unityDTX.Header.UseFullBright;

                    // TODO Check how we might know about Chroma Key
                    bool useChromaKey = false;

                    Texture2D texture2d = AssetDatabase.LoadAssetAtPath<Texture2D>(pathToPNG);

                    // TODO: Check if there's a material named "skinName" in the directory first. If so, add a number until you get unique.

                    var material = DTXConverter.CreateDefaultMaterial(skinName, texture2d, useFullBright, useChromaKey);

                    AssetDatabase.CreateAsset(material, pathToMaterial);
                }
                else
                {
                    Debug.Log($"Skipping creation of material - PNG not found for DTX: {skinLowercase}");
                }
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CreateMaterials(List<ABCWithPNGModel> abcWithPNGModels, List<UnityDTX> unityDTXModels, string childFolderName)
    {
        int i = 0;
        foreach (var abcWithPNGModel in abcWithPNGModels)
        {
            i++;
            float progress = (float)i / abcWithPNGModels.Count;
            EditorUtility.DisplayProgressBar("Creating materials for name-match ABC-to-PNG", $"Item {i} of {abcWithPNGModels.Count}", progress);

            string relativePath = Path.GetDirectoryName(abcWithPNGModel.PNGFullPathAndFilename);
            string skinName = Path.GetFileNameWithoutExtension(abcWithPNGModel.PNGFullPathAndFilename);

            string pathToPNG = GetPNGLocation(abcWithPNGModel.PNGFullPathAndFilename, null);
            if (File.Exists(pathToPNG))
            {
                string pathToMaterial = Path.Combine(
                    (Path.Combine(ModelMaterialPath, childFolderName, relativePath)).ConvertFolderSeperators().ToLower(),
                    skinName + ".mat");
                Directory.CreateDirectory(Path.GetDirectoryName(pathToMaterial));

                // TODO Find matching DTX to get "Full Bright" from the header?
                //var unityDTX = unityDTXModels.FirstOrDefault(x => x.RelativePathToDTX != null && x.RelativePathToDTX.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
                bool useFullBright = false; // unityDTX == null ? false : unityDTX.Header.UseFullBright;

                // TODO Check how we might know about Chroma Key
                bool useChromaKey = false;

                Texture2D texture2d = AssetDatabase.LoadAssetAtPath<Texture2D>(pathToPNG);

                // TODO: Check if there's a material named "skinName" in the directory first. If so, add a number until you get unique.

                var material = DTXConverter.CreateDefaultMaterial(skinName, texture2d, useFullBright, useChromaKey);

                AssetDatabase.CreateAsset(material, pathToMaterial);
            }
            else
            {
                Debug.Log($"Skipping creation of material - PNG not found for DTX: {pathToPNG}");
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CreateGeneratedPaths()
    {
        Directory.CreateDirectory(ABCMeshPath);
        Directory.CreateDirectory(TexturePath);
        Directory.CreateDirectory(ModelMaterialPath);
        Directory.CreateDirectory(ABCPrefabPath);
        Directory.CreateDirectory(BSPPrefabPath);
        Directory.CreateDirectory(ScenePath);
    }

    private static List<string> GetRelativePathToDTXFiles()
    {
        var files = Directory.GetFiles(ProjectFolder, "*.dtx", SearchOption.AllDirectories);
        return files.Select(x => Path.GetRelativePath(ProjectFolder, x)).ToList();
    }

    private static List<ABCModel> GetABCModels()
    {
        var abcFiles = Directory.GetFiles(ProjectFolder, "*.abc", SearchOption.AllDirectories);
        var abcModels = new List<ABCModel>();
        int i = 0;
        foreach (var abcFile in abcFiles)
        {
            i++;
            float progress = (float)i / abcFiles.Length;
            EditorUtility.DisplayProgressBar("Loading and processing ABC Models", $"Item {i} of {abcFiles.Length}", progress);

            var abcModel = ABCModelReader.LoadABCModel(abcFile, ProjectFolder);
            if (abcModel != null)
            {
                abcModels.Add(abcModel);
            }
        }

        EditorUtility.ClearProgressBar();

        return abcModels;
    }

    private static List<SPRModel> GetAllSPRModels()
    {
        var files = Directory.GetFiles(ProjectFolder, "*.spr", SearchOption.AllDirectories);
        var models = new List<SPRModel>();
        int i = 0;
        foreach (var file in files)
        {
            i++;
            float progress = (float)i / files.Length;
            EditorUtility.DisplayProgressBar("Loading and processing SPR files", $"Item {i} of {files.Length}", progress);

            var relativePath = Path.GetRelativePath(ProjectFolder, file);
            SPRModel model = SPRReader.LoadSPRModel(ProjectFolder, relativePath);
            if (model != null)
            {
                models.Add(model);
            }
        }

        EditorUtility.ClearProgressBar();

        return models;
    }

    private static List<UnityDTX> GetAllUnityDTXModels()
    {
        var files = Directory.GetFiles(ProjectFolder, "*.dtx", SearchOption.AllDirectories);
        var models = new List<UnityDTX>();
        int i = 0;
        foreach (var file in files)
        {
            i++;
            float progress = (float)i / files.Length;
            EditorUtility.DisplayProgressBar("Loading and processing DTX Textures", $"Item {i} of {files.Length}", progress);

            var dtxModel = DTXReader.LoadDTXModel(file, Path.GetRelativePath(ProjectFolder, file));
            if (dtxModel != null)
            {
                var model = DTXConverter.ConvertDTX(dtxModel);
                if (model != null)
                {
                    models.Add(model);
                }
            }
        }

        EditorUtility.ClearProgressBar();

        return models;
    }

    private static List<DATModel> GetAllDATModels()
    {
        var files = Directory.GetFiles(ProjectFolder, "*.dat", SearchOption.AllDirectories);
        var models = new List<DATModel>();
        int i = 0;
        foreach (var file in files)
        {
            i++;
            float progress = (float)i / files.Length;
            EditorUtility.DisplayProgressBar("Loading and processing DAT files", $"Item {i} of {files.Length}", progress);

            var datModel = DATModelReader.ReadDATModel(file, ProjectFolder, Game.LOMM);
            if (datModel != null)
            {
                datModel.SetAllWorldProperties();
                models.Add(datModel);
            }
        }

        EditorUtility.ClearProgressBar();

        return models;
    }

    private static List<ABCWithSkinModel> GetABCWithSkins(List<ABCModel> abcModels, List<DATModel> datModels)
    {
        EditorUtility.DisplayProgressBar("Getting ABC filenames from DAT files", $"Getting WorldObject models", 0f);
        var worldObjectModels = datModels.SelectMany(datModel => datModel.WorldObjects.Where(x => x.IsABC && x.SkinsLowercase.Count > 0)).ToList();

        var uniqueWorldObjectModels = worldObjectModels
            .GroupBy(g => new { g.FilenameLowercase, g.AllSkinsPathsLowercase })
            .Select(g => g.First())
            .ToList();

        int i = 0;
        var matchingABCModels = new List<ABCWithSkinModel>();
        foreach (var abcModel in abcModels)
        {
            i++;
            float progress = (float)i / abcModels.Count;
            EditorUtility.DisplayProgressBar("Matching ABC models to ones used by DAT files", $"Item {i} of {abcModels.Count}", progress);

            var matches = uniqueWorldObjectModels.Where(worldObjectModel => abcModel.RelativePathToABCFileLowercase == worldObjectModel.FilenameLowercase).ToList();
            if (matches.Any())
            {
                var abcWithSkinModels = matches.Select(
                    worldModel => new ABCWithSkinModel
                    {
                        ABCModel = abcModel,
                        WorldObjectModel = worldModel
                    });

                matchingABCModels.AddRange(abcWithSkinModels);
            }
        }

        // Make them distinct
        var uniqueABCModels = matchingABCModels
            .GroupBy(x => new { x.WorldObjectModel.AllSkinsPathsLowercase, x.ABCModel.RelativePathToABCFileLowercase })
            .Select(g => g.First())
            .ToList();

        var nonUniqueABCNames = uniqueABCModels.GroupBy(x => new { x.ABCModel.Name })
            .Where(x => x.Count() > 1)
            .Select(x => x.First().ABCModel.Name)
            .ToList();

        foreach(var nonUniqueABCName in nonUniqueABCNames)
        {
            int index = 0;
            foreach(var model in uniqueABCModels.Where(x => x.ABCModel.Name == nonUniqueABCName))
            {
                index++;
                model.UniqueIndex = index;
            }
        }

        return uniqueABCModels;
    }

    private static List<PNGFileInfo> CreateTextures(List<UnityDTX> unityDTXModels)
    {
        List<PNGFileInfo> pngFiles = new List<PNGFileInfo>();
        int i = 0;
        foreach (var unityDTXModel in unityDTXModels)
        {
            i++;
            float progress = (float)i / unityDTXModels.Count;
            EditorUtility.DisplayProgressBar("Processing and create textures", $"Item {i} of {unityDTXModels.Count}", progress);

            try
            {
                byte[] pngData = unityDTXModel.Texture2D.EncodeToPNG();
                if (pngData == null || pngData.Length == 0)
                {
                    if (ShowLogErrors)
                    {
                        Debug.LogError($"Could not convert {unityDTXModel.DTXModel.RelativePathToDTX} to PNG!");
                    }
                    continue;
                }

                string relativeTexturePath = Path.GetDirectoryName(Path.Combine(TexturePath, unityDTXModel.DTXModel.RelativePathToDTX));
                string pngFilename = Path.GetFileNameWithoutExtension(unityDTXModel.DTXModel.RelativePathToDTX) + ".png";
                Directory.CreateDirectory(relativeTexturePath);

                string relativeTextureFilePath = Path.Combine(relativeTexturePath, pngFilename);
                File.WriteAllBytes(relativeTextureFilePath, pngData);

                string relativePathToPNG = Path.ChangeExtension(unityDTXModel.DTXModel.RelativePathToDTX, "png");
                pngFiles.Add(
                    new PNGFileInfo
                    {
                        RelativeTextureFilePath = relativePathToPNG,
                        NameLowercase = Path.GetFileNameWithoutExtension(relativePathToPNG).ToLower()
                    });
            }
            catch(Exception ex)
            {
                if (ShowLogErrors)
                {
                    Debug.LogError($"Error creating texture for {(unityDTXModel == null ? "<null>" : unityDTXModel.DTXModel.RelativePathToDTX)}: {ex.Message}");
                }
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();

        return pngFiles;
    }

    private void CombineMeshes(GameObject levelGameObject)
    {
        // Combine all meshes not named PhysicsBSP
        foreach (var t in levelGameObject.GetComponentsInChildren<MeshFilter>())
        {
            if (t.transform.gameObject.name != "PhysicsBSP")
            {
                t.gameObject.MeshCombine(true);
            }
        }

        var gPhysicsBSP = GameObject.Find("PhysicsBSP");
        gPhysicsBSP.MeshCombine(true);

        // After mesh combine, we need to recalculate the normals
        MeshFilter[] meshFilters = gPhysicsBSP.GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter mf in meshFilters)
        {
            //mf.mesh.Optimize();
            mf.mesh.RecalculateNormals();
            mf.mesh.RecalculateTangents();
        }
    }

    private static Mesh CombineMeshes(List<Mesh> meshes)
    {
        CombineInstance[] combineInstances = new CombineInstance[meshes.Count];

        for (int i = 0; i < meshes.Count; i++)
        {
            combineInstances[i].mesh = meshes[i];
            combineInstances[i].transform = Matrix4x4.identity;
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combineInstances, true, true);

        return combinedMesh;
    }

    private static Mesh CreateMesh(Piece piece)
    {
        List<Mesh> individualMeshes = new List<Mesh>();

        var faces = piece.LODs[0].Faces;

        // only use the first LOD, we don't care about the rest.
        int faceIndex = 0;
        foreach (Face face in faces)
        {
            Mesh faceMesh = new Mesh();
            List<Vector3> faceVertices = new List<Vector3>();
            List<Vector3> faceNormals = new List<Vector3>();
            List<Vector2> faceUV = new List<Vector2>();
            List<int> faceTriangles = new List<int>();

            foreach (FaceVertex faceVertex in face.Vertices)
            {
                int originalVertexIndex = faceVertex.VertexIndex;

                // Add vertices, normals, and UVs for the current face
                faceVertices.Add(piece.LODs[0].Vertices[originalVertexIndex].Location * 0.01f);
                faceNormals.Add(piece.LODs[0].Vertices[originalVertexIndex].Normal);

                Vector2 uv = new Vector2(faceVertex.Texcoord.x, faceVertex.Texcoord.y);
                // Flip UV upside down - difference between Lithtech and Unity systems.
                uv.y = 1f - uv.y;

                faceUV.Add(uv);
                faceTriangles.Add(faceVertices.Count - 1);
            }

            faceMesh.vertices = faceVertices.ToArray();
            faceMesh.normals = faceNormals.ToArray();
            faceMesh.uv = faceUV.ToArray();
            faceMesh.triangles = faceTriangles.ToArray();

            individualMeshes.Add(faceMesh);
            faceIndex++;
        }

        // Combine all individual meshes into a single mesh
        Mesh combinedMesh = CombineMeshes(individualMeshes);

        return combinedMesh;
    }

    private static void AddColliders(GameObject levelGameObject)
    {
        // Assign the mesh collider to the combined meshes

        foreach (var t in levelGameObject.GetComponentsInChildren<MeshFilter>())
        {
            var mc = t.transform.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = t.mesh;
        }
    }

    private static Material GetMaterial(string matNameWithPathLowercase, string childFolderName)
    {
        var unityPath = Path.Combine(ModelMaterialPath, childFolderName, matNameWithPathLowercase);
        if (!File.Exists(unityPath))
        {
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(unityPath);
        return material;
    }

    private static GameObject CreateObjectFromABC(ABCModel abcModel, List<string> materialPaths, bool useDefaultMaterial, string childFolderName)
    {
        var rootObject = new GameObject(abcModel.Name);
        rootObject.transform.position = Vector3.zero;
        rootObject.transform.rotation = Quaternion.identity;
        rootObject.transform.localScale = Vector3.one;

        foreach (var piece in abcModel.Pieces)
        {
            GameObject modelInGameObject = new GameObject(piece.Name);
            modelInGameObject.transform.parent = rootObject.transform;
            var meshFilter = modelInGameObject.AddComponent<MeshFilter>();
            var meshRenderer = modelInGameObject.AddComponent<MeshRenderer>();
            
            meshFilter.sharedMesh = CreateMesh(piece);
            meshFilter.sharedMesh.RecalculateBounds();

            // Sometimes people don't specify a second, third or fourth texture... so we need to check if the index is out of bounds
            if (useDefaultMaterial)
            {
                meshRenderer.sharedMaterial = DefaultMaterial;
            }
            else
            {
                if (piece.MaterialIndex > materialPaths.Count - 1)
                {
                    piece.MaterialIndex = (ushort)(materialPaths.Count - 1);
                }

                var matNameWithPathLowercase = materialPaths[piece.MaterialIndex];
                var material = GetMaterial(matNameWithPathLowercase, childFolderName);
                if (material == null)
                {
                    material = DefaultMaterial;
               }

                meshRenderer.sharedMaterial = material;
            }
        }

        rootObject.MeshCombine(true);
        rootObject.tag = LithtechTags.NoRayCast;

        return rootObject;
    }

    private static void CreatePrefabFromABC(ABCModel abcModel, string nameSuffix, GameObject go, bool createPrefab, string childFolderName)
    {
        // Save mesh
        var meshFilter = go.GetComponent<MeshFilter>();
        var mesh = meshFilter.sharedMesh;
        var relativePathToABC = Path.GetDirectoryName(abcModel.RelativePathToABCFileLowercase);
        string meshPathAndFilename = Path.Combine(ABCMeshPath, childFolderName, relativePathToABC, abcModel.Name + nameSuffix + ".asset");
        Directory.CreateDirectory(Path.GetDirectoryName(meshPathAndFilename));
        AssetDatabase.CreateAsset(mesh, meshPathAndFilename);

        if (createPrefab)
        {
            // Save prefab
            string prefabPathAndFilename = Path.Combine(ABCPrefabPath, childFolderName, relativePathToABC, abcModel.Name + nameSuffix + ".prefab");
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPathAndFilename));
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPathAndFilename);
        }

        DestroyImmediate(go);
    }

    private static void CreateABCPrefabs(List<ABCWithSkinModel> abcWithSkinsModels)
    {
        int i = 0;
        foreach (var abcWithSkinModel in abcWithSkinsModels)
        {
            i++;
            float progress = (float)i / abcWithSkinsModels.Count;
            EditorUtility.DisplayProgressBar("Creating ABC Prefabs with skins", $"Item {i} of {abcWithSkinsModels.Count}", progress);

            List<string> materialPaths = abcWithSkinModel.WorldObjectModel.SkinsLowercase.Select(skin => Path.ChangeExtension(skin, "mat")).ToList();
            GameObject go = CreateObjectFromABC(abcWithSkinModel.ABCModel, materialPaths, false, ModelMaterialChildFolder_FromDAT);

            string nameSuffix = (abcWithSkinModel.UniqueIndex != 0
                ? abcWithSkinModel.UniqueIndex.ToString()
                : string.Empty);
            CreatePrefabFromABC(abcWithSkinModel.ABCModel, nameSuffix, go, true, ModelMaterialChildFolder_FromDAT);
        }

        EditorUtility.ClearProgressBar();
    }

    private static void CreateABCPrefabs(List<ABCWithPNGModel> abcWithPNGModels)
    {
        int i = 0;
        foreach (var abcWithPNGModel in abcWithPNGModels)
        {
            i++;
            float progress = (float)i / abcWithPNGModels.Count;
            EditorUtility.DisplayProgressBar("Creating ABC Prefabs with matching PNG", $"Item {i} of {abcWithPNGModels.Count}", progress);

            List<string> materialPaths = new List<string>
            {
                Path.ChangeExtension(abcWithPNGModel.PNGFullPathAndFilename, "mat")
            };
            GameObject go = CreateObjectFromABC(abcWithPNGModel.ABCModel, materialPaths, false, ModelMaterialChildFolder_FromNameMatch);

            CreatePrefabFromABC(abcWithPNGModel.ABCModel, string.Empty, go, true, ModelMaterialChildFolder_FromNameMatch);
        }

        EditorUtility.ClearProgressBar();
    }

    private static void CreateABCPrefabs(List<ABCModel> abcModels)
    {
        int i = 0;
        foreach (var abcModel in abcModels)
        {
            i++;
            float progress = (float)i / abcModels.Count;
            EditorUtility.DisplayProgressBar("Creating ABC Assets (no materials/skins)", $"Item {i} of {abcModels.Count}", progress);

            GameObject go = CreateObjectFromABC(abcModel, null, true, ModelMaterialChildFolder_NoTexture);

            CreatePrefabFromABC(abcModel, string.Empty, go, false, ModelMaterialChildFolder_NoTexture);
        }

        EditorUtility.ClearProgressBar();
    }
}
