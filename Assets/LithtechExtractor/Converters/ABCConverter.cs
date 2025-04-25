using UnityEngine;
using System.IO;

public class ABCConverter : BaseConverter
{
    public void OnEnable()
    {
        UIActionManager.OnExportABCs += ExportABCs;
    }

    public void OnDisable()
    {
        UIActionManager.OnExportABCs -= ExportABCs;
    }

    private void ExportABCs()
    {
        var files = Directory.GetFiles(SourceRootFolder, "*.abc", SearchOption.AllDirectories);
        foreach(var file in files)
        {
            var relativePath = Path.GetRelativePath(SourceRootFolder, file);
            var dtxModel = DTXModelReader.ReadDTXModel(SourceRootFolder, relativePath);
            var unityDtx = DTXConverter.ConvertDTX(dtxModel);
            if (unityDtx == null)
            {
                Debug.LogError($"Got back null for file {file}");
                continue;
            }

            var newFilePathAndName = Path.ChangeExtension(Path.Combine(DestinationRootFolder, relativePath), ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(newFilePathAndName));

            byte[] pngBytes = unityDtx.Texture2D.EncodeToPNG();
            File.WriteAllBytes(newFilePathAndName, pngBytes);
        }
    }
}