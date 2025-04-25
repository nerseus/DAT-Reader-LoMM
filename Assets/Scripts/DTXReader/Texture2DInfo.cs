using UnityEngine;

public class Texture2DInfo
{
    public Texture2D Texture2D { get; set; }
    public bool UseTransparency { get; set; }

    public Texture2DInfo(Texture2D texture, bool useTransparency)
    {
        Texture2D = texture;
        UseTransparency = useTransparency;
    }
}