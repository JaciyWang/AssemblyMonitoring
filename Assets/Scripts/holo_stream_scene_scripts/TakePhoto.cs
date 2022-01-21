using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows.WebCam;
using System.Linq;
using UnityEngine.UI;

public class TakePhoto : MonoBehaviour
{
    public PhotoCapture photoCaptureObj = null;
    List<byte> imageBufferList = new List<byte>();
    private Texture2D targetTexture;
    CameraParameters cameraParameters;

    public Image ShowImage;

    bool isReady = false;
    // Update is called once per frame
    void Update()
    {
        if (isReady)
        {
            photoCaptureObj.TakePhotoAsync(OnCaptturePhotoToMemory);
        }
    }
    

    void Start()
    {
        ShowImage.gameObject.SetActive(true);
#if UNITY_EDITOR || UNITY_WSA
        PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
#else
 
#endif
    }

    void OnPhotoCaptureCreated(PhotoCapture captureObject)
    {
        photoCaptureObj = captureObject;

        Resolution cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
        cameraParameters = new CameraParameters();
        cameraParameters.hologramOpacity = 0.0f;
        cameraParameters.cameraResolutionHeight = cameraResolution.height;
        cameraParameters.cameraResolutionWidth = cameraResolution.width;
        cameraParameters.pixelFormat = CapturePixelFormat.BGRA32;

        captureObject.StartPhotoModeAsync(cameraParameters, OnPhotoModeStarted);
    }


    private void OnPhotoModeStarted(PhotoCapture.PhotoCaptureResult result)
    {
        if (result.success)
        {
            isReady = true;
            //photoCaptureObj.TakePhotoAsync(OnCaptturePhotoToMemory);
            Debug.Log("拍照成功");
        }
        else
        {
            //重新拍照
            // PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
            Debug.Log("重新拍照");
        }
    }

    void OnCaptturePhotoToMemory(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame)
    {
        if (result.success)
        {
            //照片显示
            photoCaptureFrame.CopyRawImageDataIntoBuffer(imageBufferList);
            imageBufferList = FlipVertical(imageBufferList, cameraParameters.cameraResolutionWidth, cameraParameters.cameraResolutionHeight, 4);
            targetTexture = CreateTexture(imageBufferList, cameraParameters.cameraResolutionWidth, cameraParameters.cameraResolutionHeight);
            ShowImage.sprite = Sprite.Create(targetTexture, new Rect(0, 0, targetTexture.width, targetTexture.height), new Vector2(0.5f, 0.5f));
        }
        //photoCaptureObj.StopPhotoModeAsync(OnStoppedPhotoMode);
    }

    void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        photoCaptureObj.Dispose();
        photoCaptureObj = null;
    }


    private Texture2D CreateTexture(List<byte> rawData, int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.BGRA32, false);
        tex.LoadRawTextureData(rawData.ToArray());
        tex.Apply();
        return tex;
    }
    /// <summary>
    /// 照片上下反转
    /// </summary>
    /// <param name="src"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="stride"></param>
    /// <returns></returns>
    private List<byte> FlipVertical(List<byte> src, int width, int height, int stride)
    {
        byte[] dst = new byte[src.Count];
        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                int invY = (height - 1) - y;
                int pxel = (y * width + x) * stride;
                int invPxel = (invY * width + x) * stride;
                for (int i = 0; i < stride; ++i)
                {
                    dst[invPxel + i] = src[pxel + i];
                }
            }
        }
        return new List<byte>(dst);
    }


    public void OnStopCapture()
    {
        isReady = false;
        if(photoCaptureObj != null)
        {
            photoCaptureObj.Dispose();
            photoCaptureObj = null;
        }
        
    }

    private void OnDestroy()
    {
        if (isReady)
        {
            photoCaptureObj.Dispose();
            photoCaptureObj = null;
        }
    }
}
