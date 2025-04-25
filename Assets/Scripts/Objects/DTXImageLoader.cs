using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class DTXImageLoader : BaseConverter
{
    public string ImagePath;
    public bool UseOriginal;

    public void Start()
    {
        var image = GetComponent<Image>();

        string fullfilenameAndPath = Path.Combine(SourceRootFolder, ImagePath);
        var dtxModel = DTXModelReader.ReadDTXModel(fullfilenameAndPath, ImagePath);
        var unityDTX = DTXConverter.ConvertDTX(dtxModel);

        Sprite newSprite = Sprite.Create(unityDTX.Texture2D, new Rect(0, 0, unityDTX.Texture2D.width, unityDTX.Texture2D.width), new Vector2(0.5f, 0.5f));

        image.sprite = newSprite;
    }
}