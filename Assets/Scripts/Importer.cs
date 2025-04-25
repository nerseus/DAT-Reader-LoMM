using System.Collections.Generic;
using UnityEngine;
using LithFAQ;
using static LithFAQ.LTTypes;
using static LithFAQ.LTUtils;
using static DTX;
using System.IO;
using SFB;
using System;
using System.Linq;
using UnityEditor;
using System.Text;

public class Importer : MonoBehaviour
{
    [SerializeField]
    public DTXMaterialLibrary dtxMaterialList { get; set; } = new DTXMaterialLibrary();
    public Component DatReader;
    public GameObject RuntimeGizmoPrefab;


    [SerializeField]
    public Material defaultMaterial;
    public Color defaultColor = new Color(0.5f, 0.5f, 0.5f, 1.0f);
    public UnityEngine.UI.Text infoBox;
    public UnityEngine.UI.Text loadingUI;
    public GameObject prefab;

    [Header("GameSpecific")]
    public string szProjectPath = String.Empty;
    public string szFileName;
    public uint nVersion;
    public int nSelectedGame;
    public Game eGame { get { return (Game)nSelectedGame; }}

    public Dictionary<ModelType, INIParser> configButes = new Dictionary<ModelType, INIParser>();

    private void OpenDAT(string pathToDATFile, string projectPath)
    {
        this.szProjectPath = projectPath;
        this.szFileName = Path.GetFileName(pathToDATFile);

        //Debug.Log("Project Path: " + this.szProjectPath);
        //Debug.Log("File Name: " + this.szFileName);
        //Debug.Log("File Path: " + pathToDATFile);

        BinaryReader binaryReader = new BinaryReader(File.Open(pathToDATFile, FileMode.Open));
        if (binaryReader == null)
        {
            Debug.LogError("Could not open DAT file");
            return;
        }

        nVersion = ReadDATVersion(ref binaryReader);

        DatReader = null;

        //Build the string to find the correct DAT reader class based on the version read from the DAT
        string szComponentName = "LithFAQ.DATReader" + nVersion.ToString();
        DatReader = gameObject.AddComponent(Type.GetType(szComponentName));
        if (DatReader == null)
        {
            Debug.LogError("Could not find DAT reader for version " + nVersion.ToString());
            return;
        }

        //load the DAT
        IDATReader reader = (IDATReader)DatReader;
        reader.Load(binaryReader);

        UIActionManager.OnPostLoadLevel?.Invoke();
    }

    public void OpenDAT()
    {
        ExtensionFilter[] efExtensionFiler = new[] { new ExtensionFilter("Lithtech World DAT", "dat") };

        // Open file
        string[] pathToDATFile = StandaloneFileBrowser.OpenFilePanel("Open File", "", efExtensionFiler, false);
        if (pathToDATFile.Length == 0)
        {
            return;
        }

        string defaultProjectPath = Path.GetDirectoryName(pathToDATFile[0]);
        string[] projectPath = StandaloneFileBrowser.OpenFolderPanel("Open Project Path", defaultProjectPath, false);
        if (projectPath.Length == 0)
        {
            projectPath = new string[] { defaultProjectPath };
        }

        OpenDAT(pathToDATFile[0], projectPath[0]);
    }

    private bool ChooseABCFile()
    {
        ExtensionFilter[] efExtensionFiler = new[] { new ExtensionFilter("Lithtech Model ABC", "abc") };

        // Open file
        string[] pathToABCFile = StandaloneFileBrowser.OpenFilePanel("Open File", "", efExtensionFiler, false);
        if (pathToABCFile.Length == 0)
        {
            return false;
        }

        string defaultProjectPath = Path.GetDirectoryName(pathToABCFile[0]);
        string[] projectPath = StandaloneFileBrowser.OpenFolderPanel("Open Project Path", defaultProjectPath, false);
        if (projectPath.Length == 0)
        {
            if (!string.IsNullOrEmpty(this.szProjectPath))
            {
                projectPath = new string[] { this.szProjectPath };
            }
            else
            {
                projectPath = new string[] { defaultProjectPath };
            }
        }

        this.szProjectPath = projectPath[0];
        this.szFileName = pathToABCFile[0];

        return true;
    }

    public void OpenABC()
    {
        //this.szFileName = "c:\\lomm\\data\\models\\basilisk.abc";
        //this.szProjectPath = "c:\\lomm\\data\\";
        //var model = ABCModelReader.LoadABCModel(this.szFileName);
        //var skinTextures = new List<string> { "c:\\lomm\\data\\skins\\basilisk.dtx" };
        //CreateABC(model, skinTextures);
        if (!ChooseABCFile())
        {
            return;
        }

        var model = ABCModelReader.ReadABCModel(this.szFileName, this.szProjectPath);
        if (model != null)
        {
            var matCount = model.GetMaterialCount();
            if (matCount > 0)
            {
                var skinTextures = GetDTXSkins(matCount);
                if (skinTextures == null)
                {
                    return;
                }

                CreateABC(model, skinTextures);
            }
        }
    }

    private void CreateABC(ABCModel model, List<string> skinTexturesWithFullPath)
    {
        List<string> skinTexturesWithRelativePath = skinTexturesWithFullPath.Select(
            x => Path.GetRelativePath(this.szProjectPath, x))
            .ToList();

        var abcGameObject = ABCToUnity.CreateObjectFromABC(
            this.szFileName,
            skinTexturesWithRelativePath,
            this.szProjectPath);
    }

    private List<string> GetDTXSkins(int matCount)
    {
        ExtensionFilter[] efExtensionFiler = new[] { new ExtensionFilter("Lithtech Texture DTX", "dtx") };

        // Open file
        string[] pathToDTXFiles = StandaloneFileBrowser.OpenFilePanel("Open File", "", efExtensionFiler, false);
        if (pathToDTXFiles.Length == 0)
        {
            return null;
        }

        var skinTextures = pathToDTXFiles.ToList();
        while (skinTextures.Count < matCount)
        {
            var additionalSkinTextures = GetDTXSkins(matCount - skinTextures.Count);
            if (additionalSkinTextures == null)
            {
                return skinTextures;
            }

            skinTextures.AddRange(additionalSkinTextures);
        }

        return skinTextures;
    }

    public void OnEnable()
    {
        UIActionManager.OnPreLoadLevel += OnPreLoadLevel;
        UIActionManager.OnPreLoadABC += OnPreLoadABC;
        UIActionManager.OnPreClearLevel += ClearLevel;
        UIActionManager.OnOpenDefaultLevel += OnOpenDefaultLevel;
        UIActionManager.OnConvertEverything += OnConvertEverything;
    }

    public void OnDisable()
    {
        UIActionManager.OnPreLoadLevel -= OnPreLoadLevel;
        UIActionManager.OnPreLoadABC -= OnPreLoadABC;
        UIActionManager.OnPreClearLevel -= ClearLevel;
        UIActionManager.OnOpenDefaultLevel -= OnOpenDefaultLevel;
        UIActionManager.OnConvertEverything -= OnConvertEverything;
    }

    private void OnConvertEverything()
    {
        ClearLevel();
        GetStatsOnEverything();
        ConvertEverything();
    }

    private void OnOpenDefaultLevel()
    {
        ClearLevel();
        OpenDefaultDAT();
    }

    private void OnPreLoadLevel()
    {
        ClearLevel();
        OpenDAT();
    }

    private void OnPreLoadABC()
    {
        ClearLevel();
        OpenABC();
    }

    public void ClearLevel()
    {
        ResetAllProperties();
    }

    private void ResetAllProperties()
    {
        szProjectPath = String.Empty;
        szFileName = String.Empty;
        nVersion = 0;
        Resources.UnloadUnusedAssets();

        UIActionManager.OnReset?.Invoke();
    }

    private uint ReadDATVersion(ref BinaryReader binaryReader)
    {
        uint version = binaryReader.ReadUInt32();
        binaryReader.BaseStream.Position = 0; //reset back to start of the file so that our DAT reader can read it
        return version;
    }


    public ModelDefinition CreateModelDefinition(string szName, ModelType type, Dictionary<string, object> objectInfo = null)
    {
        //Bail out!
        if (type == ModelType.None)
            return null;

        // TTTT Fix this:
        //if (szName != "Cot0" && szName != "Bookcase1")
        //{
        //    return null;
        //}

        ModelDefinition modelDefinition = new ModelDefinition();
        INIParser ini = new INIParser();

        if (objectInfo != null)
        {
            if (objectInfo.ContainsKey("MoveToFloor"))
            {
                modelDefinition.bMoveToFloor = (bool)objectInfo["MoveToFloor"];
            }
            if (objectInfo.ContainsKey("ForceNoMoveToGround"))
            {
                modelDefinition.bMoveToFloor = !(bool)objectInfo["ForceNoMoveToGround"];
            }
            if (objectInfo.ContainsKey("HumanOnly"))
            {
                modelDefinition.bMoveToFloor = true;
            }

        }

        if (type == ModelType.Character)
        {
            modelDefinition.modelType = type;
            if (!configButes.ContainsKey(type))
            {
                String szCharButes = "\\Attributes\\CharacterButes.txt";
                //get game config selection
                if (nSelectedGame == (int)Game.DIEHARD)
                {
                    szCharButes = "\\Attributes\\Character.txt";
                }

                if (File.Exists(szProjectPath + szCharButes))
                {
                    ini.Open(szProjectPath + szCharButes);
                    configButes.Add(type, ini); //stuff this away
                }
                else
                {
                    Debug.LogError("Could not find CharacterButes.txt");
                    return null;
                }
            }


            Dictionary<string, string> item = null;

            if (type == ModelType.BodyProp)
            {
                if (objectInfo.ContainsKey("CharacterType"))
                {
                    szName = (string)objectInfo["CharacterType"];
                }
            }

            item = configButes[type].GetSectionsByName(szName);

            if (item == null)
                return null;

            foreach (var key in item)
            {

                if (key.Key == "DefaultModel")
                {
                    modelDefinition.szModelFileName = key.Value.Replace("\"", "");
                }
                if (key.Key == "DefaultSkin0")
                {
                    modelDefinition.szModelTextureName.Add("Skins\\Characters\\" + key.Value.Trim('"'));
                }
                if (key.Key == "DefaultSkin1")
                {
                    modelDefinition.szModelTextureName.Add("Skins\\Characters\\" + key.Value.Trim('"'));
                }
                if (key.Key == "DefaultSkin2")
                {
                    modelDefinition.szModelTextureName.Add("Skins\\Characters\\" + key.Value.Trim('"'));
                }
                if (key.Key == "DefaultSkin3")
                {
                    modelDefinition.szModelTextureName.Add("Skins\\Characters\\" + key.Value.Trim('"'));
                }
            }

            modelDefinition.szModelFilePath = szProjectPath + "\\Models\\Characters\\" + modelDefinition.szModelFileName;
            modelDefinition.FitTextureList();

            return modelDefinition;
        }
        else if (type == ModelType.Pickup)
        {
            modelDefinition.modelType = type;
            if (!configButes.ContainsKey(type))
            {
                IDATReader reader = (IDATReader)DatReader;

                var nVersion = reader.GetVersion();

                string szButeFile = String.Empty;

                if (nVersion > 66)
                {
                    szButeFile = szProjectPath + "\\Attributes\\PickupButes.txt";
                }
                else
                {
                    szButeFile = szProjectPath + "\\Attributes\\Weapons.txt";
                }


                if (File.Exists(szButeFile))
                {
                    ini.Open(szButeFile);
                    configButes.Add(type, ini); //stuff this away
                }
                else
                {
                    Debug.LogError("Could not find PickupButes.txt");
                    return null;
                }
            }

            foreach (var sections in configButes[type].GetSections)
            {

                var test = sections.Value;

                // check if keys has a name
                if (sections.Value.ContainsKey("Name"))
                {
                    if (sections.Value["Name"].Replace("\"", "") != szName)
                    {
                        continue;
                    }
                    else
                    {

                        string modelName = String.Empty;

                        if (nVersion > 66)
                            configButes[type].ReadValue(sections.Key, "Model", "1x1square.abc");
                        else
                            configButes[type].ReadValue(sections.Key, "HHModel", "1x1square.abc");



                        if (!String.IsNullOrEmpty(modelName))
                        {
                            modelDefinition.szModelFileName = modelName.Replace("\"", "");
                            modelDefinition.szModelFilePath = Path.Combine(szProjectPath, modelName.Replace("\"", ""));
                        }

                        //get skins, could be up to 4, but not always defined.. FUN

                        if (nVersion > 66)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                string szSkinString = String.Format("Skin{0}", i);

                                modelDefinition.szModelTextureName.Add(configButes[type].ReadValue(sections.Key, szSkinString, "").Replace("\"", String.Empty));

                            }
                            modelDefinition.FitTextureList();
                        }
                        else
                        {
                            modelDefinition.szModelTextureName.Add(configButes[type].ReadValue(sections.Key, "HHSkin", "").Replace("\"", String.Empty));
                        }

                    }
                    return modelDefinition;
                }

            }

        }
        else if (type == ModelType.WeaponItem)
        {
            modelDefinition.modelType = type;
            if (!configButes.ContainsKey(type))
            {
                IDATReader reader = (IDATReader)DatReader;

                var nVersion = reader.GetVersion();

                string szButeFile = String.Empty;

                if (nVersion > 66)
                {
                    szButeFile = szProjectPath + "\\Attributes\\PickupButes.txt";
                }
                else
                {
                    szButeFile = szProjectPath + "\\Attributes\\Weapons.txt";
                }


                if (File.Exists(szButeFile))
                {
                    ini.Open(szButeFile);
                    configButes.Add(type, ini); //stuff this away
                }
                else
                {
                    Debug.LogError("Could not find PickupButes.txt");
                    return null;
                }
            }

            foreach (var sections in configButes[type].GetSections)
            {

                var test = sections.Value;

                // check if keys has a name
                if (sections.Value.ContainsKey("Name"))
                {
                    if (sections.Value["Name"].Replace("\"", "") != szName)
                    {
                        continue;
                    }
                    else
                    {

                        string modelName = String.Empty;

                        if (nVersion > 66)
                            configButes[type].ReadValue(sections.Key, "Model", "1x1square.abc");
                        else
                            configButes[type].ReadValue(sections.Key, "HHModel", "1x1square.abc");



                        if (!String.IsNullOrEmpty(modelName))
                        {
                            modelDefinition.szModelFileName = modelName.Replace("\"", "");
                            modelDefinition.szModelFilePath = Path.Combine(szProjectPath, modelName.Replace("\"", ""));
                        }

                        //get skins, could be up to 4, but not always defined.. FUN

                        if (nVersion > 66)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                string szSkinString = String.Format("Skin{0}", i);

                                modelDefinition.szModelTextureName.Add(configButes[type].ReadValue(sections.Key, szSkinString, "").Replace("\"", String.Empty));

                            }
                            modelDefinition.FitTextureList();
                        }
                        else
                        {
                            modelDefinition.szModelTextureName.Add(configButes[type].ReadValue(sections.Key, "HHSkin", "").Replace("\"", String.Empty));
                        }

                    }
                    return modelDefinition;
                }

            }

        }
        else if (type == ModelType.Prop)
        {
            modelDefinition.modelType = type;

            //find the key "Filename" in the dictionary
            string szFilename = (string)objectInfo["Filename"];

            
            if(!objectInfo.ContainsKey("Skin"))
            {
                Debug.LogError("No skin found for prop");
                return null;
            }


            string szSkins = (string)objectInfo["Skin"];
 

            string[] szSkinArray = szSkins.Split(';');

            foreach (var szSkin in szSkinArray)
            {
                modelDefinition.szModelTextureName.Add(szSkin);
            }

            modelDefinition.szModelFileName = szFilename;
            modelDefinition.szModelFilePath = Path.Combine(szProjectPath, modelDefinition.szModelFileName);

            if (objectInfo.ContainsKey("Chromakey"))
            {
                modelDefinition.bChromakey = (bool)objectInfo["Chromakey"];
            }

            return modelDefinition;

        }
        else if (type == ModelType.PropType)
        {
            modelDefinition.modelType = type;

            if (!configButes.ContainsKey(type))
            {
                String szPropToLoad = "\\Attributes\\PropTypes.txt";

                if(nSelectedGame == (int)Game.DIEHARD)
                {
                    szPropToLoad = "\\Attributes\\Prop.txt";
                }

                if (File.Exists(szProjectPath + szPropToLoad))
                {
                    ini.Open(szProjectPath + szPropToLoad);
                    configButes.Add(type, ini); //stuff this away
                }
                else
                {
                    Debug.LogError("Could not find PropTypes.txt");
                    return null;
                }
            }

            string szType = objectInfo["Type"].ToString();



            foreach (var sections in configButes[type].GetSections)
            {

                // check if keys has a name
                if (sections.Value.ContainsKey("Type"))
                {
                    if (sections.Value["Type"].Replace("\"", "") != szType)
                    {
                        continue;
                    }
                    else
                    {
                        string modelName = configButes[type].ReadValue(sections.Key, "Filename", "1x1square.abc");

                        if (!String.IsNullOrEmpty(modelName))
                        {
                            modelDefinition.szModelFileName = modelName.Replace("\"", "");
                            modelDefinition.szModelFilePath = Path.Combine(szProjectPath, modelName.Replace("\"", ""));
                        }

                        //get skins, could be up to 4, but not always defined.. FUN
                        string szSkins = configButes[type].ReadValue(sections.Key, "Skin", "");

                        string[] szSkinArray = szSkins.Split(';');

                        foreach (string szSkin in szSkinArray)
                        {
                            modelDefinition.szModelTextureName.Add(szSkin.Replace("\"", ""));
                        }
                    }

                    string szMoveToFloorString = configButes[type].ReadValue(sections.Key, "MoveToFloor", "0");

                    if (szMoveToFloorString == "1")
                    {
                        modelDefinition.bMoveToFloor = true;
                    }
                    else
                    {
                        modelDefinition.bMoveToFloor = false;
                    }

                    modelDefinition.bChromakey = configButes[type].ReadValue(sections.Key, "Chromakey", false);

                    return modelDefinition;
                }

            }
        }
        else if (type == ModelType.Princess)
        {
            modelDefinition.modelType = type;

            modelDefinition.szModelFileName = "MODELS\\Princess.abc";
            modelDefinition.szModelTextureName.Add("SKINS\\PRINCESSPINK.DTX");
            modelDefinition.szModelFilePath = Path.Combine(szProjectPath, modelDefinition.szModelFileName);

            if (objectInfo.ContainsKey("Chromakey"))
            {
                modelDefinition.bChromakey = (bool)objectInfo["Chromakey"];
            }

            return modelDefinition;
        }
        else if (type == ModelType.Monster)
        {
            modelDefinition.modelType = type;

            string szFilename = (string)objectInfo["Filename"];
            modelDefinition.szModelFileName = szFilename;

            string skinName = Path.GetFileNameWithoutExtension(szFilename) + ".dtx";
            modelDefinition.szModelTextureName.Add("SKINS\\" + skinName);
            modelDefinition.szModelFilePath = Path.Combine(szProjectPath, modelDefinition.szModelFileName);

            if (objectInfo.ContainsKey("Chromakey"))
            {
                modelDefinition.bChromakey = (bool)objectInfo["Chromakey"];
            }

            return modelDefinition;
        }

        return null;
    }

    private void OpenDefaultDAT()
    {
        this.nSelectedGame = (int)Game.LOMM;
        //
        // OpenDAT("C:\\LoMM\\Data\\Worlds\\_RESCUEATTHERUINS.DAT", "C:\\LoMM\\Data");
        OpenDAT("C:\\LoMM\\Data\\Worlds\\_TEMPLEOFBARK.DAT", "C:\\LoMM\\Data");
    }

    private bool IsOriginalLoMMMap(string filename)
    {
        return filename.Contains("BLOODFEUD.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("CHATEAUESCAPE.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("CULTOFTHESPIDER.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("DIRTYPEWORLDS", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("DRAGONBLADE.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("DRAGONSLAYERS.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("DUNGEONRESCUE.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("DUNGEONSOFDRAGADUNE.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("FAHLTEETOWER.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("FORGOTTENKEEP.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("GAUNTLET.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("HIDEOUT.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("ISLEOFFIRE.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("ONTHERUN.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("SECRETSOFTHESPHINX.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("SPIDERSDEN.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("STONEHAM.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("SWORDINTHESTONE.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("WEDDINGDAY.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("_RESCUEATTHERUINS.DAT", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("_TEMPLEOFBARK.DAT", StringComparison.OrdinalIgnoreCase);

    }

    private string GetBspSummary(BSPModel model)
    {
        return $"WorldName={model.WorldName} | TextureCount={model.TextureCount} | TextureNames.Count={model.TextureNames.Count}";
    }

    private string GetDATSummary(string filename, DATModel datModel)
    {
        string s = $"{Path.GetFileName(filename)}: {datModel.WorldModel.WorldProperties}\r\n";

        var distinctTextureNames = datModel.GetAllBSPTextures();

        s += $"\t\tTexture Count = {distinctTextureNames.Count}\r\n\t\t";
        s += string.Join("\r\n\t\t", distinctTextureNames.OrderBy(x => x));

        var objectTypes = datModel.WorldObjects
            .GroupBy(x => x.ObjectType, StringComparer.OrdinalIgnoreCase)
            .Select(grp => new { ObjectType = grp.Key, Count = grp.Count() })
            .ToList();

        s += "\r\n\r\nObjectTypes:\r\n\t\t";
        s += string.Join("\r\n\t\t", objectTypes.OrderByDescending(x => x.Count).Select(x => $"{x.Count} : {x.ObjectType}"));

        return s;
    }

    private List<string> GetDistinctFileTextures()
    {
        var dtxFiles = Directory.GetFiles(this.szProjectPath, "*.dtx", SearchOption.AllDirectories);
        var sprFiles = Directory.GetFiles(this.szProjectPath, "*.spr", SearchOption.AllDirectories);
        return (dtxFiles.Union(sprFiles)).Select(x => Path.GetRelativePath(this.szProjectPath, x)).ToList();
    }

    private List<DATModel> GetDATModels()
    {
        //var originalDATFiles = datFiles.Where(x => IsOriginalLoMMMap(x)).ToList();
        //originalDATFiles = originalDATFiles.Where(x => x.Contains("_RESCUEATTHERUINS.DAT", StringComparison.OrdinalIgnoreCase)).ToList();
        var datFiles = Directory.GetFiles(this.szProjectPath, "*.dat", SearchOption.AllDirectories);
        var datModels = new List<DATModel>();
        foreach (var datFile in datFiles)
        {
            var datModel = DATModelReader.ReadDATModel(datFile, this.szProjectPath, Game.LOMM);
            datModels.Add(datModel);
        }

        return datModels;
    }

    private List<ABCModel> GetABCModels()
    {
        var abcFiles = Directory.GetFiles(this.szProjectPath, "*.abc", SearchOption.AllDirectories);
        var abcModels = new List<ABCModel>();
        foreach (var abcFile in abcFiles)
        {
            var abcModel = ABCModelReader.ReadABCModel(abcFile, this.szProjectPath);
            if (abcModel != null)
            {
                abcModels.Add(abcModel);
            }
        }

        return abcModels;
    }

    private List<string> ExtractAllMatches(byte[] data, string searchString)
    {
        var results = new List<string>();
        int searchLength = searchString.Length;

        int currentIndex = 0;
        while (currentIndex < (data.Length - searchLength))
        {
            string fragment = Encoding.UTF8.GetString(data, currentIndex, searchLength);
            if (fragment.Equals(searchString, StringComparison.OrdinalIgnoreCase))
            {
                int startOfStringIndex = currentIndex - 1;
                while (startOfStringIndex >= 0 && data[startOfStringIndex] != 0)
                {
                    startOfStringIndex--;
                }

                var match = Encoding.UTF8.GetString(data, startOfStringIndex + 1, currentIndex - startOfStringIndex - 1 + searchLength);
                // Exclude matches that are JUST the searchString.
                if (match.Length > searchLength)
                {
                    results.Add(match);
                }

                currentIndex += searchLength;
            }
            else
            {
                currentIndex++;
            }
        }

        return results;
    }

    private List<string> GetLTOTextures()
    {
        var data = File.ReadAllBytes(Path.Combine(this.szProjectPath, "object.lto"));

        var sprNames = ExtractAllMatches(data, ".spr");
        var dtxNames = ExtractAllMatches(data, ".dtx");

        var allNames = sprNames.Concat(dtxNames).ToList();
        allNames = allNames.Where(x => 
            !string.Equals(x, "InvalidPV.dtx", StringComparison.OrdinalIgnoreCase) 
            && !x.StartsWith("%s"))
            .ToList();

        return allNames;
    }

    private List<string> GetAllTextures(List<DATModel> datModels)
    {
        var bspTextures = datModels.SelectMany(x => x.GetAllBSPTextures())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filenames = datModels
            .SelectMany(x => x.GetAllWorldObjectFilenames())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var skins = datModels
            .SelectMany(x => x.GetAllWorldObjectSkins())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ltoTextures = GetLTOTextures();

        var allTextures = (bspTextures.Concat(filenames).Concat(skins))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(x =>
                {
                    var extension = Path.GetExtension(x);
                    return string.Equals(extension, ".dtx", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(extension, ".spr", StringComparison.OrdinalIgnoreCase);
                })
            .ToList();

        return allTextures;
    }

    private void ConvertEverything()
    {
        this.nSelectedGame = (int)Game.LOMM;
        this.szProjectPath = "C:\\temp\\LOMMConverted\\OriginalUnrezzed";

        var datModels = GetDATModels();
        var abcModels = GetABCModels();
    }

    private void GetStatsOnEverything()
    {
        this.nSelectedGame = (int)Game.LOMM;
        this.szProjectPath = "C:\\temp\\LOMMConverted\\OriginalUnrezzed";
        
        var datModels = GetDATModels();
        var abcModels = GetABCModels();

        var distinctFileTextures = GetDistinctFileTextures();
        var distinctTextureNames = GetAllTextures(datModels);

        var objectTypes = datModels.SelectMany(x => x.WorldObjects)
            .GroupBy(x => x.ObjectType, StringComparer.OrdinalIgnoreCase)
            .Select(grp => new { ObjectType = grp.Key, Count = grp.Count() })
            .ToList();

        var unusedFiles = distinctFileTextures.Except(distinctTextureNames, StringComparer.OrdinalIgnoreCase).ToList();

        var texturesNotFound = distinctTextureNames.Except(distinctFileTextures, StringComparer.OrdinalIgnoreCase).ToList();

        var filenames = datModels
            .SelectMany(x => x.GetAllWorldObjectFilenames())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var abcFilesNotUsed = abcModels.Select(x => x.RelativePathToABCFileLowercase).Except(filenames, StringComparer.OrdinalIgnoreCase).ToList();

        string s = string.Empty;
        s += "ObjectTypes:\r\n\t\t" + string.Join("\r\n\t\t", objectTypes.OrderByDescending(x => x.Count).Select(x => $"{x.Count} : {x.ObjectType}"));
        s += "\r\n\r\n";
        s += $"Unused textures:\r\n\t\t" + string.Join("\r\n\t\t", unusedFiles);
        s += "\r\n\r\n";
        s += $"Textures not found:\r\n\t\t" + string.Join("\r\n\t\t", texturesNotFound);
        s += "\r\n\r\n";
        s += $"ABC Files not referenced by a DAT:\r\n\t\t" + string.Join("\r\n\t\t", abcFilesNotUsed);

        File.WriteAllText("C:\\temp\\Lomm.txt", s);

        Debug.Log("Created file: " + "C:\\temp\\Lomm.txt");
    }

    public void Quit()
    {
        Application.Quit();
    }
}