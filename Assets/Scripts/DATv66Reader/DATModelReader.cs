using System;
using System.IO;
using UnityEngine;

namespace LithFAQ
{
    public static class DATModelReader
    {
        private static int ReadDATVersion(BinaryReader binaryReader)
        {
            binaryReader.BaseStream.Position = 0;
            int version = binaryReader.ReadInt32();
            return version;
        }

        private static BinaryReader GetBinaryReader(string datFilename, string resourcePath)
        {
            if (!File.Exists(datFilename))
            {
                Debug.LogError($"Filename invalid. Could not find {datFilename}");
                return null;
            }

            if (!Directory.Exists(resourcePath))
            {
                Debug.LogError($"Resource path invalid. Could not find {resourcePath}");
                return null;
            }

            var data = File.ReadAllBytes(datFilename);
            if (data == null || data.Length == 0)
            {
                Debug.LogError($"File missing or empty. Filename {datFilename}");
                return null;
            }

            var memoryStream = new MemoryStream(data);
            var binaryReader = new BinaryReader(memoryStream);

            return binaryReader;
        }

        public static DATModel ReadDATModel(string datFilename, string resourcePath, Game gameType)
        {
            using var binaryReader = GetBinaryReader(datFilename, resourcePath);
            if (binaryReader == null)
            {
                return null;
            }

            int version = ReadDATVersion(binaryReader);
            if (version != DATVersions.Version66)
            {
                Debug.LogError($"Invalid DAT version {version}. Expected {DATVersions.Version66}.");
                return null;
            }

            var datModel = new DATModel(version);
            datModel.Filename = datFilename;
            datModel.WorldModel = WorldModelReader.ReadWorldModel(binaryReader, datModel.Version);
            WorldTreeReader.SkipWorldTree(binaryReader);

            int nextWorldModelPosition = 0;
            var modelCount = binaryReader.ReadInt32();
            for (int i = 0; i < modelCount; i++)
            {
                nextWorldModelPosition = binaryReader.ReadInt32();

                // Skip next 32 bytes.
                binaryReader.ReadBytes(32);

                try
                {
                    var bspModel = BSPModelReader.LoadBSPModel(binaryReader, version, gameType);
                    datModel.BSPModels.Add(bspModel);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception at position {binaryReader.BaseStream.Position}: {e.Message}");
                }

                binaryReader.BaseStream.Position = nextWorldModelPosition;
            }

            binaryReader.BaseStream.Position = datModel.WorldModel.Header.ObjectDataPos;
            datModel.WorldObjects = WorldObjectModelReader.ReadWorldObjects(binaryReader);

            binaryReader.BaseStream.Close();

            return datModel;
        }
    }
}
