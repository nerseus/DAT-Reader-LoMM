using System.Collections.Generic;

public class Piece
{
    public ushort MaterialIndex;
    public float SpecularPower;
    public float SpecularScale;
    public float LodWeight;
    public ushort Padding;
    public string Name;
    public List<LOD> LODs;
}