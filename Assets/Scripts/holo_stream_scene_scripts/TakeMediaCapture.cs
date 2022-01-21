﻿// Script taken directly from Rene Schulte's repo: https://github.com/reneschulte/WinMLExperiments/blob/master/HoloVision20/Assets/Scripts/MediaCapturer.cs

using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using System.Runtime.InteropServices;
using System.Threading;

#if ENABLE_WINMD_SUPPORT
using Windows.Media;
using Windows.Media.Capture;
using Windows.Storage.Streams;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Media.Devices;
using Windows.Graphics.Imaging;
using Windows.Devices.Enumeration;
#endif // ENABLE_WINMD_SUPPORT

public class TakeMediaCapture : MonoBehaviour
{
    public bool IsCapturing { get; set; }

    // Public fields
    public float ProbabilityThreshold = 0.5f;
    public Vector2 InputFeatureSize = new Vector2(224, 224);
    public GameObject StatusBlock;
    //新加
    public Material mediaMaterial;

#if ENABLE_WINMD_SUPPORT
    private MediaCapture _mediaCapture;
    private MediaFrameReader _mediaFrameReader;
    private VideoFrame _videoFrame;

    
    // Private fields
   // private TakeMediaCapture _mediaCaptureUtility;
    private bool _isRunning = false;

    
    private Texture2D videoTexture;

    private int count = 0;
    int looptimes = 0;

    /// <summary>
    /// Method to start capturing camera frames at desired resolution.
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <returns></returns>
    public async Task InitializeMediaFrameReaderAsync(uint width = 224, uint height = 224)
    {
        // Check state of media capture object 
        if (_mediaCapture == null || _mediaCapture.CameraStreamState == CameraStreamState.Shutdown || _mediaCapture.CameraStreamState == CameraStreamState.NotStreaming)
        {
            if (_mediaCapture != null)
            {
                _mediaCapture.Dispose();
            }

            // Find right camera settings and prefer back camera
            MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings();
            var allCameras = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            Debug.Log($"InitializeMediaFrameReaderAsync: allCameras: {allCameras}");

            var selectedCamera = allCameras.FirstOrDefault(c => c.EnclosureLocation?.Panel == Panel.Back) ?? allCameras.FirstOrDefault();
            Debug.Log($"InitializeMediaFrameReaderAsync: selectedCamera: {selectedCamera}");


            if (selectedCamera != null)
            {
                settings.VideoDeviceId = selectedCamera.Id;
                Debug.Log($"InitializeMediaFrameReaderAsync: settings.VideoDeviceId: {settings.VideoDeviceId}");

            }

            // Init capturer and Frame reader
            _mediaCapture = new MediaCapture();
            Debug.Log("InitializeMediaFrameReaderAsync: Successfully created media capture object.");

            await _mediaCapture.InitializeAsync(settings);
            Debug.Log("InitializeMediaFrameReaderAsync: Successfully initialized media capture object.");

            var frameSource = _mediaCapture.FrameSources.Where(source => source.Value.Info.SourceKind == MediaFrameSourceKind.Color).First();
            Debug.Log($"InitializeMediaFrameReaderAsync: frameSource: {frameSource}.");

            // Convert the pixel formats
            var subtype = MediaEncodingSubtypes.Bgra8;

            // The overloads of CreateFrameReaderAsync with the format arguments will actually make a copy in FrameArrived
            BitmapSize outputSize = new BitmapSize { Width = width, Height = height };
            _mediaFrameReader = await _mediaCapture.CreateFrameReaderAsync(frameSource.Value, subtype, outputSize);
            Debug.Log("InitializeMediaFrameReaderAsync: Successfully created media frame reader.");

            _mediaFrameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;

            await _mediaFrameReader.StartAsync();
            Debug.Log("InitializeMediaFrameReaderAsync: Successfully started media frame reader.");

            IsCapturing = true;
        }
    }

    /// <summary>
    /// Retrieve the latest video frame from the media frame reader
    /// </summary>
    /// <returns>VideoFrame object with current frame's software bitmap</returns>
    public VideoFrame GetLatestFrame()
    {
        // The overloads of CreateFrameReaderAsync with the format arguments will actually return a copy so we don't have to copy again
        var mediaFrameReference = _mediaFrameReader.TryAcquireLatestFrame();
        var videoFrame = mediaFrameReference?.VideoMediaFrame?.GetVideoFrame();
        //Debug.Log("GetLatestFrame: Successfully retrieved video frame.");
        return videoFrame;
    }
#endif

    /// <summary>
    /// Asynchronously stop media capture and dispose of resources
    /// </summary>
    /// <returns></returns>
#if ENABLE_WINMD_SUPPORT
    public async Task StopMediaFrameReaderAsync()
    {

        if (_mediaCapture != null && _mediaCapture.CameraStreamState != CameraStreamState.Shutdown)
        {
            await _mediaFrameReader.StopAsync();
            _mediaFrameReader.Dispose();
            _mediaCapture.Dispose();
            _mediaCapture = null;
        }
        IsCapturing = false;
    }

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
                //_mediaCaptureUtility = new TakeMediaCapture();
                await InitializeMediaFrameReaderAsync(
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
                    if (IsCapturing)
                    {
                        using (var videoFrame = GetLatestFrame())
                        {
                            looptimes++;
                            //text = $"loop : {looptimes} ; image : {count}";
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

    private void Update()
    {
        StatusBlock.transform.GetComponent<TextMeshPro>().text = text;
    }

    private async void OnDestroy()
    {
        _isRunning = false;
        await StopMediaFrameReaderAsync();
    }

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
                text = $"--------------------------";
                await Task.Run(() =>
                {
                    text = $"Frame is not null.";
                
                    unsafe
                    {
                        text = $"Capturing.";
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