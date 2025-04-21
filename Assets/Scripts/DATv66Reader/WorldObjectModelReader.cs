using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static LithFAQ.LTTypes;
using static LithFAQ.LTUtils;

namespace LithFAQ
{
    public static class WorldObjectModelReader
    {
        public static List<WorldObjectModel> ReadWorldObjects(BinaryReader binaryReader)
        {
            var worldObjects = new List<WorldObjectModel>();

            var objectCount = binaryReader.ReadInt32();

            for (int i = 0; i < objectCount; i++)
            {
                WorldObjectModel theObject = new WorldObjectModel();
                
                theObject.dataOffset = binaryReader.BaseStream.Position; // store our offset in our .dat

                theObject.dataLength = binaryReader.ReadInt16(); // Read our object datalength

                var dataLength = binaryReader.ReadInt16(); //read out property length

                theObject.ObjectType = ReadString(dataLength, ref binaryReader); // read our name

                theObject.objectEntries = binaryReader.ReadInt32();// read how many properties this object has

                string realObjectName = string.Empty;
                for (int t = 0; t < theObject.objectEntries; t++)
                {
                    var nObjectPropertyDataLength = binaryReader.ReadInt16();
                    string szPropertyName = ReadString(nObjectPropertyDataLength, ref binaryReader);

                    // Sometimes an object has the same key twice but different spelling - like RayHit or Rayhit.
                    // They seem to have the same value so just remove the key if found and let it get replaced below.
                    if (theObject.options.ContainsKey(szPropertyName))
                    {
                        theObject.options.Remove(szPropertyName);
                        var prop = theObject.Properties.FirstOrDefault(x => x.Name.Equals(szPropertyName, System.StringComparison.OrdinalIgnoreCase));
                        if (prop != null)
                        {
                            theObject.Properties.Remove(prop);
                        }
                    }

                    PropType propType = (PropType)binaryReader.ReadByte();

                    theObject.objectEntryFlag.Add(binaryReader.ReadInt32()); //read the flag

                    switch (propType)
                    {
                        case PropType.PT_STRING:
                            theObject.objectEntryStringDataLength.Add(binaryReader.ReadInt16()); //read the string length plus the data length
                            nObjectPropertyDataLength = binaryReader.ReadInt16();

                            //Read the string
                            string stringVal = ReadString(nObjectPropertyDataLength, ref binaryReader);
                            theObject.options.Add(szPropertyName, stringVal);
                            theObject.AddProperty(propType, szPropertyName, stringVal);
                            break;

                        case PropType.PT_VECTOR:
                            nObjectPropertyDataLength = binaryReader.ReadInt16();
                            //Get our float data
                            LTVector tempVec = ReadLTVector(ref binaryReader);
                            //Add our object to the Dictionary
                            theObject.options.Add(szPropertyName, tempVec);
                            theObject.AddProperty(propType, szPropertyName, tempVec);
                            break;

                        case PropType.PT_ROTATION:
                            //Get our data length
                            nObjectPropertyDataLength = binaryReader.ReadInt16();
                            //Get our float data
                            LTRotation tempRot = ReadLTRotation(ref binaryReader);
                            //Add our object to the Dictionary
                            theObject.options.Add(szPropertyName, tempRot);
                            theObject.AddProperty(propType, szPropertyName, tempRot);
                            break;
                        case PropType.PT_UINT:
                            // Read the "size" of what we should read.
                            // For UINT the nObjectPropertyDataLength should always be 4.
                            nObjectPropertyDataLength = binaryReader.ReadInt16();

                            //Add our object to the Dictionary
                            uint uIntVal = binaryReader.ReadUInt32();
                            theObject.options.Add(szPropertyName, uIntVal);
                            theObject.AddProperty(propType, szPropertyName, uIntVal);
                            break;
                        case PropType.PT_BOOL:
                            nObjectPropertyDataLength = binaryReader.ReadInt16();
                            bool boolVal = ReadBool(ref binaryReader);
                            theObject.options.Add(szPropertyName, boolVal);
                            theObject.AddProperty(propType, szPropertyName, boolVal);
                            break;
                        case PropType.PT_REAL:
                            nObjectPropertyDataLength = binaryReader.ReadInt16();

                            //Add our object to the Dictionary
                            var floatVal = ReadReal(ref binaryReader);
                            theObject.options.Add(szPropertyName, floatVal);
                            theObject.AddProperty(propType, szPropertyName, floatVal);
                            break;
                        case PropType.PT_COLOR:
                            nObjectPropertyDataLength = binaryReader.ReadInt16();
                            //Get our float data
                            LTVector tempCol = ReadLTVector(ref binaryReader);
                            //Add our object to the Dictionary
                            theObject.options.Add(szPropertyName, tempCol);
                            theObject.AddProperty(propType, szPropertyName, tempCol);
                            break;
                        default:
                            Debug.LogError("Unknown prop type: " + propType);
                            break;
                    }
                }

                worldObjects.Add(theObject);
            }
            
            return worldObjects;
        }
    }
}
