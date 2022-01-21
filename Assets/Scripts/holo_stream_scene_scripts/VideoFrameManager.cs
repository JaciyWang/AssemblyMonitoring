// Adapted from the WinML MNIST sample and Rene Schulte's repo 
// https://github.com/microsoft/Windows-Machine-Learning/tree/master/Samples/MNIST
// https://github.com/reneschulte/WinMLExperiments/

using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_WINMD_SUPPORT
using Windows.Graphics.Imaging;
#endif

public class VideoFrameManager : MonoBehaviour
{
    // Public fields
    public float ProbabilityThreshold = 0.5f;
    public Vector2 InputFeatureSize = new Vector2(224, 224);
    public GameObject StatusBlock;

    // Private fields
    private TakeMediaCapture _mediaCaptureUtility;
    private bool _isRunning = false;

    //新加
    public Material mediaMaterial;
    private Texture2D videoTexture;

    private int count = 0;
    int looptimes = 0;

    #region UnityMethods

#if ENABLE_WINMD_SUPPORT
    async void Start()
    {
        try
        {
            // Create a new instance of the network model class
            // and asynchronously load the onnx model
            StatusBlock.transform.GetComponent<TextMeshPro>().text = $"Loaded model. Starting camera...";


            // Configure camera to return frames fitting the model input size
            try
            {
                Debug.Log("Creating MediaCaptureUtility and initializing frame reader.");
                _mediaCaptureUtility = new TakeMediaCapture();
                await _mediaCaptureUtility.InitializeMediaFrameReaderAsync(
                    (uint)InputFeatureSize.x, (uint)InputFeatureSize.y);
                StatusBlock.transform.GetComponent<TextMeshPro>().text = $"Camera started. Running!";

                Debug.Log("Successfully initialized frame reader.");
            }
            catch (Exception ex)
            {
                StatusBlock.transform.GetComponent<TextMeshPro>().text = $"Failed to start camera: {ex.Message}. Using loaded/picked image.";

            }

            // Run processing loop in separate parallel Task, get the latest frame
            // and asynchronously evaluate
            Debug.Log("Begin performing inference in frame grab loop.");

            //新加
            videoTexture = new Texture2D(224, 224, TextureFormat.RGBA32, false);
            mediaMaterial.mainTexture = videoTexture;

            _isRunning = true;

            //StartCoroutine("GetFrame");

            await Task.Run(async () =>
            {
                while (_isRunning)
                {
                    if (_mediaCaptureUtility.IsCapturing)
                    {
                        using (var videoFrame = _mediaCaptureUtility.GetLatestFrame())
                        {
                            looptimes++;
                            text = $"loop : {looptimes} ; image : {count}";
                            await EvaluateFrame(videoFrame);
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            });
 
        }
        catch (Exception ex)
        {
            StatusBlock.transform.GetComponent<TextMeshPro>().text = $"Error init: {ex.Message}";
            Debug.LogError($"Failed to start model inference: {ex}");
        }
    }

    IEnumerator GetFrame()
    {
        while (_isRunning)
        {
            if (_mediaCaptureUtility.IsCapturing)
            {
                using (var videoFrame = _mediaCaptureUtility.GetLatestFrame())
                {
                    StatusBlock.transform.GetComponent<TextMeshPro>().text = $"Image: {count}.";
                    //await EvaluateFrame(videoFrame);
                }
            }
            yield return null;
        }
    }

    private void Update()
    {
        StatusBlock.transform.GetComponent<TextMeshPro>().text = text;
    }

    private async void OnDestroy()
    {
        _isRunning = false;
        if (_mediaCaptureUtility != null)
        {
            await _mediaCaptureUtility.StopMediaFrameReaderAsync();
        }
    }
#endif
    #endregion

#if ENABLE_WINMD_SUPPORT
    private SoftwareBitmap frameBitmap = null;
    
    int videoWidth = 224;
    int videoHeight = 224;

    string text = "text";

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    private async Task EvaluateFrame(Windows.Media.VideoFrame videoFrame)
    {
        try
        {
            if (videoFrame != null)
            {
                //frameBitmap = videoFrame.SoftwareBitmap;
                var softwareBitmap = SoftwareBitmap.Convert(videoFrame.SoftwareBitmap, BitmapPixelFormat.Rgba8, BitmapAlphaMode.Ignore);
                Interlocked.Exchange(ref frameBitmap, softwareBitmap);
                await Task.Run(() =>
                {
                    unsafe
                    {
                        using (var input = frameBitmap.LockBuffer(BitmapBufferAccessMode.Read))
                        using (var inputReference = input.CreateReference())
                        {
                            byte* inputBytes;
                            uint inputCapacity;
                            ((IMemoryBufferByteAccess)inputReference).GetBuffer(out inputBytes, out inputCapacity);
                            videoTexture.LoadRawTextureData((IntPtr)inputBytes, videoWidth * videoHeight * 4);
                            videoTexture.Apply();
                            count++;
                            text = $"Image: {count}.";
                        }
                    }
                });
            }
            else
            {
                text = $"Frame is null.";
            }

            /*// Get the current network prediction from model and input frame
            var result = await _networkModel.EvaluateVideoFrameAsync(videoFrame);

            // Update the UI with prediction
            UnityEngine.WSA.Application.InvokeOnAppThread(() =>
            {
                StatusBlock.text = $"Label: {result.PredictionLabel} " +
                $"Probability: {Math.Round(result.PredictionProbability, 3) * 100}% " +
                $"Inference time: {result.PredictionTime} ms";
            }, false);*/
        }
        catch (Exception ex)
        {
            Debug.Log($"Exception {ex}");
        }
    }

#endif
}