using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityToolbag;
using static LithFAQ.LTTypes;
using static LithFAQ.LTUtils;
using static Utility.MaterialSafeMeshCombine;
using static DTX;
using UnityEditor.ShaderGraph.Internal;


namespace LithFAQ
{
    public class DATReader66 : MonoBehaviour, IDATReader
    {
        public WorldObjects LTGameObjects = new WorldObjects();
        WorldReader worldReader = new WorldReader();
        List<WorldBsp> bspListTest = new List<WorldBsp>();
        public List<WorldObject> WorldObjectList = new List<WorldObject>();

        public float UNITYSCALEFACTOR = 0.01f; //default scale to fit in Unity's world.
        public Importer importer;

        public ModelToGameObject modelToGameObject;


        public void OnEnable()
        {
            UIActionManager.OnPreClearLevel += ClearLevel;
        }

        public void OnDisable()
        {
            UIActionManager.OnPreClearLevel -= ClearLevel;
        }

        public void Start()
        {
            importer = GetComponent<Importer>();
            gameObject.AddComponent<Dispatcher>();

            modelToGameObject = gameObject.AddComponent<ModelToGameObject>();
        }

        public void ClearLevel()
        {
            //reset loading text
            importer.loadingUI.text = "LOADING...";

            GameObject go = GameObject.Find("Level");

            //destroy all Meshes under the Level object
            MeshFilter[] meshFilters = go.GetComponentsInChildren<MeshFilter>();
            foreach (MeshFilter meshFilter in meshFilters)
            {
                DestroyImmediate(meshFilter.sharedMesh);
            }

            foreach (Transform child in go.transform)
            {
                Destroy(child.gameObject);
            }

            go = GameObject.Find("objects");

            foreach (Transform child in go.transform)
            {
                Destroy(child.gameObject);
            }

            go = GameObject.Find("Models");

            foreach (Transform child in go.transform)
            {
                foreach (MeshFilter meshFilter in child.GetComponentsInChildren<MeshFilter>())
                {
                    DestroyImmediate(meshFilter.sharedMesh);
                }

                Destroy(child.gameObject);
            }


            worldReader = new WorldReader();
            bspListTest = new List<WorldBsp>();
            LTGameObjects = new WorldObjects();

            foreach (Texture2D tex in importer.dtxMaterialList.textures.Values)
            {
                DestroyImmediate(tex);
            }
            foreach (Material mat in importer.dtxMaterialList.materials.Values)
            {
                DestroyImmediate(mat);
            }

            importer.dtxMaterialList = new DTXMaterialLibrary();

            Resources.UnloadUnusedAssets();

            //reset UI
            Controller lightController = GetComponent<Controller>();

            foreach (var toggle in lightController.settingsToggleList)
            {
                toggle.isOn = true;

                if (toggle.name == "Shadows")
                    toggle.isOn = false;
            }
        }

        public void Load(BinaryReader b)
        {
            importer = gameObject.GetComponent<Importer>();

            ClearLevel();

            LoadLevel(b);
        }

        void AddDebugLines()
        {
            var go = GameObject.Find("objects");
            foreach (Transform child in go.transform)
            {
                var mDefComponent = child.gameObject.GetComponent<ModelDefinitionComponent>();
                if (mDefComponent != null)
                {
                    var mDef = mDefComponent.ModelDef;

                    if (mDef.bMoveToFloor ||
                        mDef.modelType == ModelType.Pickup ||
                        mDef.modelType == ModelType.Character ||
                        mDef.modelType == ModelType.Weapon ||
                        mDef.szModelFileName.Contains("Tree02"))
                    {
                        var c = mDef.rootObject.AddComponent<DebugLines>();
                        c.MoveToFloor = mDef.bMoveToFloor;
                        c.ModelType = mDef.modelType;
                        c.ModelFilename = mDef.szModelFileName;
                    }
                }
            }

        }

        private bool IsVolume(WorldBsp tBSP)
        {
            return (tBSP.TextureNames[0].Contains("AI.dtx", StringComparison.OrdinalIgnoreCase) ||
                tBSP.TextureNames[0].Contains("sound.dtx", StringComparison.OrdinalIgnoreCase) ||
                tBSP.m_szWorldName.Contains("volume", StringComparison.OrdinalIgnoreCase) ||
                tBSP.m_szWorldName.Contains("Wwater") ||
                tBSP.m_szWorldName.Contains("weather", StringComparison.OrdinalIgnoreCase) ||
                tBSP.m_szWorldName.Contains("rain", StringComparison.OrdinalIgnoreCase) &&
                !tBSP.m_szWorldName.Contains("terrain", StringComparison.OrdinalIgnoreCase) ||
                tBSP.m_szWorldName.Contains("poison", StringComparison.OrdinalIgnoreCase) ||
                tBSP.m_szWorldName.Contains("corrosive", StringComparison.OrdinalIgnoreCase) ||
                tBSP.m_szWorldName.Contains("ladder", StringComparison.OrdinalIgnoreCase));
        }

        private void SetTag(GameObject mainObject, WorldBsp tBSP)
        {
            if (IsVolume(tBSP))
            {
                mainObject.tag = LithtechTags.Volumes;
            }
            else if (tBSP.m_szWorldName.Contains("AITrk", StringComparison.OrdinalIgnoreCase))
            {
                mainObject.tag = LithtechTags.AITrack;
            }
            else if (tBSP.m_szWorldName.Contains("AIBarrier", StringComparison.OrdinalIgnoreCase))
            {
                mainObject.tag = LithtechTags.AIBarrier;
            }
        }

        private bool IsTextureInvisible(string textureName, bool includeGlobaOpsNames, bool isSky)
        {
            if (isSky)
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

        private Material AddAndGetChromaMaterialIfNeeded(Material material, string bspWorldName, bool isTranslucent, bool isInvisible, GameObject mainObject)
        {
            var possibleTWM = GameObject.Find(bspWorldName + "_obj");
            if (possibleTWM)
            {
                var twm = possibleTWM.GetComponent<TranslucentWorldModel>();
                if (twm)
                {
                    if (twm.bChromakey || isTranslucent)
                    {
                        // Try to find already existing material
                        // Create (or use previously saved) translucent version of the texture.
                        if (importer.dtxMaterialList.materials.ContainsKey(material.name + "_Chromakey"))
                        {
                            material = importer.dtxMaterialList.materials[material.name + "_Chromakey"];
                        }
                        else
                        {
                            //copy material from matReference to a new
                            Material newMaterial = new Material(Shader.Find("Shader Graphs/Lithtech Vertex Transparent"));
                            newMaterial.name = material.name + "_Chromakey";
                            newMaterial.mainTexture = material.mainTexture;
                            newMaterial.SetInt("_Chromakey", 1);
                            material = newMaterial;
                            AddMaterialToMaterialDictionary(newMaterial.name, newMaterial, importer.dtxMaterialList);
                        }
                    }

                    if (isInvisible || !twm.bVisible)
                    {
                        mainObject.tag = LithtechTags.Blocker;
                    }
                }
            }

            return material;
        }

        private void CreateChildMeshes(WorldPoly tPoly, WorldBsp tBSP, int id, Material material, Transform parentTransform, TextureSize textureSize)
        {
            // Convert OPQ to UV magic
            Vector3 center = tPoly.m_vCenter;
            Vector3 o = tPoly.GetSurface(tBSP).m_fUV1;
            Vector3 p = tPoly.GetSurface(tBSP).m_fUV2;
            Vector3 q = tPoly.GetSurface(tBSP).m_fUV3;

            o *= UNITYSCALEFACTOR;
            o -= (Vector3)tPoly.m_vCenter;
            p /= UNITYSCALEFACTOR;
            q /= UNITYSCALEFACTOR;

            // CALCULATE EACH TRI INDIVIDUALLY.
            for (int nTriIndex = 0; nTriIndex < tPoly.m_nLoVerts - 2; nTriIndex++)
            {
                Vector3[] vertexList = new Vector3[tPoly.m_nLoVerts];
                Vector3[] _aVertexNormalList = new Vector3[tPoly.m_nLoVerts];
                Color[] _aVertexColorList = new Color[tPoly.m_nLoVerts];
                Vector2[] _aUVList = new Vector2[tPoly.m_nLoVerts];
                int[] _aTriangleIndices = new int[3];

                GameObject go = new GameObject(tBSP.WorldName + id);
                // TTTT - Made static for lighting
                go.isStatic = true;
                go.transform.parent = parentTransform;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                MeshFilter mf = go.AddComponent<MeshFilter>();

                Mesh m = new Mesh();

                for (int vCount = 0; vCount < tPoly.m_nLoVerts; vCount++)
                {
                    WorldVertex tVertex = tBSP.m_pPoints[(int)tPoly.m_aVertexColorList[vCount].nVerts];

                    Vector3 data = tVertex.m_vData;
                    data *= UNITYSCALEFACTOR;
                    vertexList[vCount] = data;

                    Color color = new Color(
                        tPoly.m_aVertexColorList[vCount].red / 255,
                        tPoly.m_aVertexColorList[vCount].green / 255,
                        tPoly.m_aVertexColorList[vCount].blue / 255,
                        1.0f);
                    _aVertexColorList[vCount] = color;
                    _aVertexNormalList[vCount] = tBSP.m_pPlanes[tPoly.m_nPlane].m_vNormal;

                    // Calculate UV coordinates based on the OPQ vectors
                    // Note that since the worlds are offset from 0,0,0 sometimes we need to subtract the center point
                    Vector3 curVert = vertexList[vCount];
                    float u = Vector3.Dot((curVert - center) - o, p);
                    float v = Vector3.Dot((curVert - center) - o, q);

                    //Scale back down into something more sane
                    u /= textureSize.EngineWidth;
                    v /= textureSize.EngineHeight;

                    _aUVList[vCount] = new Vector2(u, v);
                }

                m.SetVertices(vertexList);
                m.SetNormals(_aVertexNormalList);
                m.SetUVs(0, _aUVList);
                m.SetColors(_aVertexColorList);

                // Hacky, whatever
                _aTriangleIndices[0] = 0;
                _aTriangleIndices[1] = nTriIndex + 1;
                _aTriangleIndices[2] = (nTriIndex + 2) % tPoly.m_nLoVerts;

                // Set triangles
                m.SetTriangles(_aTriangleIndices, 0);
                m.RecalculateTangents();

                mr.material = material;
                mf.mesh = m;

                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            }
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

        private void AddColliders(GameObject levelGameObject)
        {
            // Assign the mesh collider to the combined meshes
            
            foreach (var t in levelGameObject.GetComponentsInChildren<MeshFilter>())
            {
                var mc = t.transform.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = t.mesh;
            }
        }

        private void CreateBSPChildMeshes(WorldBsp tBSP, GameObject mainObject, ref int id)
        {
            foreach (WorldPoly tPoly in tBSP.m_pPolies)
            {
                // remove all bsp invisible
                var textureName = tBSP.TextureNames[tPoly.GetSurface(tBSP).m_nTexture];
                var surfaceFlags = tPoly.GetSurface(tBSP).m_nFlags;
                bool isSky = (surfaceFlags & (int)BitMask.SKY) == (int)BitMask.SKY;
                bool isTranslucent = (surfaceFlags & (int)BitMask.TRANSLUCENT) == (int)BitMask.TRANSLUCENT;
                bool isInvisible = (surfaceFlags & (int)BitMask.INVISIBLE) == (int)BitMask.INVISIBLE;

                if (IsTextureInvisible(textureName, importer.eGame == Game.GLOBALOPS, isSky))
                {
                    continue;
                }

                // TTTT Make things invis
                if (isInvisible)
                {
                    // Set texture index to last in the array of texture names.
                    tBSP.m_pSurfaces[tPoly.m_nSurface].m_nTexture = (short)(tBSP.TextureNames.Count - 1);
                }

                TextureSize textureSize = importer.dtxMaterialList.texSize[textureName];
                Material matReference = importer.dtxMaterialList.materials[textureName];

                // Check if the material needs to add the Chroma flag - which requires (possibly) creating a new Material based on the original texture.
                // May also update the tag of the mainObject
                matReference = AddAndGetChromaMaterialIfNeeded(matReference, tBSP.WorldName, isTranslucent, isInvisible, mainObject);

                CreateChildMeshes(tPoly, tBSP, id, matReference, mainObject.transform, textureSize);
                id++;
            }

        }

        public async void LoadLevel(BinaryReader b)
        {
            importer.loadingUI.enabled = true;
            await System.Threading.Tasks.Task.Yield();

            worldReader.ReadHeader(ref b);
            worldReader.ReadPropertiesAndExtents(ref b);

            WorldTree wTree = new WorldTree();

            wTree.ReadWorldTree(ref b);

            //read world models...
            byte[] anDummy = new byte[32];
            int nNextWMPosition = 0;

            WorldData pWorldData = new WorldData();

            WorldModelList WMList = new WorldModelList();
            WMList.pModelList = new List<WorldData>();
            WMList.nNumModels = b.ReadInt32();

            for (int i = 0; i < WMList.nNumModels; i++)
            {
                nNextWMPosition = b.ReadInt32();
                anDummy = b.ReadBytes(anDummy.Length);

                pWorldData.NextPos = nNextWMPosition;
                WMList.pModelList.Add(pWorldData);

                WorldBsp tBSP = new WorldBsp();
                tBSP.datVersion = worldReader.WorldHeader.nVersion;

                try
                {
                    tBSP.Load(ref b, true, importer.eGame);
                    bspListTest.Add(tBSP);
                }
                catch (Exception e)
                {
                    Debug.Log(e.Message);
                }

                b.BaseStream.Position = nNextWMPosition;
            }

            b.BaseStream.Position = worldReader.WorldHeader.dwObjectDataPos;
            LoadObjects(ref b);

            importer.infoBox.text = string.Format("Loaded World: {0}", Path.GetFileName(importer.szFileName));

            b.BaseStream.Close();

            importer.loadingUI.text = "Loading Objects";
            await System.Threading.Tasks.Task.Yield();

            int id = 0;
            foreach (WorldBsp tBSP in bspListTest)
            {
                if (tBSP.m_szWorldName.Contains("PhysicsBSP"))
                {
                    importer.loadingUI.text = "Loading BSP";
                    await System.Threading.Tasks.Task.Yield();
                }

                if (tBSP.m_szWorldName == "VisBSP")
                {
                    continue;
                }

                GameObject mainObject = new GameObject(tBSP.WorldName);
                // TTTT - Made static for lighting
                mainObject.isStatic = true;
                mainObject.transform.parent = this.transform;
                mainObject.AddComponent<MeshFilter>();
                mainObject.AddComponent<MeshRenderer>().material = importer.defaultMaterial;

                SetTag(mainObject, tBSP);
                LoadTexturesForBSP(tBSP);
                CreateBSPChildMeshes(tBSP, mainObject, ref id);
            }

            importer.loadingUI.text = "Combining Meshes";
            await System.Threading.Tasks.Task.Yield();

            var levelGameObject = GameObject.Find("Level");
            CombineMeshes(levelGameObject);
            AddColliders(levelGameObject);

            // Clip light from behind walls
            foreach (var t in levelGameObject.gameObject.GetComponentsInChildren<MeshRenderer>())
            {
                t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            }

            importer.loadingUI.enabled = false;

            //Batch all the objects
            // StaticBatchingUtility.Combine(toBatch.ToArray(), gLevelRoot);
            await System.Threading.Tasks.Task.Yield();
            
            SetupSkyBoxMaterials();
            AddDebugLines();
        }

        /// <summary>
        /// Sets up the skybox materials, Lithtech engine games use SkyPointer's to set the index of the skybox objects <br />
        /// We can use Unity's Render Queue to set the order of the skybox objects
        /// </summary>
        private void SetupSkyBoxMaterials()
        {
            Shader shaderUnlitTransparent = Shader.Find("Unlit/Transparent");
            foreach (var item in LTGameObjects.obj)
            {
                if (!item.objectName.Contains("SkyPointer", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var skyObjectName = (string)item.options["SkyObjectName"];
                var skyObjectModel = GameObject.Find(skyObjectName);

                if (!skyObjectModel) continue;

                bool bHasIndex = item.options.ContainsKey("Index");
                float nIndex = 0;
                if (bHasIndex)
                {
                    var nValue = (UInt32)item.options["Index"];
                    var aBytes = BitConverter.GetBytes(nValue);
                    nIndex = BitConverter.ToSingle(aBytes, 0);
                }

                foreach (var mrMeshRenderer in skyObjectModel.GetComponentsInChildren<MeshRenderer>())
                {
                    //set layer to 8 which is SkyBox so it doessssn't get rendered by the Main Camera.
                    mrMeshRenderer.gameObject.layer = 8;

                    //Since we combine meshes we need to set the material for each submesh
                    foreach (var mat in mrMeshRenderer.materials)
                    {
                        mat.shader = shaderUnlitTransparent;

                        //set the render queue to 3000 + the index value so the SkyPointer can control which element is drawn first.
                        if (bHasIndex)
                        {
                            mat.renderQueue = (int)nIndex + 3000;
                        }
                    }
                }
            }
        }

        private void LoadTexturesForBSP(WorldBsp tBSP)
        {
            //Load texture
            foreach (var tex in tBSP.TextureNames)
            {
                DTX.LoadDTXIntoLibrary(tex, importer.dtxMaterialList, importer.szProjectPath);
            }
        }

        IEnumerator LoadAndPlay(string uri, AudioSource audioSource)
        {
            bool bIsNotWAV = false;
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError("Error: " + www.error);
                }
                else
                {
                    if (www.downloadHandler.data[20] == 1)
                    {
                        AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                        audioSource.clip = clip;
                        audioSource.Play();
                    }
                    else
                    {
                        bIsNotWAV = true;
                    }
                }
            }
            if (bIsNotWAV)
            {
                using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.MPEG))
                {
                    yield return www.SendWebRequest();

                    if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                    {
                        Debug.LogError("Error: " + www.error);
                    }
                    else
                    {

                            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                            audioSource.clip = clip;
                            audioSource.Play();

                    }
                }
            }
        }

        public void LoadObjects(ref BinaryReader b)
        {

            LTGameObjects = ReadObjects(ref b);

            foreach (var obj in LTGameObjects.obj)
            {
                Vector3 objectPos = new Vector3();
                Quaternion objectRot = new Quaternion();
                Vector3 rot = new Vector3();
                String objectName = String.Empty;
                bool bInvisible = false;
                bool bChromakey = false;

                WorldObject thisObject = new WorldObject();

                var optArray = obj.options.ToArray();
                for (int optionIndex = 0; optionIndex < optArray.Length; optionIndex++)
                {
                    var subItem = optArray[optionIndex];

                    if (subItem.Key == "Name")
                        objectName = (String)subItem.Value;

                    else if (subItem.Key == "Pos")
                    {
                        LTVector temp = (LTVector)subItem.Value;
                        objectPos = new Vector3(temp.X, temp.Y, temp.Z) * UNITYSCALEFACTOR;
                    }

                    else if (subItem.Key == "Rotation")
                    {
                        LTRotation temp = (LTRotation)subItem.Value;
                        rot = new Vector3(temp.X * Mathf.Rad2Deg, temp.Y * Mathf.Rad2Deg, temp.Z * Mathf.Rad2Deg);
                    }

                }

                var tempObject = Instantiate(importer.RuntimeGizmoPrefab, objectPos, objectRot);
                tempObject.name = objectName + "_obj";
                tempObject.transform.eulerAngles = rot;

                if (obj.objectName == "WorldProperties")
                {
                    //find child gameobject named Icon
                    var icon = tempObject.transform.Find("Icon");
                    icon.GetComponent<MeshRenderer>().material.mainTexture = Resources.Load<Texture2D>("Gizmos/worldproperties");
                    icon.gameObject.tag = LithtechTags.NoRayCast;
                    icon.gameObject.layer = 7;
                }
                else if (obj.objectName == "SoundFX" || obj.objectName == "AmbientSound")
                {
                    //find child gameobject named Icon
                    var icon = tempObject.transform.Find("Icon");
                    icon.GetComponent<MeshRenderer>().material.mainTexture = Resources.Load<Texture2D>("Gizmos/sound");
                    icon.gameObject.tag = LithtechTags.NoRayCast;
                    icon.gameObject.layer = 7;

                    AudioSource temp = tempObject.AddComponent<AudioSource>();
                    var volumeControl = tempObject.AddComponent<Volume2D>();

                    string szFilePath = String.Empty;

                    foreach (var subItem in obj.options)
                    {
                        if (subItem.Key == "Sound" || subItem.Key == "Filename")
                        {
                            szFilePath = Path.Combine(importer.szProjectPath, subItem.Value.ToString());
                        }

                        if (subItem.Key == "Loop")
                        {
                            temp.loop = (bool)subItem.Value;
                        }

                        if (subItem.Key == "Ambient")
                        {
                            if ((bool)subItem.Value)
                            {
                                temp.spatialize = false;
                            }
                            else
                            {
                                temp.spatialize = true;
                                temp.spatialBlend = 1.0f;
                            }
                        }

                        if (subItem.Key == "Volume")
                        {
                            float vol = (UInt32)subItem.Value;
                            temp.volume = vol / 100;
                        }
                        if (subItem.Key == "OuterRadius")
                        {
                            float vol = (float)subItem.Value;
                            temp.maxDistance = vol / 75;

                            volumeControl.audioSource = temp;
                            volumeControl.listenerTransform = Camera.main.transform;
                            volumeControl.maxDist = temp.maxDistance;
                        }

                    }
                    StartCoroutine(LoadAndPlay(szFilePath, temp));
                }
                else if (obj.objectName == "TranslucentWorldModel" || obj.objectName == "Electricity" || obj.objectName == "Door")
                {
                    string szObjectName = String.Empty;
                    foreach (var subItem in obj.options)
                    {
                        if (subItem.Key == "Visible")
                            bInvisible = (bool)subItem.Value;
                        else if (subItem.Key == "Chromakey")
                            bChromakey = (bool)subItem.Value;
                        else if (subItem.Key == "Name")
                            szObjectName = (String)subItem.Value;
                    }

                    var twm = tempObject.AddComponent<TranslucentWorldModel>();
                    twm.bChromakey = bChromakey;
                    twm.bVisible = bInvisible;
                    twm.szName = szObjectName;
                }
                else if (obj.objectName == "Light")
                {
                    //find child gameobject named Icon
                    var icon = tempObject.transform.Find("Icon");
                    icon.GetComponent<MeshRenderer>().material.mainTexture = Resources.Load<Texture2D>("Gizmos/light");
                    icon.gameObject.tag = LithtechTags.NoRayCast;
                    icon.gameObject.layer = 7;

                    var light = tempObject.gameObject.AddComponent<Light>();
                    light.lightmapBakeType = LightmapBakeType.Baked;

                    foreach (var subItem in obj.options)
                    {
                        if (subItem.Key == "LightRadius")
                            light.range = (float)subItem.Value * 0.01f;

                        else if (subItem.Key == "LightColor")
                        {
                            var vec = (LTVector)subItem.Value;
                            Vector3 col = Vector3.Normalize(new Vector3(vec.X, vec.Y, vec.Z));
                            light.color = new Color(col.x, col.y, col.z);
                        }

                        else if (subItem.Key == "BrightScale")
                            light.intensity = (float)subItem.Value;
                    }
                    light.shadows = LightShadows.Soft;

                    Controller lightController = transform.GetComponent<Controller>();

                    foreach (var toggle in lightController.settingsToggleList)
                    {
                        if (toggle.name == "Shadows")
                        {
                            if (toggle.isOn)
                                light.shadows = LightShadows.Soft;
                            else
                                light.shadows = LightShadows.None;
                        }
                    }
                }
                else if (obj.objectName == "DirLight")
                {
                    //find child gameobject named Icon
                    var icon = tempObject.transform.Find("Icon");
                    icon.GetComponent<MeshRenderer>().material.mainTexture = Resources.Load<Texture2D>("Gizmos/light");
                    icon.gameObject.tag = LithtechTags.NoRayCast;
                    icon.gameObject.layer = 7;
                    var light = tempObject.gameObject.AddComponent<Light>();


                    foreach (var subItem in obj.options)
                    {
                        if (subItem.Key == "FOV")
                        {
                            light.innerSpotAngle = (float)subItem.Value;
                            light.spotAngle = (float)subItem.Value;
                        }

                        else if (subItem.Key == "LightRadius")
                            light.range = (float)subItem.Value * 0.01f;

                        else if (subItem.Key == "InnerColor")
                        {
                            var vec = (LTVector)subItem.Value;
                            Vector3 col = Vector3.Normalize(new Vector3(vec.X, vec.Y, vec.Z));
                            light.color = new Color(col.x, col.y, col.z);
                        }

                        else if (subItem.Key == "BrightScale")
                            light.intensity = (float)subItem.Value * 15;
                    }

                    light.shadows = LightShadows.Soft;
                    light.type = LightType.Spot;

                    Controller lightController = GetComponent<Controller>();

                    foreach (var toggle in lightController.settingsToggleList)
                    {
                        if (toggle.name == "Shadows")
                        {
                            if (toggle.isOn)
                                light.shadows = LightShadows.Soft;
                            else
                                light.shadows = LightShadows.None;
                        }
                    }
                }
                else if (obj.objectName == "StaticSunLight")
                {
                    //find child gameobject named Icon
                    var icon = tempObject.transform.Find("Icon");
                    icon.GetComponent<MeshRenderer>().material.mainTexture = Resources.Load<Texture2D>("Gizmos/light");
                    icon.gameObject.tag = LithtechTags.NoRayCast;
                    icon.gameObject.layer = 7;
                    var light = tempObject.gameObject.AddComponent<Light>();

                    foreach (var subItem in obj.options)
                    {
                        if (subItem.Key == "InnerColor")
                        {
                            var vec = (LTVector)subItem.Value;
                            Vector3 col = Vector3.Normalize(new Vector3(vec.X, vec.Y, vec.Z));
                            light.color = new Color(col.x, col.y, col.z);
                        }
                        else if (subItem.Key == "BrightScale")
                            light.intensity = (float)subItem.Value;
                    }

                    light.shadows = LightShadows.Soft;
                    light.type = LightType.Directional;

                    Controller lightController = GetComponent<Controller>();

                    foreach (var toggle in lightController.settingsToggleList)
                    {
                        if (toggle.name == "Shadows")
                        {
                            if (toggle.isOn)
                                light.shadows = LightShadows.Soft;
                            else
                                light.shadows = LightShadows.None;
                        }
                    }
                }
                else if (obj.objectName == "GameStartPoint")
                {

                    int nCount = ModelDefinition.AVP2RandomCharacterGameStartPoint.Length;

                    int nRandom = UnityEngine.Random.Range(0, nCount);
                    string szName = ModelDefinition.AVP2RandomCharacterGameStartPoint[nRandom];

                    var temp = importer.CreateModelDefinition(szName, ModelType.Character, obj.options);
                    var hasGravity = obj.options.ContainsKey("Gravity") ? (bool)obj.options["Gravity"] : false;
                    var gos = modelToGameObject.LoadABC(temp, tempObject.transform, hasGravity);

                    if (gos != null)
                    {
                        gos.transform.position = tempObject.transform.position;
                        gos.transform.eulerAngles = rot;
                        gos.tag = LithtechTags.NoRayCast;
                    }

                    //find child gameobject named Icon
                    var icon = tempObject.transform.Find("Icon");
                    icon.GetComponent<MeshRenderer>().material.mainTexture = Resources.Load<Texture2D>("Gizmos/gsp");
                    icon.gameObject.tag = LithtechTags.NoRayCast;
                    icon.gameObject.layer = 7;
                }
                else if (obj.objectName == "WeaponItem")
                {
                    string szName = "";

                    if (obj.options.ContainsKey("WeaponType"))
                    {
                        szName = (string)obj.options["WeaponType"];
                    }

                    //abc.FromFile("Assets/Models/" + szName + ".abc", true);

                    var temp = importer.CreateModelDefinition(szName, ModelType.WeaponItem, obj.options);
                    var hasGravity = obj.options.ContainsKey("Gravity") ? (bool)obj.options["Gravity"] : false;
                    var gos = modelToGameObject.LoadABC(temp, tempObject.transform, hasGravity);

                    if (gos != null)
                    {
                        gos.transform.position = tempObject.transform.position;
                        gos.transform.eulerAngles = rot;
                        gos.tag = LithtechTags.NoRayCast;
                        gos.layer = 2;
                    }


                }
                else if (obj.objectName == "PropType" || obj.objectName == "CProp")
                {
                    string szName = "";

                    if (obj.options.ContainsKey("Name"))
                    {
                        szName = (string)obj.options["Name"];
                    }


                    var temp = importer.CreateModelDefinition(szName, ModelType.PropType, obj.options);
                    var hasGravity = obj.options.ContainsKey("Gravity") ? (bool)obj.options["Gravity"] : false;
                    var gos = modelToGameObject.LoadABC(temp, tempObject.transform, hasGravity);

                    if (gos != null)
                    {
                        gos.transform.position = tempObject.transform.position;
                        gos.transform.eulerAngles = rot;
                        gos.tag = LithtechTags.NoRayCast;
                    }
                }
                else if (
                    obj.objectName == "Prop" ||
                    obj.objectName == "AmmoBox" ||
                    obj.objectName == "Beetle" ||
                    //obj.objectName == "BodyProp" || // not implemented
                    obj.objectName == "Civilian" ||
                    obj.objectName == "Egg" ||
                    obj.objectName == "HackableLock" ||
                    obj.objectName == "Plant" ||
                    obj.objectName == "StoryObject" ||
                    obj.objectName == "MEMO" ||
                    obj.objectName == "PC" ||
                    obj.objectName == "PDA" ||
                    obj.objectName == "Striker" ||
                    obj.objectName == "TorchableLock" ||
                    obj.objectName == "Turret" ||
                    obj.objectName == "TreasureChest" ||
                    obj.objectName == "Candle" ||
                    obj.objectName == "CandleWall")
                {
                    string szName = "";

                    if (obj.options.ContainsKey("Name"))
                    {
                        szName = (string)obj.options["Name"];
                    }

                    var temp = importer.CreateModelDefinition(szName, ModelType.Prop, obj.options);
                    var hasGravity = obj.options.ContainsKey("Gravity") ? (bool)obj.options["Gravity"] : false;
                    var gos = modelToGameObject.LoadABC(temp, tempObject.transform, hasGravity);

                    if (gos != null)
                    {
                        gos.transform.position = tempObject.transform.position;
                        gos.transform.eulerAngles = rot;
                        gos.tag = LithtechTags.NoRayCast;

                        if (obj.options.ContainsKey("Scale"))
                        {
                            float scale = (float)obj.options["Scale"];
                            if (scale != 1f)
                            {
                                gos.transform.localScale = Vector3.one * scale;
                            }
                        }
                    }
                }
                else if (obj.objectName == "Princess")
                {
                    string szName = "";

                    if (obj.options.ContainsKey("Name"))
                    {
                        szName = (string)obj.options["Name"];
                    }

                    var temp = importer.CreateModelDefinition(szName, ModelType.Princess, obj.options);
                    var hasGravity = obj.options.ContainsKey("Gravity") ? (bool)obj.options["Gravity"] : false;
                    var gos = modelToGameObject.LoadABC(temp, tempObject.transform, hasGravity);

                    if (gos != null)
                    {
                        gos.transform.position = tempObject.transform.position;
                        gos.transform.eulerAngles = rot;
                        gos.tag = LithtechTags.NoRayCast;

                        if (obj.options.ContainsKey("Scale"))
                        {
                            float scale = (float)obj.options["Scale"];
                            if (scale != 1f)
                            {
                                gos.transform.localScale = Vector3.one * scale;
                            }
                        }
                    }
                }
                else if (obj.objectName == "Trigger")
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
                    string szName = "";

                    if (obj.options.ContainsKey("Name"))
                    {
                        szName = (string)obj.options["Name"];
                    }

                    var temp = importer.CreateModelDefinition(szName, ModelType.Monster, obj.options);
                    var hasGravity = obj.options.ContainsKey("Gravity") ? (bool)obj.options["Gravity"] : false;
                    var gos = modelToGameObject.LoadABC(temp, tempObject.transform, hasGravity);

                    if (gos != null)
                    {
                        gos.transform.position = tempObject.transform.position;
                        gos.transform.eulerAngles = rot;
                        gos.tag = LithtechTags.NoRayCast;

                        if (obj.options.ContainsKey("Scale"))
                        {
                            float scale = (float)obj.options["Scale"];
                            if (scale != 1f)
                            {
                                gos.transform.localScale = Vector3.one * scale;
                            }
                        }
                    }
                }

                var g = GameObject.Find("objects");
                tempObject.transform.SetParent(g.transform);

                g.transform.localScale = Vector3.one;
            }

            //disable unity's nastyness
            //RenderSettings.ambientLight = Color.black;
            //RenderSettings.ambientIntensity = 0.0f;

            //Setup AmbientLight
            SetupAmbientLight();
        }
        public void SetupAmbientLight()
        {
            if (worldReader.WorldProperties == null)
                return;

            var worldPropertiesArray = worldReader.WorldProperties.Split(';');

            bool setAmbientLight = false;
            foreach (var property in worldPropertiesArray)
            {
                if (property.Contains("AmbientLight"))
                {
                    var splitStrings = property.Trim().Split(' ');

                    if (splitStrings.Length < 4)
                    {
                        continue;
                    }

                    Vector3 vAmbientRGB = Vector3.Normalize(
                        new Vector3(
                            float.Parse(splitStrings[1]),
                            float.Parse(splitStrings[2]),
                            float.Parse(splitStrings[3])));

                    var color = new Color(vAmbientRGB.x, vAmbientRGB.y, vAmbientRGB.z, 1);
                    SetAmbientLight(color);
                    setAmbientLight = true;
                }
            }

            if (!setAmbientLight)
            {
                SetDefaultAmbientLight();
            }
        }

        private void SetDefaultAmbientLight()
        {
            importer.defaultColor = new Color(0.1f, 0.1f, 0.1f, 1);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;
        }

        private void SetAmbientLight(Color color)
        {
            //Check if color is 0,0,0 and boost a bit
            if (color.r == 0 && color.g == 0 && color.b == 0)
            {
                color = new Color(0.1f, 0.1f, 0.1f, 1);
            }

            RenderSettings.ambientLight = color;
            importer.defaultColor = color;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;
        }
        public void Quit()
        {
            Application.Quit();
        }

        public static WorldObjects ReadObjects(ref BinaryReader b)
        {
            WorldObjects woObject = new WorldObjects();
            woObject.obj = new List<WorldObject>();

            var nTotalObjectCount = b.ReadInt32();

            for (int i = 0; i < nTotalObjectCount; i++)
            {
                //Make a new object
                WorldObject theObject = new WorldObject();

                //Make a dictionary to make things easier
                Dictionary<string, object> tempData = new Dictionary<string, object>();

                theObject.dataOffset = b.BaseStream.Position; // store our offset in our .dat

                theObject.dataLength = b.ReadInt16(); // Read our object datalength

                var dataLength = b.ReadInt16(); //read out property length

                theObject.objectName = ReadString(dataLength, ref b); // read our name

                theObject.objectEntries = b.ReadInt32();// read how many properties this object has

                string realObjectName = string.Empty;
                for (int t = 0; t < theObject.objectEntries; t++)
                {
                    var nObjectPropertyDataLength = b.ReadInt16();
                    string szPropertyName = ReadString(nObjectPropertyDataLength, ref b);

                    PropType propType = (PropType)b.ReadByte();

                    theObject.objectEntryFlag.Add(b.ReadInt32()); //read the flag

                    switch (propType)
                    {
                        case PropType.PT_STRING:
                            theObject.objectEntryStringDataLength.Add(b.ReadInt16()); //read the string length plus the data length
                            nObjectPropertyDataLength = b.ReadInt16();
                            //Read the string
                            tempData.Add(szPropertyName, ReadString(nObjectPropertyDataLength, ref b));
                            break;

                        case PropType.PT_VECTOR:
                            nObjectPropertyDataLength = b.ReadInt16();
                            //Get our float data
                            LTVector tempVec = ReadLTVector(ref b);
                            //Add our object to the Dictionary
                            tempData.Add(szPropertyName, tempVec);
                            break;

                        case PropType.PT_ROTATION:
                            //Get our data length
                            nObjectPropertyDataLength = b.ReadInt16();
                            //Get our float data
                            LTRotation tempRot = ReadLTRotation(ref b);
                            //Add our object to the Dictionary
                            tempData.Add(szPropertyName, tempRot);
                            break;
                        case PropType.PT_UINT:
                            // Read the "size" of what we should read.
                            // For UINT the nObjectPropertyDataLength should always be 4.
                            nObjectPropertyDataLength = b.ReadInt16();
                            //Add our object to the Dictionary
                            tempData.Add(szPropertyName, b.ReadUInt32());
                            break;
                        case PropType.PT_BOOL:
                            nObjectPropertyDataLength = b.ReadInt16();
                            tempData.Add(szPropertyName, ReadBool(ref b));
                            break;
                        case PropType.PT_REAL:
                            nObjectPropertyDataLength = b.ReadInt16();
                            //Add our object to the Dictionary
                            tempData.Add(szPropertyName, ReadReal(ref b));
                            break;
                        case PropType.PT_COLOR:
                            nObjectPropertyDataLength = b.ReadInt16();
                            //Get our float data
                            LTVector tempCol = ReadLTVector(ref b);
                            //Add our object to the Dictionary
                            tempData.Add(szPropertyName, tempCol);
                            break;
                        default:
                            Debug.LogError("Unknown prop type: " + propType);
                            break;
                    }

                    if (szPropertyName == "Name")
                    {
                        realObjectName = tempData["Name"].ToString();
                    }
                }

                theObject.options = tempData;

                woObject.obj.Add(theObject);
            }
            return woObject;
        }

        public WorldObjects GetWorldObjects()
        {
            return LTGameObjects;
        }

        public uint GetVersion()
        {
            return (uint)worldReader.WorldHeader.nVersion;
        }
    }
}