using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightMeasure : MonoBehaviour
{
    [SerializeField] Transform _lightCamProp;
    [SerializeField] Camera _mainCam;
    RenderTexture rt;
    [SerializeField] int readPixelX = 0;
    [SerializeField] int readPixelY = 0;
    [SerializeField] int getPixelX = 0;
    [SerializeField] int getPixelY = 0;
    [SerializeField] float _updateFrequency = 4f;
    [SerializeField] Vector3 _cameraPos;
    [SerializeField] Vector3 _cameraRot;

    private Texture2D tex;
    private Rect rectReadPicture;
    Camera _lightCam;

    int rectSize = 8;

    float _timer = 0;

    private void Awake()
    {
        _lightCam = new Camera();
        _lightCam = gameObject.AddComponent<Camera>(); ;
        rt = new(rectSize, rectSize, 1);
        _lightCam.targetTexture = rt;
        tex = new Texture2D(rt.width, rt.height);
        rectReadPicture = new Rect(0, 0, rt.width, rt.height);
    }
    private void Update()
    {
        AdjustCamera();
        // MeasureSinglePixel();
        if (IsReadyForUpdate())
        {
            MeasureAllPixels();
        }
    }

    void AdjustCamera()
    {
        _lightCam.gameObject.transform.position = _cameraPos;
        _lightCam.gameObject.transform.rotation = Quaternion.Euler(_cameraRot);
    }
    bool IsReadyForUpdate()
    {
        _timer += Time.deltaTime;
        if (_timer > _updateFrequency / 60f)
        {
            _timer = 0;
            return true;
        }
        return false;
    }
    // Start is called before the first frame update
    void Start()
    {
        // StartCoroutine(CheckGrayRoutine());
    }

    void MeasureAllPixels()
    {
        float allGray = 0;
        RenderTexture.active = rt;
        for (int i = 0; i < rt.width; i++)
        {
            for (int j = 0; j < rt.height; j++)
            {
                tex.ReadPixels(rectReadPicture, i, j);
                tex.Apply();

                // Debug.Log(tex.GetPixel(i, j).grayscale);
                float gray = tex.GetPixel(i, j).grayscale;
                allGray += gray;
            }
        }
        RenderTexture.active = null;
        rt.Release();

        float finalAverage = allGray / (rt.width * rt.height);
        Debug.Log($"final average = {finalAverage}");
    }

    void MeasureSinglePixel()
    {
        RenderTexture.active = rt;

        tex.ReadPixels(rectReadPicture, readPixelX, readPixelY);
        tex.Apply();

        Debug.Log(tex.GetPixel(getPixelX, getPixelY).grayscale);

        RenderTexture.active = null;
        rt.Release();
    }

    IEnumerator CheckGrayRoutine()
    {
        while (true)
        {
            for (int i = 0; i < rt.width; i++)
            {
                for (int j = 0; j < rt.height; j++)
                {
                    RenderTexture.active = rt;
                    tex.ReadPixels(rectReadPicture, i, j);
                    tex.Apply();

                    Debug.Log(tex.GetPixel(i, j).grayscale);

                    RenderTexture.active = null;
                    rt.Release();

                }
            }
            yield return new WaitForEndOfFrame();
        }
    }
}
