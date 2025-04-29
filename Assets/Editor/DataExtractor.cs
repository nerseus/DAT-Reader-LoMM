using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using Utility;
using UnityEngine.Rendering;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

public class DataExtractor : EditorWindow
{
    public static readonly bool BottomAlignABCModels = false;

    public static readonly bool ShowLogErrors = false;
    public static readonly float UnityScaleFactor = 0.02f;
    // World Objects should be shifted down 1 unit.
    // Since we scale things by UnityScaleFactor, we can just use that directly to get the offset.
    public static readonly Vector3 WorldObjectOffset = new Vector3(0, -UnityScaleFactor, 0); 
    public static readonly float MoveToFloorRaycastDistance = 20f;

    public static readonly string MissingMaterialPath = $"Assets/Defaults/MissingMaterial.mat";
    public static readonly string InvisibleMaterialPath = $"Assets/Defaults/InvisibleMaterial.mat";
    public static readonly string CustomInvisibleMaterialPath = $"Assets/Defaults/CustomInvisibleMaterial.mat";

    //public static readonly string ProjectFolder = "C:\\lomm\\data\\";
    public static readonly string ProjectFolder = @"C:\temp\LOMMConverted\OriginalUnrezzed\";

    public static readonly string GeneratedAssetsFolder = "Assets/GeneratedAssets";
    public static readonly string TexturePath = $"{GeneratedAssetsFolder}/Textures";
    public static readonly string MaterialPath = $"{GeneratedAssetsFolder}/Materials";

    public static readonly string ABCMeshPath = $"{GeneratedAssetsFolder}/Meshes/ABCModels";
    public static readonly string ABCPrefabPath = $"{GeneratedAssetsFolder}/Prefabs/ABCModels";

    public static readonly string BSPMeshPath = $"{GeneratedAssetsFolder}/Meshes/BSPModels";
    public static readonly string BSPPrefabPath = $"{GeneratedAssetsFolder}/Prefabs/BSPModels";

    public static Material MissingMaterial { get; set; }
    public static Material InvisibleMaterial { get; set; }
    public static Material CustomInvisibleMaterial { get; set; }

    [MenuItem("Tools/Generate All Assets (fast)")]
    public static void ExtractAllFast()
    {
        ExtractAll(false);
    }

    [MenuItem("Tools/Generate All Assets (slow - recreate)")]
    public static void ExtractAllSlow()
    {
        ExtractAll(true);
    }

    public static void ExtractAll(bool alwaysCreate)
    {
        System.Diagnostics.Stopwatch totalWatch = System.Diagnostics.Stopwatch.StartNew();
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        string stats = "Beginning of extract all. Using project path: " + ProjectFolder + "\r\n";
        if (!CreateDefaultMaterials())
        {
            return;
        }
        stats += watch.GetElapsedTime("CreateDefaultMaterials\r\n", 1);

        CreateGeneratedPaths();
        stats += watch.GetElapsedTime("CreateGeneratedPaths\r\n", 1);

        TextureLookupUtility.SetLookups(alwaysCreate); stats += watch.GetElapsedTime("TextureLookupUtility.SetLookups\r\n", 1);
        var datModels = GetAllDATModels(); stats += watch.GetElapsedTime("GetAllDATModels\r\n", 1);
        var sprModels = GetAllSPRModels(); stats += watch.GetElapsedTime("GetAllSPRModels\r\n", 1);
        MaterialLookupUtility.SetLookups(alwaysCreate, datModels, sprModels); stats += watch.GetElapsedTime("MaterialLookupUtility.SetLookups\r\n", 1);

        var abcModels = GetABCModels();
        ABCMeshLookupUtility.SetLookups(alwaysCreate, abcModels); stats += watch.GetElapsedTime("ABCMeshLookupUtility.SetLookups\r\n", 1);
        ABCPrefabLookupUtility.SetLookups(alwaysCreate, datModels, abcModels); stats += watch.GetElapsedTime("ABCLookupUtility.SetLookups\r\n", 1);
        CreateAssetsFromDATModels(datModels); stats += watch.GetElapsedTime("CreateAssetsFromDATModels\r\n", 1);

        //stats += watch.GetElapsedTime($"Created all DAT models and prefabs\r\n");

        // Step 6 - Create meshes for BSPs (from DATs)

        // Step 7 - Create scene prefab with references to BSP mesh, models, lights, etc.

        //AssetDatabase.SaveAssets();
        stats += totalWatch.GetElapsedTime("Total Processing Time\r\n");
        Debug.Log(stats);
    }

    private static bool CreateDefaultMaterials()
    {
        MissingMaterial = AssetDatabase.LoadAssetAtPath<Material>(MissingMaterialPath);
        InvisibleMaterial = AssetDatabase.LoadAssetAtPath<Material>(InvisibleMaterialPath);
        CustomInvisibleMaterial = AssetDatabase.LoadAssetAtPath<Material>(CustomInvisibleMaterialPath);

        return MissingMaterial != null && InvisibleMaterial != null && CustomInvisibleMaterial != null;
    }

    private static void CreateGeneratedPaths()
    {
        Directory.CreateDirectory(ABCMeshPath);
        Directory.CreateDirectory(TexturePath);
        Directory.CreateDirectory(MaterialPath);
        Directory.CreateDirectory(ABCPrefabPath);
        Directory.CreateDirectory(BSPMeshPath);
        Directory.CreateDirectory(BSPPrefabPath);
    }

    protected static List<ABCModel> GetABCModels()
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

    protected static List<SPRModel> GetAllSPRModels()
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

    protected static List<UnityDTXModel> GetAllUnityDTXModels()
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

    protected static List<DATModel> GetAllDATModels()
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

    protected static void RefreshAssetDatabase(bool stopAssetEditing = true)
    {
        EditorUtility.DisplayProgressBar("Refreshing Assets", "Please wait while Unity updates the asset database...", 0.5f);
        System.Threading.Thread.Sleep(50);
        if (stopAssetEditing)
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
        System.Threading.Thread.Sleep(50);
        EditorUtility.ClearProgressBar();
    }

    private static void AddColliders(GameObject rootObject)
    {
        // Assign the mesh collider to the combined meshes
        foreach (var meshFilter in rootObject.GetComponentsInChildren<MeshFilter>())
        {
            var meshCollider = meshFilter.transform.gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
        }
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

    private static bool IsTextureCustomInvisible(string textureName, bool isSky, bool showSurface, bool visible)
    {
        if (isSky || (!showSurface && !visible))
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

    private static void CreateChildMeshes(WorldPolyModel poly, BSPModel bspModel, int index, Material material, WorldSurfaceModel surface, Transform parentTransform)
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
                u /= material.mainTexture.width;
                v /= material.mainTexture.height;

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

    private static GameObject CreateGameObject(string name, GameObject parent)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.parent = parent.transform;
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        return gameObject;
    }

    private static bool CreateBSPChildMeshes(BSPModel bspModel, GameObject bspObject, WorldObjectModel worldObjectModel)
    {
        if (bspModel.Surfaces == null || bspModel.Surfaces.Count == 0)
        {
            Debug.Log($"Error creating BSP for DAT {bspObject.name}, BSP WorldName {bspModel.WorldName}. No surfaces found.");
            return false;
        }

        float? surfaceAlpha = (worldObjectModel == null || worldObjectModel.WorldObjectType != WorldObjectTypes.VisibleVolume)
            ? null
            : worldObjectModel.SurfaceAlpha;

        bool visible = worldObjectModel == null ? true : worldObjectModel.Visible;
        bool showSurface = worldObjectModel == null ? true : worldObjectModel.ShowSurface || visible;

        int polyIndex = -1;
        List<string> missingTextures = new List<string>();
        bool isPhysicsBSP = bspModel.WorldName == "PhysicsBSP";
        GameObject missingMaterialGameObject = isPhysicsBSP ? CreateGameObject("MissingMaterial", bspObject) : null;
        GameObject invisibleMaterialGameObject = isPhysicsBSP ? CreateGameObject("InvisibleMaterial", bspObject) : null;
        GameObject customInvisibleMaterialGameObject = isPhysicsBSP ? CreateGameObject("CustomInvisible", bspObject) : null;
        GameObject physicsBSPGameObject = isPhysicsBSP ? CreateGameObject("PhysicsBSP", bspObject) : null;
        foreach (WorldPolyModel poly in bspModel.Polies)
        {
            polyIndex++;
            var surface = GetSurface(bspModel, poly, bspObject.name, polyIndex);
            if (surface == null)
            {
                continue;
            }

            var textureName = bspModel.TextureNames[surface.TextureIndex];
            Material material = GetMaterial(textureName, surface, surfaceAlpha, showSurface, visible);

            if (material == MissingMaterial)
            {
                missingTextures.Add(textureName);
            }

            GameObject parentObject;
            if (!isPhysicsBSP)
            {
                parentObject = bspObject;
            }
            else
            {
                if (material == MissingMaterial)
                {
                    parentObject = missingMaterialGameObject;
                }
                else if (material == InvisibleMaterial)
                {
                    parentObject = invisibleMaterialGameObject;
                }
                else if (material == CustomInvisibleMaterial)
                {
                    parentObject = customInvisibleMaterialGameObject;
                }
                else
                {
                    parentObject = physicsBSPGameObject;
                }
            }

            CreateChildMeshes(poly, bspModel, polyIndex, material, surface, parentObject.transform);
        }

        if (missingTextures.Count > 0)
        {
            missingTextures = missingTextures.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var s = $"BSP ({bspModel.WorldName}) has missing textures:\r\n";
            foreach (var missingTexture in missingTextures)
            {
                s += $"\t{missingTexture}\r\n";
            }

            Debug.Log(s);
        }

        return isPhysicsBSP;
    }

    private static Material GetMaterial(string textureName, WorldSurfaceModel surface, float? surfaceAlpha, bool showSurface, bool visible)
    {
        bool isInvisibleFlag = (surface.Flags & (int)BitMask.INVISIBLE) == (int)BitMask.INVISIBLE;
        if (isInvisibleFlag)
        {
            return InvisibleMaterial;
        }

        if (string.IsNullOrEmpty(textureName))
        {
            return MissingMaterial;
        }

        bool isSky = (surface.Flags & (int)BitMask.SKY) == (int)BitMask.SKY;
        if (IsTextureCustomInvisible(textureName, isSky, showSurface, visible))
        {
            return CustomInvisibleMaterial;
        }

        if (surfaceAlpha.HasValue)
        {
            var surfaceAlphaMaterial = UnityLookups.GetMaterial(textureName, surfaceAlpha.Value);
            if (surfaceAlphaMaterial != null)
            {
                return surfaceAlphaMaterial;
            }
        }

        var material = UnityLookups.GetMaterial(textureName);
        if (material != null)
        {
            return material;
        }

        return MissingMaterial;
    }

    private static void CreateBSPMeshAndPrefab(GameObject datRootObject, List<GameObject> bspObjects)
    {
        // Save meshes
        string baseMeshPath = Path.Combine(BSPMeshPath, datRootObject.name);
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
        string prefabPathAndFilename = Path.Combine(BSPPrefabPath, datRootObject.name + ".prefab");
        var prefab = PrefabUtility.SaveAsPrefabAsset(datRootObject, prefabPathAndFilename);
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

            meshFilter.gameObject.hideFlags = HideFlags.HideAndDontSave;
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
                parentMeshFilter.hideFlags = HideFlags.HideAndDontSave;
                DestroyImmediate(parentMeshFilter);
            }

            if (parentMeshRenderer != null)
            {
                parentMeshRenderer.hideFlags = HideFlags.HideAndDontSave;
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

    private static List<GameObject> CreateBSPObject(string datName, BSPModel bspModel, List<WorldObjectModel> worldObjectModels, Transform parentTransform)
    {
        var matchingWorldObjectModel = worldObjectModels.FirstOrDefault(x => x.Name == bspModel.WorldName);
        if (bspModel.WorldName != "PhysicsBSP" && matchingWorldObjectModel == null)
        {
            Debug.LogError($"BSP has no matching WorldObject: {bspModel.WorldName}");
        }

        var name = bspModel.WorldName;
        var bspObject = new GameObject(name);
        bspObject.transform.parent = parentTransform;
        var bspComponent = bspObject.AddComponent<BSPObjectComponent>();
        bspComponent.WorldObjectName = bspModel.WorldName;
        bspComponent.WorldObjectType = matchingWorldObjectModel == null ? WorldObjectTypes.Unknown : matchingWorldObjectModel.WorldObjectType;
        bspObject.isStatic = true;
        bspObject.AddComponent<MeshFilter>();
        bspObject.AddComponent<MeshRenderer>().sharedMaterial = MissingMaterial;

        List<GameObject> gameObjects = new List<GameObject>();
        if (CreateBSPChildMeshes(bspModel, bspObject, matchingWorldObjectModel))
        {
            // When this is PhysicsBSP, break into child groupings.
            var children = GetChildren(bspObject);
            Debug.Log($"PhysicsBSP: Found {children.Count} children");
            foreach (var child in children)
            {
                CombineMeshesPreserveMaterials(datName, child);
            }
            gameObjects.AddRange(children);
        }
        else
        {
            CombineMeshesPreserveMaterials(datName, bspObject);
            gameObjects.Add(bspObject);
        }

        if (ShouldCollide(matchingWorldObjectModel))
        {
            AddColliders(bspObject);
        }

        return gameObjects;
    }

    private static bool ShouldCollide(WorldObjectModel matchingWorldObjectModel)
    {
        // TTTT
        return true;
    }

    private static bool ShouldHideObjectType(WorldObjectTypes type)
    {
        return type == WorldObjectTypes.InvisibleVolume ||
            type == WorldObjectTypes.AIRail ||
            type == WorldObjectTypes.AIBarrier ||
            type == WorldObjectTypes.BuyZone ||
            type == WorldObjectTypes.RescueZone ||
            type == WorldObjectTypes.SoftLandingZone;
    }

    private static List<GameObject> GetChildren(GameObject parent, string excludeName = null)
    {
        var children = new List<GameObject>();
        for (int i = 0; i < parent.transform.childCount; i++)
        {
            var child = parent.transform.GetChild(i).gameObject;
            if (child.name != excludeName)
            {
                children.Add(child);
            }
        }
        
        return children.OrderBy(x => x.name).ToList();
    }

    private static void GroupBSPObjects(GameObject rootBSPObject, Dictionary<string, GameObject> worldObjectLookups)
    {
        var bspObjects = GetChildren(rootBSPObject, excludeName:"PhysicsBSP");

        var groups = bspObjects.GroupBy(x =>
        {
            var bspComponent = x.GetComponent<BSPObjectComponent>();
            return bspComponent?.WorldObjectType.ToString() ?? x.name;
        });

        foreach (var grp in groups.OrderBy(x => x.Key))
        {
            GameObject parentGroupObject = new GameObject(grp.Key);
            parentGroupObject.transform.parent = rootBSPObject.transform;

            foreach (GameObject obj in grp)
            {
                obj.transform.parent = parentGroupObject.transform;

                var bspComponent = obj.GetComponent<BSPObjectComponent>();

                if (worldObjectLookups.TryGetValue(bspComponent.WorldObjectName, out var worldObjectGameObject))
                {
                    // Move the matching WorldObject to be a child of the BSP object.
                    // The BSP has the mesh while the WorldObject has the properties about that object.
                    worldObjectGameObject.transform.parent = obj.transform;
                }
                else
                {
                    Debug.LogError($"Found BSP with no matching WorldObject: {obj.name}");
                }
            }

            if (ShouldHideObjectType(WorldObjectModelReader.GetWorldObjectType(grp.Key)))
            {
                parentGroupObject.SetActive(false);
            }
        }
    }

    private static void GroupWorldObjects(GameObject rootWorldObject)
    {
        var worldObjectGameObjects = GetChildren(rootWorldObject);

        var groups = worldObjectGameObjects.GroupBy(x =>
        {
            var worldObjectComponent = x.GetComponent<WorldObjectComponent>();
            return worldObjectComponent?.WorldObjectType.ToString() ?? x.name;
        });

        foreach (var grp in groups.OrderBy(x => x.Key))
        {
            GameObject parentGroupObject = new GameObject(grp.Key);
            parentGroupObject.transform.parent = rootWorldObject.transform;

            foreach (GameObject obj in grp)
            {
                obj.transform.parent = parentGroupObject.transform;
            }

            if (ShouldHideObjectType(WorldObjectModelReader.GetWorldObjectType(grp.Key)))
            {
                parentGroupObject.SetActive(false);
            }
        }
    }

    private static void GroupObjects(GameObject rootBSPObject, GameObject rootWorldObject, Dictionary<string, GameObject> worldObjectLookups)
    {
        GroupBSPObjects(rootBSPObject, worldObjectLookups);
        GroupWorldObjects(rootWorldObject);
    }

    private static List<GameObject> CreateBSPObjects(GameObject rootBSPObject, string name, DATModel datModel)
    {
        try
        {
            List<GameObject> gameObjects = new List<GameObject>();
            int i = 0;
            foreach (var bspModel in datModel.BSPModels.OrderBy(x => x.WorldName))
            {
                i++;
                float progress = (float)i / datModel.BSPModels.Count;
                EditorUtility.DisplayProgressBar($"Creating BSP objects for {name}", $"Item {i} of {datModel.BSPModels.Count}", progress);

                if (bspModel.WorldName.Contains("VisBSP", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                gameObjects.AddRange(CreateBSPObject(name, bspModel, datModel.WorldObjects, rootBSPObject.transform));
            }

            EditorUtility.DisplayProgressBar($"Grouping final BSP objects for {name}", $"Item {i} of {datModel.BSPModels.Count}", 99.9f);
            
            return gameObjects;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void ProcessWorldObject(WorldObjectModel obj)
    {
        String objectName = String.Empty;
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

    private static void AddWorldObjectComponent(GameObject gameObject, WorldObjectModel worldObjectModel)
    {
        var component = gameObject.AddComponent<WorldObjectComponent>();
        component.ObjectType = worldObjectModel.ObjectType;
        component.WorldObjectType = worldObjectModel.WorldObjectType;
        component.SkyObjectName = worldObjectModel.SkyObjectName;
        component.Position = worldObjectModel.Position.Value;
        component.RotationInDegrees = worldObjectModel.RotationInDegrees.Value;
        component.HasGravity = worldObjectModel.HasGravity ?? false;
        component.MoveToFloor = worldObjectModel.MoveToFloor ?? false;
        component.WeaponType = worldObjectModel.WeaponType;
        component.Scale = worldObjectModel.Scale ?? 1f;
        component.HasSurfaceAlpha = worldObjectModel.SurfaceAlpha.HasValue;
        component.SurfaceAlpha = worldObjectModel.SurfaceAlpha ?? 0;
        component.Filename = worldObjectModel.Filename;
        component.IsABC = worldObjectModel.IsABC;
        component.Skin = worldObjectModel.Skin;
        component.Index = worldObjectModel.Index ?? 0;
        component.Solid = worldObjectModel.Solid;
        component.Visible = worldObjectModel.Visible;
        component.Hidden = worldObjectModel.Hidden;
        component.Rayhit = worldObjectModel.Rayhit;
        component.Shadow = worldObjectModel.Shadow;
        component.Transparent = worldObjectModel.Transparent;
        component.ShowSurface = worldObjectModel.ShowSurface;
        component.UseRotation = worldObjectModel.UseRotation;
        component.SpriteSurfaceName = worldObjectModel.SpriteSurfaceName;
        component.SurfaceColor1 = worldObjectModel.SurfaceColor1 ?? Vector3.zero;
        component.SurfaceColor2 = worldObjectModel.SurfaceColor2 ?? Vector3.zero;
        component.Viscosity = worldObjectModel.Viscosity ?? 0;
    }

    private static GameObject CreateABCPrefabFromWorldObject(GameObject prefab, WorldObjectModel worldObjectModel, GameObject rootWorldObject, string tag)
    {
        GameObject abcObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (abcObject == null)
        {
            Debug.Log($"CreateABCPrefabFromWorldObject - abcObject == null. WorldObjectModel={(worldObjectModel == null ? "null worldobjectmodel" : worldObjectModel.Name)}");
        }

        abcObject.name = $"{worldObjectModel.Name} (Type={worldObjectModel.ObjectType} | Model={prefab.name})";
        abcObject.transform.parent = rootWorldObject.transform;
        AddWorldObjectComponent(abcObject, worldObjectModel);

        var worldObjectModelPosition = worldObjectModel.Position.Value  * UnityScaleFactor;

        abcObject.transform.position = worldObjectModelPosition + WorldObjectOffset;
        abcObject.transform.eulerAngles = worldObjectModel.RotationInDegrees.Value;
        abcObject.tag = tag;
        if (worldObjectModel.Scale.HasValue && worldObjectModel.Scale != 1f && worldObjectModel.Scale != 0f)
        {
            abcObject.transform.localScale = Vector3.one * worldObjectModel.Scale.Value;
        }

        if (worldObjectModel.MoveToFloor ?? false)
        {
            MoveDownToFloor(abcObject);
        }

        return abcObject;
    }

    private static GameObject CreateGenericWorldObject(WorldObjectModel worldObjectModel, GameObject rootWorldObject, string tag)
    {
        GameObject gameObject = new GameObject();
        gameObject.name = worldObjectModel.Name;
        gameObject.transform.parent = rootWorldObject.transform;
        AddWorldObjectComponent(gameObject, worldObjectModel);

        var worldObjectModelPosition = worldObjectModel.Position.Value * UnityScaleFactor;

        gameObject.transform.position = worldObjectModelPosition + WorldObjectOffset;
        gameObject.transform.eulerAngles = worldObjectModel.RotationInDegrees.Value;
        gameObject.tag = tag;

        return gameObject;
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

    private static Dictionary<string, GameObject> CreateWorldObjects(GameObject rootWorldObject, string name, DATModel datModel)
    {
        int i = 0;
        var s = $"Creating WorldObjects for {name}\r\n";
        Dictionary<string, GameObject> worldObjectLookups = new Dictionary<string, GameObject>();
        foreach (var worldObjectModel in datModel.WorldObjects.OrderBy(x => x.Name))
        {
            i++;
            float progress = (float)i / datModel.WorldObjects.Count;
            EditorUtility.DisplayProgressBar($"Creating World Objects for {name}", $"Item {i} of {datModel.WorldObjects.Count}", progress);

            if (worldObjectModel.IsABC)
            {
                if (worldObjectModel.SkinsLowercase.Count > 0)
                {
                    // Try to find match on ABC model that has a matching skin used by this DAT's world object.
                    // The ABC model might be "banner.abc" but have different skins/textures applied.
                    var prefab = UnityLookups.GetABCPrefab(worldObjectModel.Filename, worldObjectModel.Skin);
                    if (prefab != null)
                    {
                        worldObjectLookups.Add(worldObjectModel.Name, CreateABCPrefabFromWorldObject(prefab, worldObjectModel, rootWorldObject, LithtechTags.NoRayCast));
                    }
                    else
                    {
                        s += $"\tERROR - Object {worldObjectModel.Name} has ABCModel at path {worldObjectModel.Filename} but no matching skin for {worldObjectModel.Skin}\r\n";
                        worldObjectLookups.Add(worldObjectModel.Name, CreateGenericWorldObject(worldObjectModel, rootWorldObject, LithtechTags.NoRayCast));
                    }
                }
                else
                {
                    // No Skins.
                    var prefab = UnityLookups.GetABCPrefab(worldObjectModel.Filename, string.Empty);
                    if (prefab != null)
                    {
                        worldObjectLookups.Add(worldObjectModel.Name, CreateABCPrefabFromWorldObject(prefab, worldObjectModel, rootWorldObject, LithtechTags.NoRayCast));
                    }
                    else
                    {
                        s += $"\tERROR - Object {worldObjectModel.Name} has filename but no skin(s) - could instantiate mesh if we want?\r\n";
                        worldObjectLookups.Add(worldObjectModel.Name, CreateGenericWorldObject(worldObjectModel, rootWorldObject, LithtechTags.NoRayCast));
                    }
                }
            }
            else
            {
                worldObjectLookups.Add(worldObjectModel.Name, CreateGenericWorldObject(worldObjectModel, rootWorldObject, LithtechTags.NoRayCast));
            }

            //ProcessWorldObject(worldObjectModel);
        }

        Debug.Log(s);

        // SetupAmbientLight();

        EditorUtility.ClearProgressBar();

        return worldObjectLookups;
    }

    /// <summary>
    /// Create assets related to DAT models
    /// </summary>
    /// <param name="abcModels"></param>
    /// <param name="datModels"></param>
    /// <param name="materialLookups"></param>
    private static int CreateAssetsFromDATModels(List<DATModel> datModels)
    {
        AssetDatabase.StartAssetEditing();

        foreach (var datModel in datModels)
        {
            string name = Path.GetFileNameWithoutExtension(datModel.Filename);
            if (name != "DUNGEONRESCUE")
            {
                continue;
            }

            GameObject datRootObject = new GameObject(name);

            var rootBSPObject = new GameObject("BSP");
            rootBSPObject.transform.parent = datRootObject.transform;
            var bspObjects = CreateBSPObjects(rootBSPObject, name, datModel);

            var rootWorldObject = new GameObject("WorldObjects");
            rootWorldObject.transform.parent = datRootObject.transform;
            var worldObjectLookups = CreateWorldObjects(rootWorldObject, name, datModel);

            GroupObjects(rootBSPObject, rootWorldObject, worldObjectLookups);

            CreateBSPMeshAndPrefab(datRootObject, bspObjects);

            datRootObject.hideFlags = HideFlags.HideAndDontSave;
            DestroyImmediate(datRootObject);
        }

        RefreshAssetDatabase();

        return datModels.Count;
    }
}
