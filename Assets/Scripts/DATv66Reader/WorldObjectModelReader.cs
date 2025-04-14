using System.Collections.Generic;
using System.IO;
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

                theObject.objectName = ReadString(dataLength, ref binaryReader); // read our name

                theObject.objectEntries = binaryReader.ReadInt32();// read how many properties this object has

                string realObjectName = string.Empty;
                for (int t = 0; t < theObject.objectEntries; t++)
                {
                    var nObjectPropertyDataLength = binaryReader.ReadInt16();
                    string szPropertyName = ReadString(nObjectPropertyDataLength, ref binaryReader);

                    PropType propType = (PropType)binaryReader.ReadByte();

                    theObject.objectEntryFlag.Add(binaryReader.ReadInt32()); //read the flag

                    switch (propType)
                    {
                        case PropType.PT_STRING:
                            theObject.objectEntryStringDataLength.Add(binaryReader.ReadInt16()); //read the string length plus the data length
                            nObjectPropertyDataLength = binaryReader.ReadInt16();
                            //Read the string
                            theObject.options.Add(szPropertyName, ReadString(nObjectPropertyDataLength, ref binaryReader));
                            break;

                        case PropType.PT_VECTOR:
                            nObjectPropertyDataLength = binaryReader.ReadInt16();
                            //Get our float data
                            LTVector tempVec = ReadLTVector(ref binaryReader);
                            //Add our object to the Dictionary
                            theObject.options.Add(szPropertyName, tempVec);
                            break;

                        case PropType.PT_ROTATION:
                            //Get our data length
                            nObjectPropertyDataLength = binaryReader.ReadInt16();
                            //Get our float data
                            LTRotation tempRot = ReadLTRotation(ref binaryReader);
                            //Add our object to the Dictionary
                            theObject.options.Add(szPropertyName, tempRot);
                            break;
                        case PropType.PT_UINT:
                            // Read the "size" of what we should read.
                            // For UINT the nObjectPropertyDataLength should always be 4.
                            nObjectPropertyDataLength = binaryReader.ReadInt16();
                            //Add our object to the Dictionary
                            theObject.options.Add(szPropertyName, binaryReader.ReadUInt32());
                            break;
                        case PropType.PT_BOOL:
                            nObjectPropertyDataLength = binaryReader.ReadInt16();
                            theObject.options.Add(szPropertyName, ReadBool(ref binaryReader));
                            break;
                        case PropType.PT_REAL:
                            nObjectPropertyDataLength = binaryReader.ReadInt16();
                            //Add our object to the Dictionary
                            theObject.options.Add(szPropertyName, ReadReal(ref binaryReader));
                            break;
                        case PropType.PT_COLOR:
                            nObjectPropertyDataLength = binaryReader.ReadInt16();
                            //Get our float data
                            LTVector tempCol = ReadLTVector(ref binaryReader);
                            //Add our object to the Dictionary
                            theObject.options.Add(szPropertyName, tempCol);
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
