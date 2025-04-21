using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using LithFAQ;
using Utility;
using UnityEngine.Rendering;

public class DataExtractor : EditorWindow
{
    private static readonly bool ShowLogErrors = false;
    private static readonly float UnityScaleFactor = 0.01f;
    private static readonly string DefaultMaterialPath = $"Assets/Materials/DefaultMaterial.mat";
    private static readonly string ProjectFolder = "C:\\lomm\\data\\";

    private static readonly string GeneratedAssetsFolder = "Assets/GeneratedAssets";
    private static readonly string TexturePath = $"{GeneratedAssetsFolder}/Textures";
    private static readonly string ABCMeshPath = $"{GeneratedAssetsFolder}/Meshes/ABCModels";
    private static readonly string ModelMaterialPath = $"{GeneratedAssetsFolder}/ModelMaterials";
    private static readonly string ABCPrefabPath = $"{GeneratedAssetsFolder}/Prefabs/ABCModels";

    private static readonly string BSPMaterialPath = $"{GeneratedAssetsFolder}/BSPMaterials";
    private static readonly string BSPMeshPath = $"{GeneratedAssetsFolder}/Meshes/BSPModels";
    private static readonly string BSPPrefabPath = $"{GeneratedAssetsFolder}/Prefabs/BSPModels";
    private static readonly string ScenePath = $"{GeneratedAssetsFolder}/Scenes";

    private static readonly string ModelMaterialChildFolder_FromDAT = "FromDAT";
    private static readonly string ModelMaterialChildFolder_FromNameMatch = "FromNameMatch";
    private static readonly string ModelMaterialChildFolder_NoTexture = "NoTexture";

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

        //if (GetVal()) { Debug.Log("Early out!"); return; }

        // Step 1 - Load everything from the ProjectFolder
        var unityDTXModels = GetAllUnityDTXModels();
        var abcModels = GetABCModels();
        var sprModels = GetAllSPRModels();
        var datModels = GetAllDATModels();

        //var worldObjects = datModels.SelectMany(dat => dat.WorldObjects).ToList();
        //var properties = worldObjects.SelectMany(worldObject => worldObject.Properties).ToList();
        //var groupedProperties = properties.GroupBy(p => p.Name);
        //string s = "Properties:\r\n";
        //foreach(var group in groupedProperties.OrderByDescending(x => x.Count()))
        //{
        //    s += $"{group.Count()} - {group.Key} (type={group.First().PropType})\r\n";
        //}
        //Debug.Log(s);

        // var pngFiles = CreateTextures(unityDTXModels);

        // Step 2 - Create assets related to ABC models:
        // CreateAssetsFromABCModels(abcModels, sprModels, datModels, unityDTXModels, pngFiles);

        // Step 3 - Create assets for BSPs (from DATs)
        CreateAssetsFromDATModels(abcModels, sprModels, datModels, unityDTXModels);

        // Step 6 - Create meshes for BSPs (from DATs)

        // Step 7 - Create scene prefab with references to BSP mesh, models, lights, etc.

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("All data extracted.");
    }

    /// <summary>
    /// Create assets related to ABC models
    /// NOTE:   Textures should already exist at the appropriate sub-folder: \{TexturePath}\
    ///         Materials will look for PNG files located there.
    ///         Textures are assumed to be PNG files - it looks for this extension specifically.
    /// 
    /// Assets created:
    ///     Materials:
    ///         \{ModelMaterialPath}\FromDAT\       <-- ABC files found referenced by DAT files (they'll usually have a Skin associated).
    ///         \{ModelMaterialPath}\FromNameMatch\ <-- ABC files with a same-named DTX/PNG. For example "models\bandit.abc" would find bandit.dtx in any folder.
    ///
    ///     Meshes:
    ///         \{ABCMeshPath}\FromDAT\             <-- ABC files found referenced by DAT files (they'll usually have a Skin associated).
    ///         \{ABCMeshPath}\FromNameMatch\       <-- ABC files with a same-named DTX/PNG. For example "models\bandit.abc" would find bandit.dtx in any folder.
    ///         \{ABCMeshPath}\NoTexture\           <-- ABC files with no texture references that can be inferred.
    ///     
    ///     Prefabs
    ///         \{ABCPrefabPath}\FromDAT\           <-- ABC files found referenced by DAT files (they'll usually have a Skin associated).
    ///         \{ABCPrefabPath}\FromNameMatch\     <-- ABC files with a same-named DTX/PNG. For example "models\bandit.abc" would find bandit.dtx in any folder.
    /// </summary>
    /// <param name="abcModels"></param>
    /// <param name="sprModels"></param>
    /// <param name="datModels"></param>
    /// <param name="unityDTXModels"></param>
    private static void CreateAssetsFromABCModels(List<ABCModel> abcModels, List<SPRModel> sprModels, List<DATModel> datModels, List<UnityDTX> unityDTXModels, List<PNGFileInfo> pngFiles)
    {
        // Step 2a - Find skins (textures) for ABC Models
        ABCReferenceModels abcReferenceModels = GetABCReferences(abcModels, sprModels, datModels, unityDTXModels, pngFiles);

        // Step 2b - Create materials for models
        CreateModelMaterials(abcReferenceModels.ABCWithSkinsModels, sprModels, unityDTXModels, ModelMaterialChildFolder_FromDAT);
        CreateModelMaterials(abcReferenceModels.ABCWithPNGModels, unityDTXModels, ModelMaterialChildFolder_FromNameMatch);

        // Step 2c - Create meshes and prefabs.
        // Only creates a prefab if there's a material referencing the ABCFile.
        // Otherwise the mesh will be enough. Someone will have to apply a material later.
        CreateABCPrefabs(abcReferenceModels.ABCWithSkinsModels);
        CreateABCPrefabs(abcReferenceModels.ABCWithPNGModels);
        CreateABCPrefabs(abcReferenceModels.ABCModelsWithNoReferences);
    }

    private static ABCReferenceModels GetABCReferences(List<ABCModel> abcModels, List<SPRModel> sprModels, List<DATModel> datModels, List<UnityDTX> unityDTXModels, List<PNGFileInfo> pngFiles)
    {
        // Get list of abcModels referenced by a DAT file. The DAT defines the "skins" for the ABC model.
        var abcWithSkinsModels = GetABCWithSkins(abcModels, datModels);
        var abcWithoutSkinsModels = abcModels.Where(
            abcModel => !abcWithSkinsModels.Any(
                abcWithSkinsModel => abcWithSkinsModel.ABCModel.RelativePathToABCFileLowercase == abcModel.RelativePathToABCFileLowercase))
            .ToList();

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

        return new ABCReferenceModels
        {
            ABCWithSkinsModels = abcWithSkinsModels,
            ABCWithPNGModels = abcModelsWithMatchingPNG,
            ABCModelsWithNoReferences = abcModelsWithNoReferences
        };
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

    private static void CreateModelMaterials(List<ABCWithSkinModel> abcWithSkinsModels, List<SPRModel> sprModels, List<UnityDTX> unityDTXModels, string childFolderName)
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

    private static void CreateModelMaterials(List<ABCWithPNGModel> abcWithPNGModels, List<UnityDTX> unityDTXModels, string childFolderName)
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
        Directory.CreateDirectory(BSPMaterialPath);
        Directory.CreateDirectory(BSPMeshPath);
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

    private static void CombineMeshes(GameObject rootObject)
    {
        // Combine all meshes not named PhysicsBSP
        var meshFilters = rootObject.GetComponentsInChildren<MeshFilter>();
        foreach (var meshFilter in meshFilters)
        {
            if (meshFilter.transform.gameObject.name != "PhysicsBSP")
            {
                meshFilter.gameObject.MeshCombine(true);
            }
        }

        var gPhysicsBSP = GameObject.Find("PhysicsBSP");
        gPhysicsBSP.MeshCombine(true);

        //After mesh combine, we need to recalculate the normals
        meshFilters = gPhysicsBSP.GetComponentsInChildren<MeshFilter>();
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

    private static void AddColliders(GameObject rootObject)
    {
        // Assign the mesh collider to the combined meshes
        foreach (var meshFilter in rootObject.GetComponentsInChildren<MeshFilter>())
        {
            if (meshFilter.gameObject == rootObject)
            {
                continue;
            }

            var meshCollider = meshFilter.transform.gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
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

    private static bool IsVolume(BSPModel bspModel)
    {
        if (bspModel.WorldName.Contains("terrain", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return (
            bspModel.TextureNames[0].Contains("AI.dtx", StringComparison.OrdinalIgnoreCase) ||
            bspModel.TextureNames[0].Contains("sound.dtx", StringComparison.OrdinalIgnoreCase) ||
            bspModel.WorldName.Contains("volume", StringComparison.OrdinalIgnoreCase) ||
            bspModel.WorldName.Contains("water", StringComparison.OrdinalIgnoreCase) ||
            bspModel.WorldName.Contains("weather", StringComparison.OrdinalIgnoreCase) ||
            bspModel.WorldName.Contains("rain", StringComparison.OrdinalIgnoreCase) ||
            bspModel.WorldName.Contains("poison", StringComparison.OrdinalIgnoreCase) ||
            bspModel.WorldName.Contains("corrosive", StringComparison.OrdinalIgnoreCase) ||
            bspModel.WorldName.Contains("ladder", StringComparison.OrdinalIgnoreCase));
    }

    private static void SetTag(GameObject bspObject, BSPModel bspModel)
    {
        if (bspModel.WorldName == "PhysicsBSP")
        {
            bspObject.tag = LithtechTags.PhysicsBSP;
        }
        else if (IsVolume(bspModel))
        {
            bspObject.tag = LithtechTags.Volumes;
        }
        else if (bspModel.WorldName.Contains("AITrk", StringComparison.OrdinalIgnoreCase))
        {
            bspObject.tag = LithtechTags.AITrack;
        }
        else if (bspModel.WorldName.Contains("AIBarrier", StringComparison.OrdinalIgnoreCase))
        {
            bspObject.tag = LithtechTags.AIBarrier;
        }
        else if (bspObject.GetComponent<MeshFilter>() == null)
        {
            bspObject.tag = LithtechTags.MiscInvisible;
        }
    }

    private static bool IsTextureInvisible(string textureName, bool includeGlobaOpsNames, bool isSky, bool isInvisible)
    {
        if (isInvisible)
        {
            return true;
        }

        if (isSky)
        {
            return true;
        }

        if (string.IsNullOrEmpty(textureName))
        {
            return true;
        }

        if (textureName.Contains("invisible", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (textureName.Contains("Invisible.dtx", StringComparison.OrdinalIgnoreCase)
               || textureName.Contains("Sky.dtx", StringComparison.OrdinalIgnoreCase)
               || textureName.Contains("Rain.dtx", StringComparison.OrdinalIgnoreCase)
               || textureName.Contains("hull.dtx", StringComparison.OrdinalIgnoreCase)
               || textureName.Contains("occluder.dtx", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (includeGlobaOpsNames)
        {
            if (textureName.Contains("sector.dtx", StringComparison.OrdinalIgnoreCase)
               || textureName.Contains("Sound_Environment.dtx", StringComparison.OrdinalIgnoreCase)
               || textureName.Contains("Useable.dtx", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static WorldSurfaceModel GetSurface(BSPModel bspModel, WorldPolyModel poly, string datName, int polyIndex)
    {
        if (bspModel.TextureNames == null || bspModel.TextureNames.Count == 0)
        {
            Debug.Log($"Error creating BSP for DAT {datName}, BSP WorldName {bspModel.WorldName}. No textures found on poly {polyIndex}");
            return null;
        }

        if (poly.SurfaceIndex > bspModel.Surfaces.Count - 1)
        {
            Debug.Log($"Error creating BSP for DAT {datName}, BSP WorldName {bspModel.WorldName}. Invalid surface index on poly {polyIndex}");
            return null;
        }

        var surface = bspModel.Surfaces[poly.SurfaceIndex];
        if (surface == null)
        {
            Debug.Log($"Error creating BSP for DAT {datName}, BSP WorldName {bspModel.WorldName}. Could not find surface index on poly {polyIndex}");
            return null;
        }

        if (surface.TextureIndex > bspModel.TextureNames.Count - 1)
        {
            Debug.Log($"Error creating BSP for DAT {datName}, BSP WorldName {bspModel.WorldName}. Invalid texture index on poly {polyIndex}");
            return null;
        }

        if (string.IsNullOrEmpty(bspModel.TextureNames[surface.TextureIndex]))
        {
            Debug.Log($"Error creating BSP for DAT {datName}, BSP WorldName {bspModel.WorldName}. Texture name missing or invalid on poly {polyIndex}");
            return null;
        }

        return surface;
    }

    private static void CreateChildMeshes(WorldPolyModel poly, BSPModel bspModel, int index, Material material, WorldSurfaceModel surface, Transform parentTransform, TextureSize textureSize)
    {
        // Convert OPQ to UV magic
        Vector3 center = poly.Center;
        Vector3 o = surface.UV1;
        Vector3 p = surface.UV2;
        Vector3 q = surface.UV3;

        o *= UnityScaleFactor;
        o -= (Vector3)poly.Center;
        p /= UnityScaleFactor;
        q /= UnityScaleFactor;

        // CALCULATE EACH TRI INDIVIDUALLY.
        for (int nTriIndex = 0; nTriIndex < poly.LoVerts - 2; nTriIndex++)
        {
            Vector3[] vertexList = new Vector3[poly.LoVerts];
            Vector3[] vertexNormalList = new Vector3[poly.LoVerts];
            Color[] vertexColorList = new Color[poly.LoVerts];
            Vector2[] uvList = new Vector2[poly.LoVerts];
            int[] triangleIndices = new int[3];

            GameObject bspChildObject = new GameObject(bspModel.WorldName + index);
            bspChildObject.isStatic = true;
            bspChildObject.transform.parent = parentTransform;
            MeshRenderer meshRenderer = bspChildObject.AddComponent<MeshRenderer>();
            MeshFilter meshFilter = bspChildObject.AddComponent<MeshFilter>();

            Mesh mesh = new Mesh();
            for (int vertIndex = 0; vertIndex < poly.LoVerts; vertIndex++)
            {
                var vertex = bspModel.Vertices[(int)poly.VertexColorList[vertIndex].nVerts];

                Vector3 vertexData = vertex;
                vertexData *= UnityScaleFactor;
                vertexList[vertIndex] = vertexData;

                Color color = new Color(
                    poly.VertexColorList[vertIndex].red / 255,
                    poly.VertexColorList[vertIndex].green / 255,
                    poly.VertexColorList[vertIndex].blue / 255,
                    1.0f);
                vertexColorList[vertIndex] = color;
                vertexNormalList[vertIndex] = bspModel.Planes[poly.PlaneIndex].m_vNormal;

                // Calculate UV coordinates based on the OPQ vectors
                // Note that since the worlds are offset from 0,0,0 sometimes we need to subtract the center point
                Vector3 curVert = vertexList[vertIndex];
                float u = Vector3.Dot((curVert - center) - o, p);
                float v = Vector3.Dot((curVert - center) - o, q);

                //Scale back down into something more sane
                u /= textureSize.EngineWidth;
                v /= textureSize.EngineHeight;

                uvList[vertIndex] = new Vector2(u, v);
            }

            mesh.SetVertices(vertexList);
            mesh.SetNormals(vertexNormalList);
            mesh.SetUVs(0, uvList);
            mesh.SetColors(vertexColorList);

            // Hacky, whatever
            triangleIndices[0] = 0;
            triangleIndices[1] = nTriIndex + 1;
            triangleIndices[2] = (nTriIndex + 2) % poly.LoVerts;

            // Set triangles
            mesh.SetTriangles(triangleIndices, 0);
            mesh.RecalculateTangents();

            meshRenderer.sharedMaterial = material;
            meshFilter.sharedMesh = mesh;

            meshRenderer.shadowCastingMode = ShadowCastingMode.TwoSided;
        }
    }

    private static void CreateBSPChildMeshes(BSPModel bspModel, GameObject bspObject, List<PNGMap> pngMaps, List<UnityDTX> unityDTXModels)
    {
        if (bspModel.Surfaces == null || bspModel.Surfaces.Count == 0)
        {
            Debug.Log($"Error creating BSP for DAT {bspObject.name}, BSP WorldName {bspModel.WorldName}. No surfaces found.");
            return;
        }

        int polyIndex = -1;
        foreach (WorldPolyModel poly in bspModel.Polies)
        {
            polyIndex++;
            var surface = GetSurface(bspModel, poly, bspObject.name, polyIndex);
            if (surface == null)
            {
                continue;
            }

            var textureName = bspModel.TextureNames[surface.TextureIndex];
            bool isSky = (surface.Flags & (int)BitMask.SKY) == (int)BitMask.SKY;
            bool isTranslucent = (surface.Flags & (int)BitMask.TRANSLUCENT) == (int)BitMask.TRANSLUCENT;
            bool isInvisible = (surface.Flags & (int)BitMask.INVISIBLE) == (int)BitMask.INVISIBLE;

            if (IsTextureInvisible(textureName, false, isSky, isInvisible))
            {
                continue;
            }

            var pngMap = pngMaps.FirstOrDefault(x => x.LookupPath.Equals(textureName, StringComparison.OrdinalIgnoreCase));
            if (pngMap == null)
            {
                Debug.LogError("How did this happen???");
                continue;
            }

            Material material = pngMap.Material;
            TextureSize textureSize = GetTextureSize(textureName, unityDTXModels, material);

            // TODO: Create optional material with Chroma?
            // Check if the material needs to add the Chroma flag - which requires (possibly) creating a new Material based on the original texture.
            // May also update the tag of the mainObject
            // material = AddAndGetChromaMaterialIfNeeded(matReference, bspModel.WorldName, isTranslucent, isInvisible, mainObject);

            CreateChildMeshes(poly, bspModel, polyIndex, material, surface, bspObject.transform, textureSize);
        }
    }

    private static TextureSize GetTextureSize(string textureName, List<UnityDTX> unityDTXModels, Material material)
    {
        TextureSize textureSize = null;
        var unityDTXModel = unityDTXModels.FirstOrDefault(x => x.DTXModel.RelativePathToDTX.Equals(textureName, StringComparison.OrdinalIgnoreCase));
        if (unityDTXModel != null)
        {
            textureSize = unityDTXModel.TextureSize;
        }

        if (textureSize == null)
        {
            textureSize = new TextureSize
            {
                Width = material.mainTexture.width,
                Height = material.mainTexture.height,
                EngineWidth = material.mainTexture.width,
                EngineHeight = material.mainTexture.height
            };
        }

        return textureSize;
    }

    private static List<PNGMap> CreateMaterialsAndGetPNGMaps(List<DATModel> datModels, List<SPRModel> sprModels)
    {
        var uniqueTextures = datModels.SelectMany(datModel => datModel.BSPModels.SelectMany(bspModel => bspModel.TextureNames))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<PNGMap> pngMaps = new List<PNGMap>();
        foreach (var datTexturePath in uniqueTextures)
        {
            string realPathToPNG = GetRealPathToPNG(datTexturePath, sprModels);

            Material material = realPathToPNG == null
                ? DefaultMaterial
                : CreateBSPMaterial(datTexturePath, realPathToPNG);

            pngMaps.Add(new PNGMap
            {
                LookupPath = datTexturePath,
                RealPathToPNG = realPathToPNG,
                Material = material
            });
        }

        return pngMaps;
    }

    private static Material CreateBSPMaterial(string datTexturePath, string realPathToPNG)
    {
        string datTexturePathOnly = Path.GetDirectoryName(datTexturePath);
        string materialPathOnly = Path.Combine(BSPMaterialPath, datTexturePathOnly).ConvertFolderSeperators();
        string textureNameOnly = Path.GetFileNameWithoutExtension(datTexturePath);
        string pathToMaterial = Path.Combine(materialPathOnly, textureNameOnly + ".mat");
        Directory.CreateDirectory(Path.GetDirectoryName(pathToMaterial));

        // TODO Find matching DTX to get "Full Bright" from the header?
        //var unityDTX = unityDTXModels.FirstOrDefault(x => x.RelativePathToDTX != null && x.RelativePathToDTX.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
        bool useFullBright = false; // unityDTX == null ? false : unityDTX.Header.UseFullBright;

        // TODO Check how we might know about Chroma Key
        bool useChromaKey = false;

        Texture2D texture2d = AssetDatabase.LoadAssetAtPath<Texture2D>(realPathToPNG);

        // TODO: Check if there's a material named "skinName" in the directory first. If so, add a number until you get unique.
        var material = DTXConverter.CreateDefaultMaterial(textureNameOnly, texture2d, useFullBright, useChromaKey);

        AssetDatabase.CreateAsset(material, pathToMaterial);

        return material;
    }

    private static string GetRealPathToPNG(string datTexturePath, List<SPRModel> sprModels)
    {
        string realPathToPNG = null;
        string extension = Path.GetExtension(datTexturePath).ToLower();
        if (extension == ".dtx")
        {
            realPathToPNG = Path.ChangeExtension(Path.Combine(TexturePath, datTexturePath), "png");
            if (!File.Exists(realPathToPNG))
            {
                realPathToPNG = null;
            }
        }
        else if (extension == ".spr")
        {
            var matchingSprite = sprModels.FirstOrDefault(
                x => x.RelativePathToSprite.Equals(datTexturePath, StringComparison.OrdinalIgnoreCase));
            if (matchingSprite == null || matchingSprite.DTXPaths == null || matchingSprite.DTXPaths.Length == 0)
            {
                realPathToPNG = null;
            }
            else
            {
                realPathToPNG = Path.ChangeExtension(Path.Combine(TexturePath, matchingSprite.DTXPaths[0]), "png");
                if (!File.Exists(realPathToPNG))
                {
                    realPathToPNG = null;
                }
            }
        }

        return realPathToPNG;
    }

    private static void CreateBSPMeshAndPrefab(string name, GameObject go)
    {
        // Save mesh
        var meshFilter = go.GetComponent<MeshFilter>();
        var mesh = meshFilter.sharedMesh;
        string meshPathAndFilename = Path.Combine(BSPMeshPath, name + ".asset");
        AssetDatabase.CreateAsset(mesh, meshPathAndFilename);

        // Save prefab
        string prefabPathAndFilename = Path.Combine(BSPPrefabPath, name + ".prefab");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPathAndFilename);
    }

    private static void CombineMeshesPreserveMaterials(string datName, GameObject parent)
    {
        var meshFilters = parent.GetComponentsInChildren<MeshFilter>(includeInactive: true);

        // Group by material
        Dictionary<Material, List<CombineInstance>> materialToCombine = new();

        foreach (var meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null || meshFilter.gameObject == parent)
            {
                continue;
            }

            var renderer = meshFilter.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterials.Length != 1)
            {
                Debug.LogWarning($"Skipping {meshFilter.name} — requires exactly one material.");
                continue;
            }

            Material mat = renderer.sharedMaterial;

            if (!materialToCombine.ContainsKey(mat))
            {
                materialToCombine[mat] = new List<CombineInstance>();
            }

            CombineInstance combineInstance = new CombineInstance
            {
                mesh = meshFilter.sharedMesh,
                transform = meshFilter.transform.localToWorldMatrix
            };

            materialToCombine[mat].Add(combineInstance);

            DestroyImmediate(meshFilter.gameObject);
        }

        // Combine all groups into a single mesh with multiple submeshes
        List<CombineInstance> finalCombine = new();
        List<Material> finalMaterials = new();

        foreach (var kvp in materialToCombine)
        {
            Mesh subMesh = new Mesh();
            subMesh.CombineMeshes(kvp.Value.ToArray(), true, true);

            CombineInstance ci = new CombineInstance
            {
                mesh = subMesh,
                transform = Matrix4x4.identity
            };

            finalCombine.Add(ci);
            finalMaterials.Add(kvp.Key);
        }

        Mesh finalMesh = new Mesh();
        finalMesh.name = $"{datName}-{parent.name}";
        finalMesh.CombineMeshes(finalCombine.ToArray(), false, false);

        var parentMeshFilter = parent.GetComponent<MeshFilter>();
        var parentMeshRenderer = parent.GetComponent<MeshRenderer>();
        if (finalMesh.subMeshCount == 0)
        {
            if (parentMeshFilter != null)
            {
                DestroyImmediate(parentMeshFilter);
            }

            if (parentMeshRenderer != null)
            {
                DestroyImmediate(parentMeshRenderer);
            }
        }
        else
        {
            parentMeshFilter ??= parent.AddComponent<MeshFilter>();
            parentMeshRenderer ??= parent.AddComponent<MeshRenderer>();

            // Apply to parent
            parentMeshFilter.sharedMesh = finalMesh;
            parentMeshRenderer.sharedMaterials = finalMaterials.ToArray();
        }
    }

    private static GameObject CreateBSPObject(string name, BSPModel bspModel, List<PNGMap> pngMaps, List<UnityDTX> unityDTXModels)
    {
        var bspObject = new GameObject(bspModel.WorldName);
        bspObject.isStatic = true;
        bspObject.AddComponent<MeshFilter>();
        bspObject.AddComponent<MeshRenderer>().sharedMaterial = DefaultMaterial;

        CreateBSPChildMeshes(bspModel, bspObject, pngMaps, unityDTXModels);
        CombineMeshesPreserveMaterials(name, bspObject);
        SetTag(bspObject, bspModel);

        return bspObject;
    }

    private static List<GameObject> CreateBSPObjects(GameObject parent, string name, DATModel datModel, List<PNGMap> pngMaps, List<UnityDTX> unityDTXModels)
    {
        int i = 0;
        var bspObjects = new List<GameObject>();
        foreach (var bspModel in datModel.BSPModels)
        {
            i++;
            float progress = (float)i / datModel.BSPModels.Count;
            EditorUtility.DisplayProgressBar($"Creating BSP objects for {name}", $"Item {i} of {datModel.BSPModels.Count}", progress);

            if (bspModel.WorldName.Contains("VisBSP", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GameObject bspObject = CreateBSPObject(name, bspModel, pngMaps, unityDTXModels);
            bspObjects.Add(bspObject);
        }

        EditorUtility.DisplayProgressBar($"Grouping final BSP objects for {name}", $"Item {i} of {datModel.BSPModels.Count}", 99.9f);

        var taggedObjects = bspObjects.GroupBy(x => x.tag);
        foreach (var taggedObject in taggedObjects)
        {
            GameObject tagObject = new GameObject(taggedObject.Key ?? "Untagged");
            tagObject.transform.parent = parent.transform;

            foreach (GameObject obj in taggedObject)
            {
                obj.transform.parent = tagObject.transform;
            }
        }

        AddColliders(parent);

        EditorUtility.ClearProgressBar();

        return bspObjects;
    }

    /// <summary>
    /// Create assets related to DAT models
    /// </summary>
    /// <param name="abcModels"></param>
    /// <param name="sprModels"></param>
    /// <param name="datModels"></param>
    /// <param name="unityDTXModels"></param>
    private static void CreateAssetsFromDATModels(List<ABCModel> abcModels, List<SPRModel> sprModels, List<DATModel> datModels, List<UnityDTX> unityDTXModels)
    {
        datModels = datModels.Where(x => x.Filename.Contains("RESCUEATTHERUINS") && !x.Filename.Contains("Copy")).ToList();

        List<PNGMap> pngMaps = CreateMaterialsAndGetPNGMaps(datModels, sprModels);

        foreach (var datModel in datModels)
        {
            string name = Path.GetFileNameWithoutExtension(datModel.Filename);
            Debug.Log($"Creating {name} from {datModel.Filename}");
            GameObject rootObject = new GameObject(name);
            var bspObjects = CreateBSPObjects(rootObject, name, datModel, pngMaps, unityDTXModels);
            

            // CreateBSPMeshAndPrefab(rootObject.name, rootObject);

            //DestroyImmediate(rootObject);
        }
    }

}
