using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DTXConverter
{
    private static bool ShowLogError = false;

    /// <summary>
    /// Converts a Texture2D to ARGB32.
    /// </summary>
    /// <param name="originalTexture"></param>
    private static Texture2D ConvertTextureToArgb32(Texture2D originalTexture)
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(
            originalTexture.width,
            originalTexture.height,
            0, // depthBuffer
            RenderTextureFormat.Default,
            RenderTextureReadWrite.sRGB);

        Graphics.Blit(originalTexture, renderTexture);

        Texture2D newTex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false, false);

        var oldRenderTexture = RenderTexture.active;
        RenderTexture.active = renderTexture;
        newTex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        newTex.Apply();
        RenderTexture.active = oldRenderTexture;
        RenderTexture.ReleaseTemporary(renderTexture);

        return newTex;
    }

    private static void ApplyMipMapOffset(DTXHeader header, TextureSize texInfo)
    {
        if (header.MipMapOffset == 1)
        {
            texInfo.EngineWidth /= 2;
            texInfo.EngineHeight /= 2;
        }
        else if (header.MipMapOffset == 2)
        {
            texInfo.EngineWidth /= 4;
            texInfo.EngineHeight /= 4;
        }
        else if (header.MipMapOffset == 3)
        {
            texInfo.EngineWidth /= 8;
            texInfo.EngineHeight /= 8;
        }
    }

    private static TextureFormat GetTextureFormat(byte bytesPerPixel)
    {
        if (bytesPerPixel == (byte)DTXBPP.BPP_S3TC_DXT5) return TextureFormat.DXT5;
        if (bytesPerPixel == (byte)DTXBPP.BPP_S3TC_DXT3) return TextureFormat.DXT5Crunched; // we use crunched as dxt3
        if (bytesPerPixel == (byte)DTXBPP.BPP_S3TC_DXT1) return TextureFormat.DXT1;
        if (bytesPerPixel == (byte)DTXBPP.BPP_32) return TextureFormat.BGRA32;
        return TextureFormat.BGRA32; // Default to BGRA32
    }

    private static Texture2DInfo ReadTextureData(DTXModel model)
    {
        //Version check must come before everything else!!
		if (model.Header.Version == DTXVersions.LT1
            || model.Header.Version == DTXVersions.LT15
            || (model.Header.Version != DTXVersions.LT2 && model.Header.BPPFormat == (byte)DTXBPP.BPP_8P))
        {
            return Create8BitPaletteTexture(model);
        }
        else if (
            model.Header.BPPFormat == (byte)DTXBPP.BPP_S3TC_DXT1
            || model.Header.BPPFormat == (byte)DTXBPP.BPP_S3TC_DXT3
            || model.Header.BPPFormat == (byte)DTXBPP.BPP_S3TC_DXT5)
        {
            return CreateCompressedTexture(model);
        }
        else if (model.Header.BPPFormat == (byte)DTXBPP.BPP_32)
        {
            return Create32bitTexture(model);
        }
        else if (model.Header.BPPFormat == (byte)DTXBPP.BPP_32P)
        {
            return Create32BitPaletteTexture(model);
        }

        return Create32bitTexture(model);
    }

    private static void FlipTexture(Texture2D texture)
    {
        int width = texture.width;
        int height = texture.height;

        Color[] pixels = texture.GetPixels();
        Color[] flipped = new Color[pixels.Length];

        for (int y = 0; y < height; y++)
        {
            int srcRow = y * width;
            int dstRow = (height - 1 - y) * width;
            for (int x = 0; x < width; x++)
            {
                flipped[dstRow + x] = pixels[srcRow + x];
            }
        }

        texture.SetPixels(flipped);
        texture.Apply();
    }

    private static Texture2DInfo Create32bitTexture(DTXModel model)
    {
        // BGRA32 ?
        bool useTransparency = true;
        if (!model.Header.Prefer4444)
        {
            useTransparency = false;
            // Force alpha to 1.0 for everything:
            for (int i = 0; i < model.Data.Length; i += 4)
            {
                model.Data[i + 3] = 255; // Set alpha (A) to 255
            }
        }

        var texture2D = new Texture2D(model.Header.BaseWidth, model.Header.BaseHeight, TextureFormat.BGRA32, false);
        try
        {
            texture2D.LoadRawTextureData(model.Data);
        }
        catch(Exception ex)
        {
            Debug.LogError("Ex: " + ex.Message);
            return null;
        }

        texture2D.Apply();

        if (!useTransparency)
        {
            return new Texture2DInfo(texture2D, false);
        }

        return new Texture2DInfo(texture2D, UseTransparency(texture2D));
    }

    private static bool UseTransparency(List<Color32> pixels)
    {
        if (pixels.All(pixel => pixel.a == 1))
        {
            return false;
        }

        if (pixels.All(pixel => pixel.a == 0))
        {
            return false;
        }

        return true;
    }

    private static bool UseTransparency(Color[] pixels)
    {
        if (pixels.All(pixel => pixel.a == 1f))
        {
            return false;
        }

        if (pixels.All(pixel => pixel.a == 0f))
        {
            return false;
        }

        return true;
    }

    private static bool UseTransparency(Texture2D texture)
    {
        Color[] pixels = texture.GetPixels();
        return UseTransparency(pixels);
    }

    private static Texture2DInfo CreateCompressedTexture(DTXModel model)
    {
        // DXT1 - Default
        TextureFormat textureFormat = GetTextureFormat(model.Header.BPPFormat);
        TextureFormat format = TextureFormat.DXT1;

        int scale = 8; // Extra bytes needed in the decoding process

        if (model.Header.BPPFormat == (byte)DTXBPP.BPP_S3TC_DXT3)
        {
            // Not supported - no TextureFormat.DXT3 support.
            // If possible, code would:
            //      format = TextureFormat.DXT3;
            //      scale = 16;
            return null;
        }
        else if (model.Header.BPPFormat == (byte)DTXBPP.BPP_S3TC_DXT5)
        {
            format = TextureFormat.DXT5;
            scale = 16;
        }

        var compressed_width = (int)((model.Header.BaseWidth + 3) / 4);
        var compressed_height = (int)((model.Header.BaseHeight + 3) / 4);

        var bytesToKeep = compressed_width * compressed_height * scale;
        var compressedData = model.Data.Take(bytesToKeep).ToArray();

        var texture2D = new Texture2D(model.Header.BaseWidth, model.Header.BaseHeight, format, false);
        try
        {
            texture2D.LoadRawTextureData(compressedData);
        }
        catch (Exception ex)
        {
            Debug.LogError("Ex: " + ex.Message);
            return null;
        }

        texture2D.Apply();

        return new Texture2DInfo(texture2D, UseTransparency(texture2D));
    }

    private static Texture2DInfo Create8BitPaletteTexture(DTXModel model)
    {
        var palette = new List<Color32>();

        var expectedSize = 8 + (256 * 4) + (model.Header.BaseWidth * model.Header.BaseHeight);
        if (model.Data.Length < expectedSize)
        {
            if (ShowLogError)
            {
                Debug.LogError("Invalid DTX? Not enough data to support an 8 bit palette texture!");
            }

            return null;
        }

        // Two unknown ints
        // Used for the internal get palette function in LT1.
        var paletteHeader1 = BitConverter.ToInt32(model.Data, 0);
        var paletteHeader2 = BitConverter.ToInt32(model.Data, 4);

        // Read the colors from the palette.
        for (int i = 0; i < 256;i++)
        {
            var r = model.Data[8 + (i * 4) + 0];
            var g = model.Data[8 + (i * 4) + 1];
            var b = model.Data[8 + (i * 4) + 2];
            var a = model.Data[8 + (i * 4) + 3];

            var c = new Color32(r, g, b, a);
            palette.Add(c);
        }

        // I alpha is always 0 - convert to white so everything shows.
        bool useTransparency = true;
        if (palette.All(c => c.a == 0))
        {
            useTransparency = false;
            palette.ForEach(x => x.a = 255);
        }
        else if (palette.All(c => c.a == 255))
        {
            useTransparency = false;
        }

        // Read the data - just indexes into the palette above.
        int bytesToSkip = 8 + (256 * 4);
        var data = model.Data.Skip(bytesToSkip).Take(model.Header.BaseWidth * model.Header.BaseHeight).ToArray();

        // # Apply the palette
        var colorData = new List<byte>();
        int dataIndex = 0;
        while (dataIndex < data.Length)
        {
            var color = palette[data[dataIndex]];
            colorData.Add(color.r);
            colorData.Add(color.g);
            colorData.Add(color.b);
            colorData.Add(color.a);

            dataIndex += 1;
        }

        var texture2D = new Texture2D(model.Header.BaseWidth, model.Header.BaseHeight, TextureFormat.RGBA32, false);
        try
        {
            texture2D.LoadRawTextureData(colorData.ToArray());
        }
        catch (Exception ex)
        {
            Debug.LogError("Ex: " + ex.Message);
            return null;
        }

        texture2D.Apply();

        return new Texture2DInfo(texture2D, useTransparency);
    }

    private static Texture2DInfo Create32BitPaletteTexture(DTXModel model)
    {
        if (model.Header.SectionCount != 1)
        {
            // Should be 1 section for a 32bit palette texture.
            throw new Exception("Expected SectionCount of 1 for a 32bit palette texture");
        }

        var palette = new List<Color32>();

        int dataSize = model.Header.BaseWidth * model.Header.BaseHeight;
        var data = model.Data.Take(dataSize).ToArray();
        var colorData = new List<byte>();

        int skipCount = 0;
        int width = model.Header.BaseWidth;
        int height = model.Header.BaseHeight;
        // Should this be "i < MipMapCount - 1" or "i < MipMapCount" ?
        for (int i = 0; i < model.Header.MipMapCount - 1; i++)
        {
            width /= 2;
            height /= 2;
            var skip = width * height;
            skipCount += skip;
        }

        // Skip
        skipCount += 16; // Skip section type data
        skipCount += 10; // Skip 10 bytes
        skipCount += 2; // Skip 2 byte filler
        skipCount += 4; // Skip Section length (4x byte int)

        // Read the colors from the palette.
        for (int i = 0; i < 256; i++)
        {
            var a = model.Data[dataSize + skipCount + (i * 4) + 0];
            var r = model.Data[dataSize + skipCount + (i * 4) + 1];
            var g = model.Data[dataSize + skipCount + (i * 4) + 2];
            var b = model.Data[dataSize + skipCount + (i * 4) + 3];

            var c = new Color32(r, g, b, a);
            palette.Add(c);
        }

        // # Apply the palette
        int dataIndex = 0;
        while (dataIndex < data.Length)
        {
            var color = palette[data[dataIndex]];
            colorData.Add(color.r);
            colorData.Add(color.g);
            colorData.Add(color.b);
            colorData.Add(color.a);

            dataIndex += 1;
        }

        var texture2D = new Texture2D(model.Header.BaseWidth, model.Header.BaseHeight, TextureFormat.RGBA32, false);
        try
        {
            texture2D.LoadRawTextureData(colorData.ToArray());
        }
        catch (Exception ex)
        {
            Debug.LogError("Ex: " + ex.Message);
            return null;
        }

        texture2D.Apply();

        return new Texture2DInfo(texture2D, UseTransparency(palette));
    }

    public static Material CreateDefaultMaterial(string name, Texture2D texture, bool useFullbright = false, bool useChromaKey = false)
    {
        Material material = new Material(Shader.Find("Shader Graphs/Lithtech Vertex"));
        material.name = name;
        material.mainTexture = texture;

        if (useFullbright)
        {
            material.SetInt("_FullBright", 1);
        }

        if (useChromaKey)
        {
            material.SetInt("_Chromakey", 1);
            material.SetFloat("NormalIntensityAmount", 0); // turn off normal calculation for now.
            material.SetFloat("_Metallic", 0.9f);
            material.SetFloat("_Smoothness", 0.8f);
            material.SetColor("_Color", Color.white);
        }
        else
        {
            material.SetFloat("_Metallic", 0.9f);
            material.SetFloat("_Smoothness", 0.8f);
        }

        return material;
    }

    public static UnityDTX ConvertDTX(DTXModel model)
    {
        var texture2DInfo = ReadTextureData(model);
        if (texture2DInfo == null)
        {
            return null;
        }

        texture2DInfo.Texture2D.wrapMode = TextureWrapMode.Repeat;
        Texture2D convertedTexture2D = ConvertTextureToArgb32(texture2DInfo.Texture2D);
        if (convertedTexture2D == null)
        {
            return null;
        }

        convertedTexture2D.wrapMode = TextureWrapMode.Repeat;

        FlipTexture(convertedTexture2D);

        TextureSize textureSize = new TextureSize
        {
            Width = model.Header.BaseWidth,
            Height = model.Header.BaseHeight,
            EngineWidth = model.Header.BaseWidth,
            EngineHeight = model.Header.BaseHeight
        };

        if (model.Header.Version == DTXVersions.LT2)
        {
            ApplyMipMapOffset(model.Header, textureSize);
        }

        Material material = new Material(Shader.Find("Shader Graphs/Lithtech Vertex"));
        if (model.Header.UseFullBright)
        {
            material.SetInt("_FullBright", 1);
        }

        var unityDTX = new UnityDTX
        {
            DTXModel = model,
            Material = material,
            Texture2D = convertedTexture2D,
            TextureSize = textureSize,
            UseTransparency = texture2DInfo.UseTransparency
        };

        return unityDTX;
    }
}