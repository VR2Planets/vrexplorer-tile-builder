using System;
using System.Collections.Generic;
using UnityEngine;

public class Texture2DPool
{
    private Dictionary<int, Queue<Texture2D>> availableTextures = new();
    private TextureFormat textureFormat;
    private bool mipChain;

    public Texture2DPool(TextureFormat textureFormat, bool mipChain)
    {
        this.textureFormat = textureFormat;
        this.mipChain = mipChain;
    }

    private Texture2D CreateNewTexture(int textureSize)
    {
        var t = new Texture2D(textureSize, textureSize, textureFormat, mipChain, true);
        t.filterMode = FilterMode.Bilinear;
        t.wrapMode = TextureWrapMode.MirrorOnce;
        t.Apply();
        return t;
    }

    public Texture2D Get(int textureSize)
    {
        if (!availableTextures.ContainsKey(textureSize))
        {
            availableTextures[textureSize] = new Queue<Texture2D>();
        }
        if (availableTextures[textureSize].Count == 0)
        {
            availableTextures[textureSize].Enqueue(CreateNewTexture(textureSize));
        }

        return availableTextures[textureSize].Dequeue();
    }

    public void Return(Texture2D texture)
    {
        if (texture == null || texture.width != texture.height || !availableTextures.ContainsKey(texture.width))
        {
            throw new ApplicationException(
                "Wrong texture returning. The texture should not be null, have width == height, and a Queue corresponding to this texture size should already be existing.");
        }
        if (texture != null)
        {
            availableTextures[texture.width].Enqueue(texture);
        }
    }
}