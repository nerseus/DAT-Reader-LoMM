using UnityEngine;
using System.IO;

public class TextureConverter : BaseConverter
{
    public void OnEnable()
    {
        UIActionManager.OnExportTextures += ExportTextures;
    }

    public void OnDisable()
    {
        UIActionManager.OnExportTextures -= ExportTextures;
    }

    private void ExportTextures()
    {
        var files = Directory.GetFiles(SourceRootFolder, "*.dtx", SearchOption.AllDirectories);
        foreach(var file in files)
        {
            var relativePath = Path.GetRelativePath(SourceRootFolder, file);
            var dtxModel = DTXModelReader.ReadDTXModel(file, relativePath);
            var unityDtx = DTXConverter.ConvertDTX(dtxModel);
            if (unityDtx == null)
            {
                Debug.LogError($"Got back null for file {file}");
                continue;
            }

            var newFilePathAndName = Path.ChangeExtension(Path.Combine(DestinationRootFolder, "Textures", relativePath), ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(newFilePathAndName));

            byte[] pngBytes = unityDtx.Texture2D.EncodeToPNG();
            File.WriteAllBytes(newFilePathAndName, pngBytes);
        }

        Debug.Log($"Finished! Created {files.Length} files.");
    }
}