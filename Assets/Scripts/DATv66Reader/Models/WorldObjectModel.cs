using LithFAQ;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static LithFAQ.LTTypes;

public class WorldObjectModel
{
    public Dictionary<string, object> options { get; set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    public List<WorldObjectPropertyModel> Properties { get; set; } = new List<WorldObjectPropertyModel>();
    public void AddProperty(PropType propType, string name, string val) { Properties.Add(new WorldObjectPropertyModel { StringValue = val, Name = name, PropType = propType }); }
    public void AddProperty(PropType propType, string name, Vector3 val) { Properties.Add(new WorldObjectPropertyModel { VectorValue = val, Name = name, PropType = propType }); }
    public void AddProperty(PropType propType, string name, float val) { Properties.Add(new WorldObjectPropertyModel { FloatValue = val, Name = name, PropType = propType }); }
    public void AddProperty(PropType propType, string name, uint val) { Properties.Add(new WorldObjectPropertyModel { UIntValue = val, Name = name, PropType = propType }); }
    public void AddProperty(PropType propType, string name, bool val) { Properties.Add(new WorldObjectPropertyModel { BoolValue = val, Name = name, PropType = propType }); }
    public void AddProperty(PropType propType, string name, LTRotation val) { Properties.Add(new WorldObjectPropertyModel { QuatValue = new Quaternion { w = val.W, x = val.X, y = val.Y, z = val.Z }, Name = name, PropType = propType }); }

    public string ObjectType { get; set; }
    public short dataLength { get; set; }
    public long dataOffset { get; set; }
    public int objectEntries { get; set; }
    public List<int> objectEntryFlag { get; set; } = new List<int>();
    public List<short> objectEntryStringDataLength { get; set; } = new List<short>();

    // ******************************************************************************************
    // ** Properties below are set in FlattenProperties
    // ******************************************************************************************
    private bool flattened = false;
    private string name;
    private string skyObjectName;
    private LTTypes.LTVector position;
    private LTTypes.LTVector dims;
    private LTTypes.LTRotation rotation;
    private bool hasGravity;
    private string weaponType;
    private float scale;
    private string originalFilename;
    private string filename;
    private string filenameLowercase;
    private bool isABC;
    private string skin;
    private string allSkinsPathsLowercase;
    private List<string> skinsLowercase;

    public string Name { get{ FlattenProperties(); return name; } }
    public string SkyObjectName { get { FlattenProperties(); return skyObjectName; } }
    public LTTypes.LTVector Position { get { FlattenProperties(); return position; } }
    public LTTypes.LTRotation Rotation { get { FlattenProperties(); return rotation; } }
    public bool HasGravity { get { FlattenProperties(); return hasGravity; } }
    public string WeaponType { get { FlattenProperties(); return weaponType; } }
    public float Scale { get { FlattenProperties(); return scale; } }
    public string OriginalFilename { get { FlattenProperties(); return originalFilename; } }
    public string Filename { get { FlattenProperties(); return filename; } }
    public string FilenameLowercase { get { FlattenProperties(); return filenameLowercase; } }
    public bool IsABC { get { FlattenProperties(); return isABC; } }
    public string Skin { get { FlattenProperties(); return skin; } }
    public string AllSkinsPathsLowercase { get { FlattenProperties(); return allSkinsPathsLowercase; } }
    public List<string> SkinsLowercase { get { FlattenProperties(); return skinsLowercase; } }
    public LTTypes.LTVector Dims { get { FlattenProperties(); return dims; } }

    private bool GetBoolValue(string propName)
    {
        return options.TryGetValue(propName, out var value) && value is bool boolVal
            ? boolVal
            : false;
    }

    private float GetFloatValue(string propName)
    {
        return options.TryGetValue(propName, out var value) && value is float floatVal
            ? floatVal
            : default(float);
    }

    private string GetStringValue(string propName)
    {
        return options.TryGetValue(propName, out var value)
            ? value?.ToString()
            : null;
    }

    private LTTypes.LTVector GetVectorValue(string propName)
    {
        return options.TryGetValue(propName, out var value) && value is LTTypes.LTVector vec
            ? vec
            : default(LTTypes.LTVector);
    }

    private LTTypes.LTRotation GetRotationValue(string propName)
    {
        return options.TryGetValue(propName, out var value) && value is LTTypes.LTRotation rot
            ? rot
            : default(LTTypes.LTRotation);
    }

    public void FlattenProperties()
    {
        if (flattened) return;

        name = GetStringValue("Name");
        skyObjectName = GetStringValue("SkyObjectName");
        position = GetVectorValue("Pos");
        dims = GetVectorValue("Dims");
        rotation = GetRotationValue("Rotation");
        hasGravity = GetBoolValue("Gravity");
        weaponType = GetStringValue("WeaponType");
        scale = GetFloatValue("Scale");
        originalFilename = GetStringValue("Filename");
        if (originalFilename != null)
        {
            filename = Path.Combine(originalFilename, string.Empty);
            filenameLowercase = filename.ConvertFolderSeperators().ToLower();
        }
        skin = GetStringValue("Skin");

        skinsLowercase = new List<string>();

        if (!string.IsNullOrWhiteSpace(skin))
        {
            allSkinsPathsLowercase = skin.ConvertFolderSeperators().ToLower();
            var splitSkins = skin.Split(";");
            foreach (var splitItem in splitSkins)
            {
                var item = (splitItem ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(item))
                {
                    skinsLowercase.Add(item.ConvertFolderSeperators().ToLower());
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(filename))
        {
            isABC = Path.GetExtension(filename).ToLower() == ".abc";
        }
    }
}
