using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static LTTypes;
using static LTUtils;

public static class WorldObjectModelReader
{
    public static List<WorldObjectModel> ReadWorldObjectModels(BinaryReader binaryReader)
    {
        var worldObjects = new List<WorldObjectModel>();

        var objectCount = binaryReader.ReadInt32();

        for (int i = 0; i < objectCount; i++)
        {
            WorldObjectModel worldObject = new WorldObjectModel();

            worldObject.dataOffset = binaryReader.BaseStream.Position; // store our offset in our .dat
            worldObject.dataLength = binaryReader.ReadInt16(); // Read our object datalength
            var dataLength = binaryReader.ReadInt16(); //read out property length
            worldObject.ObjectType = ReadString(dataLength, ref binaryReader); // read our name
            worldObject.OptionsCount = binaryReader.ReadInt32();// read how many properties this object has

            for (int t = 0; t < worldObject.OptionsCount; t++)
            {
                var nObjectPropertyDataLength = binaryReader.ReadInt16();
                string szPropertyName = ReadString(nObjectPropertyDataLength, ref binaryReader);

                // Sometimes an object has the same key twice but different spelling - like RayHit or Rayhit.
                // They seem to have the same value so just remove the key if found and let it get replaced below.
                if (worldObject.options.ContainsKey(szPropertyName))
                {
                    worldObject.options.Remove(szPropertyName);
                    var prop = worldObject.Properties
                        .FirstOrDefault(x => x.Name.Equals(szPropertyName, System.StringComparison.OrdinalIgnoreCase));
                    if (prop != null)
                    {
                        worldObject.Properties.Remove(prop);
                    }
                }

                PropType propType = (PropType)binaryReader.ReadByte();

                worldObject.objectEntryFlag.Add(binaryReader.ReadInt32()); //read the flag

                switch (propType)
                {
                    case PropType.String:
                        worldObject.objectEntryStringDataLength.Add(binaryReader.ReadInt16()); //read the string length plus the data length
                        nObjectPropertyDataLength = binaryReader.ReadInt16();

                        //Read the string
                        string stringVal = ReadString(nObjectPropertyDataLength, ref binaryReader);
                        worldObject.options.Add(szPropertyName, stringVal);
                        worldObject.AddProperty(propType, szPropertyName, stringVal);
                        break;

                    case PropType.Vector:
                        nObjectPropertyDataLength = binaryReader.ReadInt16();

                        //Get our float data
                        LTVector tempVec = ReadLTVector(ref binaryReader);

                        //Add our object to the Dictionary
                        worldObject.options.Add(szPropertyName, tempVec);
                        worldObject.AddProperty(propType, szPropertyName, tempVec);
                        break;

                    case PropType.Rotation:

                        //Get our data length
                        nObjectPropertyDataLength = binaryReader.ReadInt16();

                        //Get our float data
                        LTRotation tempRot = ReadLTRotation(ref binaryReader);

                        //Add our object to the Dictionary
                        worldObject.options.Add(szPropertyName, tempRot);
                        worldObject.AddProperty(propType, szPropertyName, tempRot);
                        break;
                    case PropType.UInt:

                        // Read the "size" of what we should read.
                        // For UINT the nObjectPropertyDataLength should always be 4.
                        nObjectPropertyDataLength = binaryReader.ReadInt16();

                        //Add our object to the Dictionary
                        uint uIntVal = binaryReader.ReadUInt32();
                        worldObject.options.Add(szPropertyName, uIntVal);
                        worldObject.AddProperty(propType, szPropertyName, uIntVal);
                        break;
                    case PropType.Bool:
                        nObjectPropertyDataLength = binaryReader.ReadInt16();
                        bool boolVal = ReadBool(ref binaryReader);
                        worldObject.options.Add(szPropertyName, boolVal);
                        worldObject.AddProperty(propType, szPropertyName, boolVal);
                        break;
                    case PropType.Float:
                        nObjectPropertyDataLength = binaryReader.ReadInt16();

                        //Add our object to the Dictionary
                        var floatVal = ReadReal(ref binaryReader);
                        worldObject.options.Add(szPropertyName, floatVal);
                        worldObject.AddProperty(propType, szPropertyName, floatVal);
                        break;
                    case PropType.Color:
                        nObjectPropertyDataLength = binaryReader.ReadInt16();

                        //Get our float data
                        LTVector tempCol = ReadLTVector(ref binaryReader);

                        //Add our object to the Dictionary
                        worldObject.options.Add(szPropertyName, tempCol);
                        worldObject.AddProperty(propType, szPropertyName, tempCol);
                        break;
                    default:
                        Debug.LogError("Unknown prop type: " + propType);
                        break;
                }
            }

            worldObjects.Add(worldObject);
        }

        return worldObjects;
    }
}
