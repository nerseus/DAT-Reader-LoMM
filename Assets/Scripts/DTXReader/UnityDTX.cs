using UnityEngine;

public class UnityDTX
{
    public DTXHeader Header { get; set; }
    public string RelativePathToDTX { get; set; }
    public Material Material { get; set; }
    public Texture2D  Texture2D { get; set; }
    public TextureSize TextureSize { get; set; }
}