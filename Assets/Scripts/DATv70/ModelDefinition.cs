using System.Collections.Generic;
using UnityEngine;

public class ModelDefinition
{
    public string szModelFileName = string.Empty;
    public string szModelFilePath = string.Empty;
    public List<string> szModelTextureName = new List<string>();
    public ModelType modelType = ModelType.None;
    public ABCModel model = new ABCModel();
    public GameObject rootObject;
    public bool bMoveToFloor = false;
    public bool bChromakey = false;

    public static string[] AVP2RandomCharacterGameStartPoint = { "BaseHuman", "Tamiko", "Eisenberg", "RailGunner", "Obrian", "Predalien", "BaseAlien", "BasePredator", "SmartGunner", "Grenadier", "LightPredator", "Merc2", "Merc3", "Rykov" };

    public void FitTextureList()
    {
        szModelTextureName.RemoveAll(string.IsNullOrEmpty);
    }
}
