using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ABCModel
{
    public string Name { get; set; }
    public int Version { get; set; }
    public string CommandString { get; set; }
    public float InternalRadius { get; set; }
    public int LODCount { get; set; }
    public List<float> LODDistances { get; set; }
    public List<PieceModel> Pieces { get; set; }
    public string RelativePathToABCFileLowercase { get; set; }
    public Material[] Materials { get; set; }

    public int GetMaterialCount()
    {
        if (Pieces == null || Pieces.Count == 0)
        {
            return 0;
        }

        // The number of materials is the number of unique/distinct MaterialIndexes.
        return Pieces.Select(x => x.MaterialIndex).Distinct().Count();
    }

    public ushort GetMaxMaterialIndex()
    {
        if (Pieces == null || Pieces.Count == 0)
        {
            return 0;
        }

        // The number of materials is the number of unique/distinct MaterialIndexes.
        return Pieces.Max(x => x.MaterialIndex);
    }
}
