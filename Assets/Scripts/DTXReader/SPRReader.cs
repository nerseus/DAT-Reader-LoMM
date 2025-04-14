using System.IO;

public static class SPRReader
{
    public static UnitySPR LoadSPRModel(string projectPath, string relativePathToSPR)
    {
        string filenameWithFullPath = Path.Combine(projectPath, relativePathToSPR);
        if (!File.Exists(filenameWithFullPath))
        {
            return null;
        }

        using BinaryReader binaryReader = new BinaryReader(File.Open(filenameWithFullPath, FileMode.Open));

        var numDTX = binaryReader.ReadInt32();

        // Jump past the header and read the first texture.
        binaryReader.BaseStream.Position = 20; 
        var unitySPR = new UnitySPR
        {
            RelativePathToSprite = relativePathToSPR,
            DTXPaths = new string[numDTX]
        };

        for (int i = 0; i < numDTX; i++)
        {
            int strLength = binaryReader.ReadUInt16();
            byte[] strData = binaryReader.ReadBytes(strLength);
            string dtxPath = System.Text.Encoding.UTF8.GetString(strData);
            unitySPR.DTXPaths[i] = dtxPath;
        }

        binaryReader.Close();

        return unitySPR;
    }
}