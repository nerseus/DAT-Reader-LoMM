using UnityEngine;

public class Texture2DInfoModel
{
    public Texture2D Texture2D { get; set; }
    public bool UseTransparency { get; set; }

    public Texture2DInfoModel(Texture2D texture, bool useTransparency)
    {
        Texture2D = texture;
        UseTransparency = useTransparency;
    }
}