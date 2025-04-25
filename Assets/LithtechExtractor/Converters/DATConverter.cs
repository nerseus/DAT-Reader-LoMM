using UnityEngine;
using System.IO;
using LithFAQ;

public class DATConverter : BaseConverter
{
    public void OnEnable()
    {
        UIActionManager.OnExportDATs += ExportDATs;
    }

    public void OnDisable()
    {
        UIActionManager.OnExportDATs -= ExportDATs;
    }

    private void ExportDATs()
    {
        var datModel = DATModelReader.ReadDATModel(DATFile, SourceRootFolder, Game.LOMM);
        Debug.Log($"Read: {DATFile}\r\n" 
            + $"Version = {datModel.Version}\r\n"
            + $"WorldProperties = {datModel.WorldModel.WorldProperties}\r\n"
            + $"BSP Model Count = {datModel.BSPModels.Count}\r\n"
            + $"Object Count = {datModel.WorldObjects.Count}\r\n"
            );
   }
}