using System.Collections.Generic;
using UnityEngine;

public class DTXMaterialLibrary
{
    public Dictionary<string, string> fileNameAndPath { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, Material> materials { get; set; } = new Dictionary<string, Material>();
    public Dictionary<string, Texture2D> textures { get; set; } = new Dictionary<string, Texture2D>();
    public Dictionary<string, TextureSize> texSize { get; set; } = new Dictionary<string, TextureSize>();
}
