using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnalyzeLight : MonoBehaviour
{
    [SerializeField] RenderTexture rt;

    private Texture2D tex;
    private Rect rectReadPicture;

    private void Awake()
    {
        tex = new Texture2D(rt.width, rt.height);
        rectReadPicture = new Rect(0, 0, rt.width, rt.height);
    }
    private void Update()
    {
        RenderTexture.active = rt;

        tex.ReadPixels(rectReadPicture, 0, 0);
        tex.Apply();

        Debug.Log(tex.GetPixel(0, 0));

        RenderTexture.active = null;
        rt.Release();
    }
}
