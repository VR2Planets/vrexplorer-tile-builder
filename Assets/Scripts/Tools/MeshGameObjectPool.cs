using System;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

public class MeshGameObjectPool
{
    private ObjectPool<MeshFilter> internalPool;
    private Transform parent;

    public MeshGameObjectPool(Transform parent)
    {
        this.parent = parent;
        internalPool = new ObjectPool<MeshFilter>(OnCreate, OnGet, OnRelease, OnDestroy);
    }

    public MeshFilter Get()
    {
        return internalPool.Get();
    }

    public void Release(MeshFilter obj)
    {
        internalPool.Release(obj);
    }

    private MeshFilter OnCreate()
    {
        GameObject obj = new GameObject("Pooled Mesh Object");
        obj.AddComponent<MeshRenderer>();
        var mf = obj.AddComponent<MeshFilter>();
        obj.transform.SetParent(parent);
        obj.SetActive(false);

        return mf;
    }

    private void OnGet(MeshFilter obj)
    {
        obj.SmartActive(true);
    }

    private void OnRelease(MeshFilter obj)
    {
        obj.SmartActive(false);
        obj.sharedMesh = null;
        obj.GetComponent<MeshRenderer>().materials = Array.Empty<Material>();
    }

    private void OnDestroy(MeshFilter obj)
    {
        if (obj != null)
        {
            Object.Destroy(obj);
        }
    }
}