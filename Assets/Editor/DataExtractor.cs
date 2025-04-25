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
    private static readonly float UnityScaleFactor = 0.02f;
    private static readonly float MoveToFloorRaycastDistance = 20f;

    private static readonly string DefaultMaterialPath = $"Assets/Materials/DefaultMaterial.mat";
    //private static readonly string ProjectFolder = "C:\\lomm\\data\\";
    private static readonly string ProjectFolder = @"C:\temp\LOMMConverted\OriginalUnrezzed\";

    private static readonly string GeneratedAssetsFolder = "Assets/GeneratedAssets";
    private static readonly string TexturePath = $"{GeneratedAssetsFolder}/Textures";
    private static readonly string MaterialPath = $"{GeneratedAssetsFolder}/Materials";

    private static readonly string ABCMeshPath = $"{GeneratedAssetsFolder}/Meshes/ABCModels";
    private static readonly string ABCPrefabPath = $"{GeneratedAssetsFolder}/Prefabs/ABCModels";

    private static readonly string BSPMeshPath = $"{GeneratedAssetsFolder}/Meshes/BSPModels";
    private static readonly string BSPPrefabPath = $"{GeneratedAssetsFolder}/Prefabs/BSPModels";
    private static readonly string ScenePath = $"{GeneratedAssetsFolder}/Scenes";

    private static Material DefaultMaterial { get; set; }

    [MenuItem("Tools/Test")]
    public static void Test()
    {
        //var datModel = DATModelReader.ReadDATModel(@"C:\LoMM\Data\Worlds\CubeWorld3.dat", @"C:\LoMM\Data", Game.LOMM);
        var datModel = DATModelReader.ReadDATModel(@"C:\LoMM\Data\Worlds\_RESCUEATTHERUINS.DAT", @"C:\LoMM\Data", Game.LOMM);
        var properties = datModel.WorldObjects.SelectMany(x => x.Properties);

        var boolProps = properties.Where(x => x.PropType == LTTypes.PropType.Bool).GroupBy(x => x.Name).ToList();

        var s = string.Empty;
        foreach (var boolProp in boolProps)
        {
            var trueCount = boolProp.Where(x => x.BoolValue == true).Count();
            var falseCount = boolProp.Where(x => x.BoolValue == false).Count();
            s += $"{boolProp.Key}: True={trueCount} | False={falseCount}\r\n";
        }

        Debug.Log(s);
    }

    [MenuItem("Tools/Generate All Assets")]
    public static void ExtractAll()
    {
        System.Diagnostics.Stopwatch totalWatch = System.Diagnostics.Stopwatch.StartNew();
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        string stats = "Beginning of extract all. Using project path: " + ProjectFolder + "\r\n";
        CreateDefaultMaterial();
        CreateGeneratedPaths();

        stats += watch.GetElapsedTime("Create default material and create initial paths\r\n");
        //if (GetVal()) { Debug.Log("Early out!"); return; }

        // Step 1 - Load everything from the ProjectFolder
        var unityDTXModels = GetAllUnityDTXModels();
        stats += watch.GetElapsedTime($"Loaded DTX models (count={unityDTXModels.Count})\r\n");
        var abcModels = GetABCModels();
        stats += watch.GetElapsedTime($"Loaded ABC models (count={abcModels.Count})\r\n");
        var sprModels = GetAllSPRModels();
        stats += watch.GetElapsedTime($"Loaded SPR models (count={sprModels.Count})\r\n");
        var datModels = GetAllDATModels();
        stats += watch.GetElapsedTime($"Loaded DAT models (count={datModels.Count})\r\n");


        var s = "Clouds1 properties\r\n";
        var CULTOFTHESPIDER = datModels.FirstOrDefault(dat => Path.GetFileNameWithoutExtension(dat.Filename) == "CULTOFTHESPIDER");
        var matches = CULTOFTHESPIDER.WorldObjects.Where(wo => wo.ObjectType == "DemoSkyWorldModel").ToList();
        foreach (var match in matches)
        {
            s += match.Name + Environment.NewLine;
            foreach (var prop in match.Properties)
            {
                s += "\t" + prop + Environment.NewLine;
            }
            s += "\tIndex as float=" + match.Index.ToString() + Environment.NewLine;
        }
        Debug.Log(s);


        //var s = "SkyBox properties\r\n";
        //var rescueDAT = datModels.FirstOrDefault(dat => Path.GetFileNameWithoutExtension(dat.Filename) == "_RESCUEATTHERUINS");
        //var blueWater = rescueDAT.WorldObjects.FirstOrDefault(wo => wo.Name == "SkyBox");
        //foreach (var prop in blueWater.Properties)
        //{
        //    s += prop + Environment.NewLine;
        //}
        //Debug.Log(s);


        //var materialLookups = CreateTexturesAndMaterials(unityDTXModels, sprModels);
        ////var materialLookups = GetTexturesAndMaterials(unityDTXModels, sprModels);
        //stats += watch.GetElapsedTime($"Created all textures and materials\r\n");

        //// Step 2 - Create assets related to ABC models:
        //var abcReferenceModels = CreateAssetsFromABCModels(abcModels, datModels, materialLookups);
        //stats += watch.GetElapsedTime($"Created all ABC models and prefabs\r\n");

        //// Step 3 - Create assets for BSPs (from DATs)
        //CreateAssetsFromDATModels(abcModels, datModels, materialLookups, abcReferenceModels);
        //stats += watch.GetElapsedTime($"Created all DAT models and prefabs\r\n");

        // Step 6 - Create meshes for BSPs (from DATs)

        // Step 7 - Create scene prefab with references to BSP mesh, models, lights, etc.

        //AssetDatabase.SaveAssets();
        stats += totalWatch.GetElapsedTime("Total Processing Time\r\n");
        stats += "Done!!";
        Debug.Log(stats);
    }

    /// <summary>
    /// Create assets related to ABC models - the meshes and prefabs.
    /// 
    /// If an ABC is referenced by 1 or more DAT files then the skins referenced there will be used.
    /// If more than one set of skins are referenced then multiple meshes and prefabs will be created.
    /// For example:        
    ///     DAT1 references model.ABC with Skins = "skins\a.dtx"
    ///     DAT1 references model.ABC with Skins = "skins\b.dtx"
    ///     DAT2 references model.ABC with Skins = "skins\xyz.dtx"
    ///     3 meshes and prefabs will be created. A number is added to the name of the model for each unique "Skins" value.
    ///         model1.abc
    ///         model2.abc
    ///         model3.abc
    /// </summary>
    /// <param name="abcModels"></param>
    /// <param name="datModels"></param>
    /// <param name="materialLookups"></param>
    private static ABCReferenceModels CreateAssetsFromABCModels(List<ABCModel> abcModels, List<DATModel> datModels, Dictionary<string, MaterialLookupModel> materialLookups)
    {
        // Step 2a - Find skins (textures) for ABC Models
        ABCReferenceModels abcReferenceModels = GetABCReferences(abcModels, datModels, materialLookups);

        AssetDatabase.StartAssetEditing();

        // Step 2c - Create meshes and prefabs.
        // Only creates a prefab if there's a material referencing the ABCFile.
        // Otherwise the mesh will be enough. Someone will have to apply a material later.
        List<GameObject> gameObjectsToDestroy = new List<GameObject>();
        gameObjectsToDestroy.AddRange(CreateABCPrefabs(abcReferenceModels.ABCWithSkinsModels, materialLookups));
        gameObjectsToDestroy.AddRange(CreateABCPrefabs(abcReferenceModels.ABCWithSameNameMaterialModels));
        gameObjectsToDestroy.AddRange(CreateABCPrefabs(abcReferenceModels.ABCModelsWithNoReferences));

        int itemsCreated = gameObjectsToDestroy.Count;
        foreach (var go in gameObjectsToDestroy)
        {
            DestroyImmediate(go);
        }

        RefreshAssetDatabase();

        return abcReferenceModels;
    }

    private static ABCReferenceModels GetABCReferences(List<ABCModel> abcModels, List<DATModel> datModels, Dictionary<string, MaterialLookupModel> materialLookups)
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
        var materialModelList = materialLookups.Select(x => x.Value).ToList();
        var abcModelsWithMatchingMaterial = abcWithoutSkinsModels.Select(
            abcModel => new ABCWithSameNameMaterialModel
            {
                ABCModel = abcModel,
                Material = materialModelList
                    .Where(x => x.Name.ToLower() == abcModel.Name.ToLower())
                    .Select(x => x.Material)
                    .FirstOrDefault()
            })
            .Where(x => x.Material != null)
            .ToList();

        var abcModelsWithNoReferences = abcWithoutSkinsModels.Where(
            abcWithoutSkinsModel => !abcModelsWithMatchingMaterial.Any(
                x => x.ABCModel.Name.ToLower() == abcWithoutSkinsModel.Name.ToLower()))
            .ToList();

        return new ABCReferenceModels
        {
            ABCWithSkinsModels = abcWithSkinsModels,
            ABCWithSameNameMaterialModels = abcModelsWithMatchingMaterial,
            ABCModelsWithNoReferences = abcModelsWithNoReferences
        };
    }

    private static void CreateDefaultMaterial()
    {
        DefaultMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
    }

    private static void CreateGeneratedPaths()
    {
        Directory.CreateDirectory(ABCMeshPath);
        Directory.CreateDirectory(TexturePath);
        Directory.CreateDirectory(MaterialPath);
        Directory.CreateDirectory(ABCPrefabPath);
        Directory.CreateDirectory(BSPMeshPath);
        Directory.CreateDirectory(BSPPrefabPath);
        Directory.CreateDirectory(ScenePath);
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

            var abcModel = ABCModelReader.ReadABCModel(abcFile, ProjectFolder);
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
            SPRModel model = SPRModelReader.ReadSPRModel(ProjectFolder, relativePath);
            if (model != null)
            {
                models.Add(model);
            }
        }

        EditorUtility.ClearProgressBar();

        return models;
    }

    private static List<UnityDTXModel> GetAllUnityDTXModels()
    {
        var files = Directory.GetFiles(ProjectFolder, "*.dtx", SearchOption.AllDirectories);
        var models = new List<UnityDTXModel>();
        int i = 0;
        foreach (var file in files)
        {
            i++;
            float progress = (float)i / files.Length;
            EditorUtility.DisplayProgressBar("Loading and processing DTX Textures", $"Item {i} of {files.Length}", progress);

            var dtxModel = DTXModelReader.ReadDTXModel(file, Path.GetRelativePath(ProjectFolder, file));
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

    static void RefreshAssetDatabase(bool stopAssetEditing = true)
    {
        EditorUtility.DisplayProgressBar("Refreshing Assets", "Please wait while Unity updates the asset database...", 0.5f);

        if (stopAssetEditing)
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();
    }

    private static Dictionary<string, MaterialLookupModel> CreateTextures(List<UnityDTXModel> unityDTXModels)
    {
        Dictionary<string, MaterialLookupModel> materialLookups = new Dictionary<string, MaterialLookupModel>(StringComparer.OrdinalIgnoreCase);

        AssetDatabase.StartAssetEditing();
        int i = 0;
        foreach (var unityDTXModel in unityDTXModels)
        {
            i++;
            float progress = (float)i / unityDTXModels.Count;
            EditorUtility.DisplayProgressBar("Creating Textures", $"Item {i} of {unityDTXModels.Count}", progress);

            // Create the PNG and get a few references to it.
            var materialLookup = CreatePNG(unityDTXModel);
            if (materialLookup != null)
            {
                materialLookups.Add(materialLookup.RelativeLookupPath, materialLookup);
            }
        }

        RefreshAssetDatabase();

        return materialLookups;
    }

    private static void CreateMaterials(Dictionary<string, MaterialLookupModel> materialLookups)
    {
        AssetDatabase.StartAssetEditing();

        int i = 0;
        foreach (var materialLookup in materialLookups.Values)
        {
            i++;
            float progress = (float)i / materialLookups.Values.Count;
            EditorUtility.DisplayProgressBar("Creating Materials", $"Item {i} of {materialLookups.Values.Count}", progress);

            Texture2D texture2d = AssetDatabase.LoadAssetAtPath<Texture2D>(materialLookup.PathToPNG);
            texture2d.alphaIsTransparency = true;

            bool useFullBright = materialLookup.DTXModel.DTXModel.Header.UseFullBright;

            Shader shader = Shader.Find(useFullBright ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            Material material = new Material(shader);
            material.name = materialLookup.Name;
            material.mainTexture = texture2d;
            material.SetFloat("_Smoothness", 0f);

            if (materialLookup.DTXModel.UseTransparency)
            {
                material.SetFloat("_Surface", 0f); // 0 = Opaque, 1 = Transparent
                material.SetOverrideTag("RenderType", "Opaque");
                material.SetFloat("_AlphaClip", 1f); // enable alpha clipping
                material.SetFloat("_Cutoff", 0.5f);  // default threshold
                material.EnableKeyword("_ALPHATEST_ON");

                //material.SetFloat("_BlendModePreserveSpecular", 0f);
                //material.SetFloat("_Surface", 1f); // 0 = Opaque, 1 = Transparent
                //material.SetOverrideTag("RenderType", "Transparent");
                //material.renderQueue = (int)RenderQueue.Transparent;

                //material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                //material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                //material.SetInt("_ZWrite", 0); // Turn off depth writing for transparency
            }

            //var material = DTXConverter.CreateDefaultMaterial(materialLookup.Name, texture2d, useFullBright, false);

            var pathToMaterial = Path.ChangeExtension(Path.Combine(MaterialPath, materialLookup.RelativeLookupPath), "mat").ConvertFolderSeperators();
            Directory.CreateDirectory(Path.GetDirectoryName(pathToMaterial));

            AssetDatabase.CreateAsset(material, pathToMaterial);
            materialLookup.Material = AssetDatabase.LoadAssetAtPath<Material>(pathToMaterial);
        }

        RefreshAssetDatabase();

        AssetDatabase.StartAssetEditing();
        i = 0;
        foreach (var materialLookup in materialLookups.Values)
        {
            i++;
            float progress = (float)i / materialLookups.Values.Count;
            EditorUtility.DisplayProgressBar("Refreshing Materials", $"Item {i} of {materialLookups.Values.Count}", progress);

            var pathToMaterial = Path.ChangeExtension(Path.Combine(MaterialPath, materialLookup.RelativeLookupPath), "mat").ConvertFolderSeperators();
            materialLookup.Material = AssetDatabase.LoadAssetAtPath<Material>(pathToMaterial);
        }

        RefreshAssetDatabase();
    }

    /// <summary>
    /// This adds a sprite "path" to the dictionary so it will return the first DTX it finds.
    /// </summary>
    /// <param name="materialLookups"></param>
    /// <param name="sprModels"></param>
    private static void AddSpritePathsToMaterials(Dictionary<string, MaterialLookupModel> materialLookups, List<SPRModel> sprModels)
    {
        foreach (var sprModel in sprModels)
        {
            if (sprModel.DTXPaths == null || sprModel.DTXPaths.Length == 0)
            {
                continue;
            }

            var firstDTX = sprModel.DTXPaths[0];
            if (materialLookups.ContainsKey(firstDTX))
            {
                // Add the sprite as a reference to the same material lookup
                var materialLookup = materialLookups[firstDTX];
                materialLookup.RelativeSpritePaths.Add(sprModel.RelativePathToSprite);
                materialLookups.Add(sprModel.RelativePathToSprite, materialLookup);
            }
        }
    }

    private static void SetMaterialAlpha(Dictionary<string, MaterialLookupModel> materialLookups)
    {
        var pathToPNGs = materialLookups.Values
            .Where(x => x.DTXModel.UseTransparency)
            .Select(x => x.PathToPNG)
            .Distinct()
            .ToList();

        foreach(var pathToPNG in pathToPNGs)
        {
            TextureImporter importer = AssetImporter.GetAtPath(pathToPNG) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.textureType = TextureImporterType.Default;
                importer.androidETC2FallbackOverride = AndroidETC2FallbackOverride.Quality16Bit;
                importer.SaveAndReimport();
            }
        }
    }

    private static Dictionary<string, MaterialLookupModel> CreateTexturesAndMaterials(List<UnityDTXModel> unityDTXModels, List<SPRModel> sprModels)
    {
        Dictionary<string, MaterialLookupModel> materialLookups = CreateTextures(unityDTXModels);

        //SetMaterialAlpha(materialLookups);

        CreateMaterials(materialLookups);

        AddSpritePathsToMaterials(materialLookups, sprModels);

        return materialLookups;
    }

    private static MaterialLookupModel CreatePNG(UnityDTXModel unityDTXModel)
    {
        try
        {
            byte[] pngData = unityDTXModel.Texture2D.EncodeToPNG();
            if (pngData == null || pngData.Length == 0)
            {
                if (ShowLogErrors)
                {
                    Debug.LogError($"Could not convert {unityDTXModel.DTXModel.RelativePathToDTX} to PNG!");
                }

                return null;
            }

            string texturePath = Path.GetDirectoryName(Path.Combine(TexturePath, unityDTXModel.DTXModel.RelativePathToDTX)).ConvertFolderSeperators();
            Directory.CreateDirectory(texturePath);

            string pngFilenameOnly = Path.GetFileNameWithoutExtension(unityDTXModel.DTXModel.RelativePathToDTX) + ".png";
            string pathToPNG = Path.Combine(texturePath, pngFilenameOnly);
            File.WriteAllBytes(pathToPNG, pngData);

            return new MaterialLookupModel
            {
                DTXModel = unityDTXModel,
                PathToPNG = pathToPNG,
                Name = Path.GetFileNameWithoutExtension(unityDTXModel.DTXModel.RelativePathToDTX),
                RelativeLookupPath = unityDTXModel.DTXModel.RelativePathToDTX.ConvertFolderSeperators()
            };
        }
        catch (Exception ex)
        {
            if (ShowLogErrors)
            {
                Debug.LogError($"Error creating texture for {(unityDTXModel == null ? "<null>" : unityDTXModel.DTXModel.RelativePathToDTX)}: {ex.Message}");
            }
        }

        return null;
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

    private static Mesh CreateMesh(PieceModel piece, float yVertOffset)
    {
        List<Mesh> individualMeshes = new List<Mesh>();

        var faces = piece.LODs[0].Faces;

        // only use the first LOD, we don't care about the rest.
        int faceIndex = 0;
        foreach (FaceModel face in faces)
        {
            Mesh faceMesh = new Mesh();
            List<Vector3> faceVertices = new List<Vector3>();
            List<Vector3> faceNormals = new List<Vector3>();
            List<Vector2> faceUV = new List<Vector2>();
            List<int> faceTriangles = new List<int>();

            foreach (FaceVertexModel faceVertex in face.Vertices)
            {
                int originalVertexIndex = faceVertex.VertexIndex;

                // Add vertices, normals, and UVs for the current face
                var vert = piece.LODs[0].Vertices[originalVertexIndex].Location * UnityScaleFactor;
                var offset = new Vector3(0, yVertOffset, 0);
                faceVertices.Add(vert - offset);
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

    private static GameObject CreateObjectFromABC(ABCModel abcModel, Material[] materials)
    {
        var rootObject = new GameObject(abcModel.Name);
        rootObject.transform.position = Vector3.zero;
        rootObject.transform.rotation = Quaternion.identity;
        rootObject.transform.localScale = Vector3.one;

        var lowestY = abcModel.Pieces.Min(piece => piece.LODs[0].Vertices.Min(vert => vert.Location.y)) * UnityScaleFactor;

        foreach (var piece in abcModel.Pieces)
        {
            GameObject modelInGameObject = new GameObject(piece.Name);
            modelInGameObject.transform.parent = rootObject.transform;
            var meshFilter = modelInGameObject.AddComponent<MeshFilter>();
            var meshRenderer = modelInGameObject.AddComponent<MeshRenderer>();
            
            meshFilter.sharedMesh = CreateMesh(piece, lowestY);
            meshFilter.sharedMesh.RecalculateBounds();

            meshRenderer.sharedMaterial = materials[piece.MaterialIndex];
        }

        rootObject.MeshCombine(true);
        rootObject.tag = LithtechTags.NoRayCast;

        return rootObject;
    }

    private static GameObject CreatePrefabFromABC(ABCModel abcModel, string nameSuffix, GameObject go, bool createPrefab)
    {
        // Save mesh
        var meshFilter = go.GetComponent<MeshFilter>();
        var mesh = meshFilter.sharedMesh;
        var relativePathToABC = Path.GetDirectoryName(abcModel.RelativePathToABCFileLowercase);
        string meshPathAndFilename = Path.Combine(ABCMeshPath, relativePathToABC, abcModel.Name + nameSuffix + ".asset");
        Directory.CreateDirectory(Path.GetDirectoryName(meshPathAndFilename));
        AssetDatabase.CreateAsset(mesh, meshPathAndFilename);
        
        if (createPrefab)
        {
            // Force the load of the newly created/referenced asset.
            AssetDatabase.ImportAsset(meshPathAndFilename);
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPathAndFilename);
            meshFilter.sharedMesh = mesh;

            // Save prefab
            string prefabPathAndFilename = Path.Combine(ABCPrefabPath, relativePathToABC, abcModel.Name + nameSuffix + ".prefab");
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPathAndFilename));
            PrefabUtility.SaveAsPrefabAsset(go, prefabPathAndFilename);
            AssetDatabase.ImportAsset(prefabPathAndFilename);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPathAndFilename);
        }

        return null;
    }

    private static Material[] GetMaterials(ABCModel abcModel, List<string> skins, Dictionary<string, MaterialLookupModel> materialLookups)
    {
        var materials = new Material[abcModel.GetMaxMaterialIndex() + 1];

        int skinCount = skins?.Count ?? 0;
        if (skinCount == 0)
        {
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = DefaultMaterial;
            }
        }
        else
        {
            for (int i = 0; i < materials.Length; i++)
            {
                string skin = i > skinCount - 1
                    ? skins[skinCount - 1]
                    : skins[i];

                if (materialLookups.TryGetValue(skin, out MaterialLookupModel materialLookup))
                {
                    materials[i] = materialLookup.Material;
                }
                else
                {
                    materials[i] = DefaultMaterial;
                }
            }
        }

        return materials;
    }

    private static Material[] GetMaterials(ABCModel abcModel, Material material)
    {
        var materials = new Material[abcModel.GetMaxMaterialIndex() + 1];

        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = material;
        }

        return materials;
    }

    private static List<GameObject> CreateABCPrefabs(List<ABCWithSkinModel> abcWithSkinsModels, Dictionary<string, MaterialLookupModel> materialLookups)
    {
        int i = 0;
        List<GameObject> gameObjects = new List<GameObject>();
        foreach (var abcWithSkinModel in abcWithSkinsModels)
        {
            i++;
            float progress = (float)i / abcWithSkinsModels.Count;
            EditorUtility.DisplayProgressBar("Creating ABC Prefabs with skins", $"Item {i} of {abcWithSkinsModels.Count}", progress);

            Material[] materials = GetMaterials(abcWithSkinModel.ABCModel, abcWithSkinModel.WorldObjectModel.SkinsLowercase, materialLookups);
            GameObject go = CreateObjectFromABC(abcWithSkinModel.ABCModel, materials);

            string nameSuffix = (abcWithSkinModel.UniqueIndex != 0
                ? abcWithSkinModel.UniqueIndex.ToString()
                : string.Empty);
            var prefab = CreatePrefabFromABC(abcWithSkinModel.ABCModel, nameSuffix, go, true);
            abcWithSkinModel.Prefab = prefab;
            gameObjects.Add(go);
        }

        EditorUtility.ClearProgressBar();

        return gameObjects;
    }

    private static List<GameObject> CreateABCPrefabs(List<ABCWithSameNameMaterialModel> abcWithSameNameMaterialModels)
    {
        int i = 0;
        List < GameObject > gameObjects = new List<GameObject>();
        foreach (var abcWithSameNameMaterialModel in abcWithSameNameMaterialModels)
        {
            i++;
            float progress = (float)i / abcWithSameNameMaterialModels.Count;
            EditorUtility.DisplayProgressBar("Creating ABC Prefabs with matching PNG", $"Item {i} of {abcWithSameNameMaterialModels.Count}", progress);

            Material[] materials = GetMaterials(abcWithSameNameMaterialModel.ABCModel, abcWithSameNameMaterialModel.Material);
            GameObject go = CreateObjectFromABC(abcWithSameNameMaterialModel.ABCModel, materials);
            var prefab = CreatePrefabFromABC(abcWithSameNameMaterialModel.ABCModel, string.Empty, go, true);
            abcWithSameNameMaterialModel.Prefab = prefab;
            gameObjects.Add(go);
        }

        EditorUtility.ClearProgressBar();

        return gameObjects;
    }

    private static List<GameObject> CreateABCPrefabs(List<ABCModel> abcModels)
    {
        int i = 0;
        List<GameObject> gameObjects = new List<GameObject>();
        foreach (var abcModel in abcModels)
        {
            i++;
            float progress = (float)i / abcModels.Count;
            EditorUtility.DisplayProgressBar("Creating ABC Assets (no materials/skins)", $"Item {i} of {abcModels.Count}", progress);

            Material[] materials = GetMaterials(abcModel, DefaultMaterial);
            GameObject go = CreateObjectFromABC(abcModel, materials);

            CreatePrefabFromABC(abcModel, string.Empty, go, false);
            gameObjects.Add(go);
        }

        EditorUtility.ClearProgressBar();

        return gameObjects;
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
        else
        {
            bspObject.tag = LithtechTags.NoRayCast;
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

    private static void CreateChildMeshes(WorldPolyModel poly, BSPModel bspModel, int index, Material material, WorldSurfaceModel surface, Transform parentTransform, TextureSizeModel textureSize)
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
                var vertex = bspModel.Vertices[(int)poly.VertexColorList[vertIndex].VertexCount];

                Vector3 vertexData = vertex;
                vertexData *= UnityScaleFactor;
                vertexList[vertIndex] = vertexData;

                Color color = new Color(
                    poly.VertexColorList[vertIndex].R / 255,
                    poly.VertexColorList[vertIndex].G / 255,
                    poly.VertexColorList[vertIndex].B / 255,
                    1.0f);
                vertexColorList[vertIndex] = color;
                vertexNormalList[vertIndex] = bspModel.Planes[poly.PlaneIndex].Normal;

                // Calculate UV coordinates based on the OPQ vectors
                // Note that since the worlds are offset from 0,0,0 sometimes we need to subtract the center point
                Vector3 curVert = vertexList[vertIndex];
                float u = Vector3.Dot((curVert - center) - o, p);
                float v = Vector3.Dot((curVert - center) - o, q);

                //Scale back down into something more sane
                u /= textureSize.EngineWidth;
                v /= textureSize.EngineHeight;

                var uv = new Vector2(u, v);
                // Flip UV upside down - difference between Lithtech and Unity systems.
                uv.y = 1f - uv.y;
                uvList[vertIndex] = uv;
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

    private static void CreateBSPChildMeshes(BSPModel bspModel, GameObject bspObject, Dictionary<string, MaterialLookupModel> materialLookups)
    {
        if (bspModel.Surfaces == null || bspModel.Surfaces.Count == 0)
        {
            Debug.Log($"Error creating BSP for DAT {bspObject.name}, BSP WorldName {bspModel.WorldName}. No surfaces found.");
            return;
        }

        int polyIndex = -1;
        List<string> missingTextures = new List<string>();
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

            Material material;
            TextureSizeModel textureSize;
            if (materialLookups.TryGetValue(textureName, out MaterialLookupModel materialLookup))
            {
                // TODO: Create optional material with Chroma?
                // Check if the material needs to add the Chroma flag - which requires (possibly) creating a new Material based on the original texture.
                // May also update the tag of the mainObject
                // material = AddAndGetChromaMaterialIfNeeded(matReference, bspModel.WorldName, isTranslucent, isInvisible, mainObject);
                material = materialLookup.Material;
                textureSize = materialLookup.DTXModel.TextureSize;
            }
            else
            {
                material = DefaultMaterial;
                textureSize = new TextureSizeModel
                {
                    Width = material.mainTexture.width,
                    EngineWidth = material.mainTexture.width,
                    Height = material.mainTexture.height,
                    EngineHeight = material.mainTexture.height
                };
                
                missingTextures.Add(textureName);
            }

            if (missingTextures.Count > 0)
            {
                missingTextures = missingTextures.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var s = "BSP with missing textures:\r\n";
                foreach(var missingTexture in missingTextures)
                {
                    s += $"\t{missingTexture}\r\n";
                }

                Debug.Log(s);
            }
            
            CreateChildMeshes(poly, bspModel, polyIndex, material, surface, bspObject.transform, textureSize);
        }
    }

    private static void CreateBSPMeshAndPrefab(string name, GameObject go, List<GameObject> bspObjects)
    {
        // Save meshes
        string baseMeshPath = Path.Combine(BSPMeshPath, name);
        Directory.CreateDirectory(baseMeshPath);
        foreach(var bspObject in bspObjects)
        {
            string meshPathAndFilename = Path.Combine(baseMeshPath, bspObject.name + ".asset");
            var meshFilter = bspObject.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                var mesh = meshFilter.sharedMesh;
                AssetDatabase.CreateAsset(mesh, meshPathAndFilename);
            }
        }

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

    private static GameObject CreateBSPObject(string name, BSPModel bspModel, Dictionary<string, MaterialLookupModel> materialLookups)
    {
        var bspObject = new GameObject(bspModel.WorldName);
        bspObject.isStatic = true;
        bspObject.AddComponent<MeshFilter>();
        bspObject.AddComponent<MeshRenderer>().sharedMaterial = DefaultMaterial;

        CreateBSPChildMeshes(bspModel, bspObject, materialLookups);
        CombineMeshesPreserveMaterials(name, bspObject);
        SetTag(bspObject, bspModel);

        return bspObject;
    }

    private static List<GameObject> CreateBSPObjects(GameObject parent, string name, DATModel datModel, Dictionary<string, MaterialLookupModel> materialLookups)
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

            GameObject bspObject = CreateBSPObject(name, bspModel, materialLookups);
            bspObjects.Add(bspObject);
        }

        EditorUtility.DisplayProgressBar($"Grouping final BSP objects for {name}", $"Item {i} of {datModel.BSPModels.Count}", 99.9f);

        GroupBSPObjectsByTag(bspObjects, parent);

        AddColliders(parent);

        EditorUtility.ClearProgressBar();

        return bspObjects;
    }

    private static void GroupBSPObjectsByTag(List<GameObject> bspObjects, GameObject parent)
    {
        var taggedObjects = bspObjects.GroupBy(x => x.tag);
        foreach (var taggedObject in taggedObjects)
        {
            GameObject tagObject = new GameObject(taggedObject.Key ?? "Untagged");
            tagObject.transform.parent = parent.transform;

            foreach (GameObject obj in taggedObject)
            {
                obj.transform.parent = tagObject.transform;
            }

            if (taggedObject.Key == LithtechTags.MiscInvisible || taggedObject.Key == LithtechTags.AIBarrier || taggedObject.Key == LithtechTags.AITrack)
            {
                tagObject.SetActive(false);
            }
        }
    }

    private static void ProcessWorldObject(WorldObjectModel obj)
    {
        Vector3 rot = new Vector3();
        String objectName = String.Empty;
        bool bInvisible = false;
        bool bChromakey = false;

        var tempObject = new GameObject(obj.Name);

        //var tempObject = Instantiate(importer.RuntimeGizmoPrefab, objectPos, objectRot);
        //tempObject.name = objectName + "_obj";
        //tempObject.transform.eulerAngles = rot;

        if (obj.Name == "WorldProperties")
        {
            // TODO - Store WorldProperties somewhere?
        }
        else if (obj.Name == "SoundFX" || obj.Name == "AmbientSound")
        {
            //// TODO : Create sound
            //AudioSource temp = tempObject.AddComponent<AudioSource>();
            //var volumeControl = tempObject.AddComponent<Volume2D>();

            //string szFilePath = String.Empty;

            //foreach (var subItem in obj.options)
            //{
            //    if (subItem.Key == "Sound" || subItem.Key == "Filename")
            //    {
            //        szFilePath = Path.Combine(importer.szProjectPath, subItem.Value.ToString());
            //    }

            //    if (subItem.Key == "Loop")
            //    {
            //        temp.loop = (bool)subItem.Value;
            //    }

            //    if (subItem.Key == "Ambient")
            //    {
            //        if ((bool)subItem.Value)
            //        {
            //            temp.spatialize = false;
            //        }
            //        else
            //        {
            //            temp.spatialize = true;
            //            temp.spatialBlend = 1.0f;
            //        }
            //    }

            //    if (subItem.Key == "Volume")
            //    {
            //        float vol = (UInt32)subItem.Value;
            //        temp.volume = vol / 100;
            //    }
            //    if (subItem.Key == "OuterRadius")
            //    {
            //        float vol = (float)subItem.Value;
            //        temp.maxDistance = vol / 75;

            //        volumeControl.audioSource = temp;
            //        volumeControl.listenerTransform = Camera.main.transform;
            //        volumeControl.maxDist = temp.maxDistance;
            //    }
            //}
            //StartCoroutine(LoadAndPlay(szFilePath, temp));
        }
        else if (obj.Name == "TranslucentWorldModel" ||
            obj.Name == "Electricity" ||
            obj.Name == "Door")
        {
            // TODO - Handle TranslucentWorldModel
            //string szObjectName = String.Empty;
            //foreach (var subItem in obj.options)
            //{
            //    if (subItem.Key == "Visible")
            //        bInvisible = (bool)subItem.Value;
            //    else if (subItem.Key == "Chromakey")
            //        bChromakey = (bool)subItem.Value;
            //    else if (subItem.Key == "Name")
            //        szObjectName = (String)subItem.Value;
            //}

            //var twm = tempObject.AddComponent<TranslucentWorldModel>();
            //twm.bChromakey = bChromakey;
            //twm.bVisible = bInvisible;
            //twm.szName = szObjectName;
        }
        else if (obj.Name == "Light")
        {
            // TODO - Create Lights
            ////find child gameobject named Icon
            //var icon = tempObject.transform.Find("Icon");
            //icon.GetComponent<MeshRenderer>().material.mainTexture = Resources.Load<Texture2D>("Gizmos/light");
            //icon.gameObject.tag = LithtechTags.NoRayCast;
            //icon.gameObject.layer = 7;

            //var light = tempObject.gameObject.AddComponent<Light>();
            //light.lightmapBakeType = LightmapBakeType.Baked;

            //foreach (var subItem in obj.options)
            //{
            //    if (subItem.Key == "LightRadius")
            //        light.range = (float)subItem.Value * 0.01f;

            //    else if (subItem.Key == "LightColor")
            //    {
            //        var vec = (LTVector)subItem.Value;
            //        Vector3 col = Vector3.Normalize(new Vector3(vec.X, vec.Y, vec.Z));
            //        light.color = new Color(col.x, col.y, col.z);
            //    }

            //    else if (subItem.Key == "BrightScale")
            //        light.intensity = (float)subItem.Value;
            //}
            //light.shadows = LightShadows.Soft;

            //Controller lightController = transform.GetComponent<Controller>();

            //foreach (var toggle in lightController.settingsToggleList)
            //{
            //    if (toggle.name == "Shadows")
            //    {
            //        if (toggle.isOn)
            //            light.shadows = LightShadows.Soft;
            //        else
            //            light.shadows = LightShadows.None;
            //    }
            //}
        }
        else if (obj.Name == "DirLight")
        {
            // TODO Create Directional Light
            ////find child gameobject named Icon
            //var icon = tempObject.transform.Find("Icon");
            //icon.GetComponent<MeshRenderer>().material.mainTexture = Resources.Load<Texture2D>("Gizmos/light");
            //icon.gameObject.tag = LithtechTags.NoRayCast;
            //icon.gameObject.layer = 7;
            //var light = tempObject.gameObject.AddComponent<Light>();

            //foreach (var subItem in obj.options)
            //{
            //    if (subItem.Key == "FOV")
            //    {
            //        light.innerSpotAngle = (float)subItem.Value;
            //        light.spotAngle = (float)subItem.Value;
            //    }

            //    else if (subItem.Key == "LightRadius")
            //        light.range = (float)subItem.Value * 0.01f;

            //    else if (subItem.Key == "InnerColor")
            //    {
            //        var vec = (LTVector)subItem.Value;
            //        Vector3 col = Vector3.Normalize(new Vector3(vec.X, vec.Y, vec.Z));
            //        light.color = new Color(col.x, col.y, col.z);
            //    }

            //    else if (subItem.Key == "BrightScale")
            //        light.intensity = (float)subItem.Value * 15;
            //}

            //light.shadows = LightShadows.Soft;
            //light.type = LightType.Spot;

            //Controller lightController = GetComponent<Controller>();

            //foreach (var toggle in lightController.settingsToggleList)
            //{
            //    if (toggle.name == "Shadows")
            //    {
            //        if (toggle.isOn)
            //            light.shadows = LightShadows.Soft;
            //        else
            //            light.shadows = LightShadows.None;
            //    }
            //}
        }
        else if (obj.Name == "StaticSunLight")
        {
            // TODO Create Static Sun Light.
            ////find child gameobject named Icon
            //var icon = tempObject.transform.Find("Icon");
            //icon.GetComponent<MeshRenderer>().material.mainTexture = Resources.Load<Texture2D>("Gizmos/light");
            //icon.gameObject.tag = LithtechTags.NoRayCast;
            //icon.gameObject.layer = 7;
            //var light = tempObject.gameObject.AddComponent<Light>();

            //foreach (var subItem in obj.options)
            //{
            //    if (subItem.Key == "InnerColor")
            //    {
            //        var vec = (LTVector)subItem.Value;
            //        Vector3 col = Vector3.Normalize(new Vector3(vec.X, vec.Y, vec.Z));
            //        light.color = new Color(col.x, col.y, col.z);
            //    }
            //    else if (subItem.Key == "BrightScale")
            //        light.intensity = (float)subItem.Value;
            //}

            //light.shadows = LightShadows.Soft;
            //light.type = LightType.Directional;

            //Controller lightController = GetComponent<Controller>();

            //foreach (var toggle in lightController.settingsToggleList)
            //{
            //    if (toggle.name == "Shadows")
            //    {
            //        if (toggle.isOn)
            //            light.shadows = LightShadows.Soft;
            //        else
            //            light.shadows = LightShadows.None;
            //    }
            //}
        }
        else if (obj.Name == "GameStartPoint")
        {
            // TODO Create GameStartPoint
            //int nCount = ModelDefinition.AVP2RandomCharacterGameStartPoint.Length;

            //int nRandom = UnityEngine.Random.Range(0, nCount);
            //string szName = ModelDefinition.AVP2RandomCharacterGameStartPoint[nRandom];

            //var temp = importer.CreateModelDefinition(szName, ModelType.Character, obj.options);
            //var hasGravity = obj.options.ContainsKey("Gravity") ? (bool)obj.options["Gravity"] : false;
            //var gos = modelToGameObject.LoadABC(temp, tempObject.transform, hasGravity);

            //if (gos != null)
            //{
            //    gos.transform.position = tempObject.transform.position;
            //    gos.transform.eulerAngles = rot;
            //    gos.tag = LithtechTags.NoRayCast;
            //}

            ////find child gameobject named Icon
            //var icon = tempObject.transform.Find("Icon");
            //icon.GetComponent<MeshRenderer>().material.mainTexture = Resources.Load<Texture2D>("Gizmos/gsp");
            //icon.gameObject.tag = LithtechTags.NoRayCast;
            //icon.gameObject.layer = 7;
        }
        else if (obj.Name == "WeaponItem")
        {
            // TODO Create this
            //string szName = "";

            //if (obj.options.ContainsKey("WeaponType"))
            //{
            //    szName = (string)obj.options["WeaponType"];
            //}

            ////abc.FromFile("Assets/Models/" + szName + ".abc", true);

            //var temp = importer.CreateModelDefinition(szName, ModelType.WeaponItem, obj.options);
            //var hasGravity = obj.options.ContainsKey("Gravity") ? (bool)obj.options["Gravity"] : false;
            //var gos = modelToGameObject.LoadABC(temp, tempObject.transform, hasGravity);

            //if (gos != null)
            //{
            //    gos.transform.position = tempObject.transform.position;
            //    gos.transform.eulerAngles = rot;
            //    gos.tag = LithtechTags.NoRayCast;
            //    gos.layer = 2;
            //}
        }
        else if (obj.Name == "PropType" || obj.Name == "CProp")
        {
            // TODO Create this
            //string szName = "";

            //if (obj.options.ContainsKey("Name"))
            //{
            //    szName = (string)obj.options["Name"];
            //}

            //var temp = importer.CreateModelDefinition(szName, ModelType.PropType, obj.options);
            //var hasGravity = obj.options.ContainsKey("Gravity") ? (bool)obj.options["Gravity"] : false;
            //var gos = modelToGameObject.LoadABC(temp, tempObject.transform, hasGravity);

            //if (gos != null)
            //{
            //    gos.transform.position = tempObject.transform.position;
            //    gos.transform.eulerAngles = rot;
            //    gos.tag = LithtechTags.NoRayCast;
            //}
        }
        else if (obj.Name == "Prop" ||
            obj.Name == "AmmoBox" ||
            obj.Name == "Beetle" ||

            //obj.objectName == "BodyProp" || // not implemented
            obj.Name == "Civilian" ||
            obj.Name == "Egg" ||
            obj.Name == "HackableLock" ||
            obj.Name == "Plant" ||
            obj.Name == "StoryObject" ||
            obj.Name == "MEMO" ||
            obj.Name == "PC" ||
            obj.Name == "PDA" ||
            obj.Name == "Striker" ||
            obj.Name == "TorchableLock" ||
            obj.Name == "Turret" ||
            obj.Name == "TreasureChest" ||
            obj.Name == "Candle" ||
            obj.Name == "CandleWall")
        {
            //string szName = "";

            //if (obj.options.ContainsKey("Name"))
            //{
            //    szName = (string)obj.options["Name"];
            //}

            //var temp = importer.CreateModelDefinition(szName, ModelType.Prop, obj.options);
            //var hasGravity = obj.options.ContainsKey("Gravity") ? (bool)obj.options["Gravity"] : false;
            //var gos = modelToGameObject.LoadABC(temp, tempObject.transform, hasGravity);

            //if (gos != null)
            //{
            //    gos.transform.position = tempObject.transform.position;
            //    gos.transform.eulerAngles = rot;
            //    gos.tag = LithtechTags.NoRayCast;

            //    if (obj.options.ContainsKey("Scale"))
            //    {
            //        float scale = (float)obj.options["Scale"];
            //        if (scale != 1f)
            //        {
            //            gos.transform.localScale = Vector3.one * scale;
            //        }
            //    }
            //}
        }
        else if (obj.Name == "Princess")
        {
            //string szName = "";

            //if (obj.options.ContainsKey("Name"))
            //{
            //    szName = (string)obj.options["Name"];
            //}

            //var temp = importer.CreateModelDefinition(szName, ModelType.Princess, obj.options);
            //var hasGravity = obj.options.ContainsKey("Gravity") ? (bool)obj.options["Gravity"] : false;
            //var gos = modelToGameObject.LoadABC(temp, tempObject.transform, hasGravity);

            //if (gos != null)
            //{
            //    gos.transform.position = tempObject.transform.position;
            //    gos.transform.eulerAngles = rot;
            //    gos.tag = LithtechTags.NoRayCast;

            //    if (obj.options.ContainsKey("Scale"))
            //    {
            //        float scale = (float)obj.options["Scale"];
            //        if (scale != 1f)
            //        {
            //            gos.transform.localScale = Vector3.one * scale;
            //        }
            //    }
            //}
        }
        else if (obj.Name == "Trigger")
        {
            //find child gameobject named Icon
            var icon = tempObject.transform.Find("Icon");
            icon.GetComponent<MeshRenderer>().material.mainTexture = Resources.Load<Texture2D>("Gizmos/trigger");
            icon.gameObject.tag = LithtechTags.NoRayCast;
            icon.gameObject.layer = 7;
        }

        // Generic Monster type - has a Filename but no skin
        else if (obj.options.ContainsKey("Filename"))
        {
            //string szName = "";

            //if (obj.options.ContainsKey("Name"))
            //{
            //    szName = (string)obj.options["Name"];
            //}

            //var temp = importer.CreateModelDefinition(szName, ModelType.Monster, obj.options);
            //var hasGravity = obj.options.ContainsKey("Gravity") ? (bool)obj.options["Gravity"] : false;
            //var gos = modelToGameObject.LoadABC(temp, tempObject.transform, hasGravity);

            //if (gos != null)
            //{
            //    gos.transform.position = tempObject.transform.position;
            //    gos.transform.eulerAngles = rot;
            //    gos.tag = LithtechTags.NoRayCast;

            //    if (obj.options.ContainsKey("Scale"))
            //    {
            //        float scale = (float)obj.options["Scale"];
            //        if (scale != 1f)
            //        {
            //            gos.transform.localScale = Vector3.one * scale;
            //        }
            //    }
            //}
        }

        var g = GameObject.Find("objects");
        tempObject.transform.SetParent(g.transform);

        g.transform.localScale = Vector3.one;
    }

    private static bool NameMatches(string valueToCheck, params string[] names)
    {
        return names.Any(x => x.Equals(valueToCheck, StringComparison.OrdinalIgnoreCase));
    }

    private static bool NameMatchesMonster(string valueToCheck)
    {
        return NameMatches(valueToCheck, 
            "Fish", "Bandit", "SkeletonWarrior", "Soldier", "Skeleton", "DragonFly", "Monk", "Troglodyte", "EvilEye", "Orc"
            , "Spider2", "Dagrell", "Lich", "Goblin", "EvilEyeTerror", "Dwarf", "Wight", "ArcherBot", "Gargoyle", "Basilisk", "Spider"
            , "Harpy", "Cow", "Goat", "LizardWarrior", "DruidBot", "Bird", "GolemStone", "Pig", "LichKing", "LizardMan", "Troll"
            , "PaladinBot", "Zombie", "DragonRed", "Titan", "Duck", "Mummy", "Hen", "Bat", "Gopher", "Rooster", "Nobleman"
            , "TitanGrand", "WarriorBot", "DwarfKing", "TownsFolkFemale", "TownsFolkGirl", "TownsFolkFemaleMid", "ElementalEarth", "Priest", "HereticBot");
    }

    private static WorldObjectTypes GetWorldObjectType(WorldObjectModel model)
    {
        if (NameMatches(model.ObjectType, "StaticSunLight", "GlowingLight", "DirLight", "ObjectLight"))
        {
            return WorldObjectTypes.Light;
        }

        if (NameMatches(model.ObjectType, "AIBarrier"))
        {
            return WorldObjectTypes.AIBarrier;
        }

        if (NameMatches(model.ObjectType, "AIRail"))
        {
            return WorldObjectTypes.AIRail;
        }

        if (NameMatches(model.ObjectType, "Prop", "WallTorch", "BagGold", "Brazier", "TreasureChest", "Torch", "CandleWall", "DestructableProp", "Candle", "Candelabra", "Chandelier", "PropDamager"))
        {
            return WorldObjectTypes.Prop;
        }

        if (NameMatches(model.ObjectType, "StartPoint"))
        {
            return WorldObjectTypes.StartPoint;
        }

        if (NameMatches(model.ObjectType, "AmbientSound", "Sound"))
        {
            return WorldObjectTypes.Sound;
        }

        if (NameMatches(model.ObjectType, "BuyZone"))
        {
            return WorldObjectTypes.BuyZone;
        }

        if (NameMatches(model.ObjectType, "RescueZone", "GoodKingRescueZone", "EvilKingRescueZone"))
        {
            return WorldObjectTypes.RescueZone;
        }

        if (NameMatches(model.ObjectType, "Teleporter", "PortalZone"))
        {
            return WorldObjectTypes.Teleporter;
        }

        if (NameMatches(model.ObjectType, "SwordInStone"))
        {
            return WorldObjectTypes.SwordInStone;
        }

        if (NameMatches(model.ObjectType, "WorldProperties"))
        {
            return WorldObjectTypes.WorldProperties;
        }

        if (NameMatches(model.ObjectType, "Princess"))
        {
            return WorldObjectTypes.Princess;
        }

        if (NameMatches(model.ObjectType, "SoftLandingZone"))
        {
            return WorldObjectTypes.SoftLandingZone;
        }

        if (NameMatches(model.ObjectType, "SpectatorStartPoint"))
        {
            return WorldObjectTypes.SpectatorStartPoint;
        }

        if (NameMatches(model.ObjectType, "EndlessFall"))
        {
            return WorldObjectTypes.EndlessFall;
        }

        if (NameMatches(model.ObjectType, "Fire", "BlueWater", "DirtyWater", "CorrosiveFluid", "ClearWater", "LiquidNitrogen", "ZeroGravity"))
        {
            return WorldObjectTypes.Volume;
        }

        if (NameMatchesMonster(model.ObjectType))
        {
            return WorldObjectTypes.Monster;
        }

        return WorldObjectTypes.Unknown;
    }

    private static void CreateABCPrefabFromWorldObject(GameObject prefab, WorldObjectModel worldObjectModel, GameObject rootWorldObject, string tag)
    {
        GameObject abcObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        abcObject.name = $"{worldObjectModel.Name} ({prefab.name})";
        abcObject.transform.parent = rootWorldObject.transform;
        abcObject.transform.position = new Vector3(worldObjectModel.Position.X, worldObjectModel.Position.Y, worldObjectModel.Position.Z) * UnityScaleFactor;
        abcObject.transform.eulerAngles = new Vector3(worldObjectModel.Rotation.X * Mathf.Rad2Deg, worldObjectModel.Rotation.Y * Mathf.Rad2Deg, worldObjectModel.Rotation.Z * Mathf.Rad2Deg);
        abcObject.tag = tag;
        if (worldObjectModel.Scale != default(float) && worldObjectModel.Scale != 1f)
        {
            abcObject.transform.localScale = Vector3.one * worldObjectModel.Scale;
        }

        if (worldObjectModel.MoveToFloor)
        {
            MoveDownToFloor(abcObject);
        }
    }

    private static void MoveObjectToGroundOrig(GameObject gameObject, RaycastHit hit)
    {
        //// calculate bounds of object so it doesnt fall through the floor
        //Bounds bounds = gameObject.GetComponent<Renderer>().bounds;
        //float halfHeight = bounds.extents.y;

        ////sometimes pivot point isnt in the middle of the object, so we need to compoensate for that
        //float pivotOffset = gameObject.transform.position.y - bounds.center.y;

        ////move object to hit point
        //gameObject.transform.position = new Vector3(gameObject.transform.position.x, hit.point.y + halfHeight + pivotOffset, gameObject.transform.position.z);
        gameObject.transform.position = new Vector3(gameObject.transform.position.x, hit.point.y, gameObject.transform.position.z);
    }

    private static void MoveDownToFloor(GameObject abcObject)
    {
        Vector3 epsilon = new Vector3(0, float.Epsilon, 0);
        var hits = Physics.RaycastAll(abcObject.transform.position + epsilon, Vector3.down, MoveToFloorRaycastDistance)
            .Where(x => x.collider.tag != LithtechTags.NoRayCast)
            .OrderBy(x => x.distance)
            .ToList();

        if (hits.Count > 0)
        {
            MoveObjectToGroundOrig(abcObject, hits[0]);
        }
    }

    private static void CreateWorldObjects(GameObject parent, string name, DATModel datModel, ABCReferenceModels abcReferenceModels)
    {
        // Create container object under the parent.
        // All WorldObjects will be created under this.
        GameObject rootWorldObject = new GameObject("WorldObjects");
        rootWorldObject.transform.parent = parent.transform;

        int i = 0;
        var s = $"Creating WorldObjects for {name}\r\n";
        foreach (var worldObjectModel in datModel.WorldObjects)
        {
            i++;
            float progress = (float)i / datModel.WorldObjects.Count;
            EditorUtility.DisplayProgressBar($"Creating World Objects for {name}", $"Item {i} of {datModel.WorldObjects.Count}", progress);

            var worldObjectType = GetWorldObjectType(worldObjectModel);
            if (worldObjectModel.IsABC)
            {
                if (worldObjectModel.SkinsLowercase.Count > 0)
                {
                    // Try to find match on ABC model that has a matching skin used by this DAT's world object.
                    // The ABC model might be "banner.abc" but have different skins/textures applied.
                    var matchingABCWithSkinsModel = abcReferenceModels.ABCWithSkinsModels.Where(x => x.ABCModel.RelativePathToABCFileLowercase == worldObjectModel.FilenameLowercase && x.WorldObjectModel.AllSkinsPathsLowercase == worldObjectModel.AllSkinsPathsLowercase).FirstOrDefault();
                    if (matchingABCWithSkinsModel != null)
                    {
                        CreateABCPrefabFromWorldObject(matchingABCWithSkinsModel.Prefab, worldObjectModel, rootWorldObject, LithtechTags.NoRayCast);
                    }
                    else
                    {
                        s += $"\tERROR - Object {worldObjectModel.Name} has ABCModel at path {worldObjectModel.Filename} but no matching skin for {worldObjectModel.SkinsLowercase}\r\n";
                    }
                }
                else
                {
                    // No Skins.
                    // Just match on filename to a model that found a same-named texture.
                    var matchingABCWithSameNameModel = abcReferenceModels.ABCWithSameNameMaterialModels.Where(x => x.ABCModel.RelativePathToABCFileLowercase == worldObjectModel.FilenameLowercase).FirstOrDefault();
                    if (matchingABCWithSameNameModel != null)
                    {
                        CreateABCPrefabFromWorldObject(matchingABCWithSameNameModel.Prefab, worldObjectModel, rootWorldObject, LithtechTags.NoRayCast);
                    }
                    else
                    {
                        s += $"\tERROR - Object {worldObjectModel.Name} has filename but no skin(s) - could instantiate mesh if we want?\r\n";
                    }
                }
            }

            //ProcessWorldObject(worldObjectModel);
        }

        Debug.Log(s);

        // SetupAmbientLight();

        EditorUtility.ClearProgressBar();
    }

    /// <summary>
    /// Create assets related to DAT models
    /// </summary>
    /// <param name="abcModels"></param>
    /// <param name="datModels"></param>
    /// <param name="materialLookups"></param>
    private static int CreateAssetsFromDATModels(List<ABCModel> abcModels, List<DATModel> datModels, Dictionary<string, MaterialLookupModel> materialLookups, ABCReferenceModels abcReferenceModels)
    {
        AssetDatabase.StartAssetEditing();

        List<GameObject> gameObjectsToDestroy = new List<GameObject>();
        foreach (var datModel in datModels)
        {
            string name = Path.GetFileNameWithoutExtension(datModel.Filename);
            if (name != "_RESCUEATTHERUINS")
            {
                continue;
            }

            //Debug.Log($"Creating {name} from {datModel.Filename}");
            GameObject rootObject = new GameObject(name);
            var bspObjects = CreateBSPObjects(rootObject, name, datModel, materialLookups);
            CreateWorldObjects(rootObject, name, datModel, abcReferenceModels);

            CreateBSPMeshAndPrefab(rootObject.name, rootObject, bspObjects);

            gameObjectsToDestroy.Add(rootObject);
        }

        foreach (var go in gameObjectsToDestroy)
        {
            DestroyImmediate(go);
        }

        RefreshAssetDatabase();

        return datModels.Count;
    }
}
