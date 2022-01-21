using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
using Windows.Media.Devices.Core;
using Windows.Perception.Spatial;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using System.Threading.Tasks;
using System;
using Windows.Graphics.Imaging;
using System.Threading;
using System.Linq;
using Windows.Storage.Streams;
using wMatrix4x4 = System.Numerics.Matrix4x4;
#endif

/// <summary>
/// The MediaCaptureUnity class manages video access of HoloLens.
/// </summary>


public class MediaCaptureHandler : MonoBehaviour
{
    private Texture2D mediaTexture;

    public enum MediaCaptureProfiles
    {
        HL1_1280x720,
        HL1_1408x792,
        HL1_1344x756,
        HL1_896x504,
        HL2_2272x1278,
        HL2_896x504,
        HL2_1280x720
    }

    public MediaCaptureProfiles mediaCaptureProfiles;
    private int _targetVideoWidth, _targetVideoHeight;
    private float _targetVideoFrameRate = 10f;
    public GameObject calibrationText;
    public GameObject debugText;


    //public GameObject CameraText;

    public VideoPanel _videoPanelUI;

    void DebugLog(string s)
    {
        debugText.GetComponent<TextMeshPro>().text = s;
    }
       
    /*void CameraLog(string s)
    {
        CameraText.GetComponent<TextMeshPro>().text = s;
    }*/


    private enum CaptureStatus
    {
        Clean,
        Initialized,
        Running
    }
    private CaptureStatus captureStatus = CaptureStatus.Clean;
    //public FpsDisplayHUD fpsDisplayHUD;
    public static string TAG = "MediaCaptureUnity";

    //private UDPKeyboardInput udpKeyboard;
    //private FrameSaver frameSaver;
    int saveCount = 0;
    public bool isIntrinsicsReady = false;

#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    private SoftwareBitmap upBitmap = null;
    private SoftwareBitmap _tempBitmap = null;   //在OnFrameArrived被latestFrame替换
    private MediaCapture mediaCapture;

    private MediaFrameReader frameReader = null;

    private int videoWidth = 0;
    private int videoHeight = 0;
    private int HL = 0;
    
    [HideInInspector]
    public wMatrix4x4 worldToCameraTransform = wMatrix4x4.Identity; // = (wMatrix4x4)unityWorldCoordinateSystem.TryGetTransformTo(w_coordinateSystem);
    public CameraIntrinsics cameraIntrinsics = null; // = frame.VideoMediaFrame.CameraIntrinsics;
    
    private async Task<bool> InitializeMediaCaptureAsync()
    {
        if (captureStatus != CaptureStatus.Clean)
        {
            DebugLog(TAG + ": InitializeMediaCaptureAsync() fails because of incorrect status");
            return false;
        }

        if (mediaCapture != null)
        {
            return false;
        }

        var allGroups = await MediaFrameSourceGroup.FindAllAsync();
        int selectedGroupIndex = -1;

        // 打印Camera数据
        /*string t = "";
        for (int j = 0; j < allGroups.Count; j++)
        {
            var cameraGroup = allGroups[j];
            t += cameraGroup.DisplayName + "," + cameraGroup.Id + "\n";
        }

        IReadOnlyList<MediaCaptureVideoProfile> profile_list = MediaCapture.FindAllVideoProfiles(allGroups[1].Id);
        t += profile_list;
        CameraLog(t);*/

        for (int i = 0; i < allGroups.Count; i++)
        {
            var group = allGroups[i];
            //DebugLog(group.DisplayName + ", " + group.Id);
            // for HoloLens 1
            if (group.DisplayName == "MN34150")
            {
                selectedGroupIndex = i;
                HL = 1;
                DebugLog(TAG + ": Selected group " + i + " on HoloLens 1");
                break;
            }
            // for HoloLens 2
            else if (group.DisplayName == "QC Back Camera")
            {
                selectedGroupIndex = i;
                HL = 2;
                DebugLog(TAG + ": Selected group " + i + " on HoloLens 2");
                break;
            }
        }

        if (selectedGroupIndex == -1)
        {
            DebugLog(TAG + ": InitializeMediaCaptureAsyncTask() fails because there is no suitable source group");
            return false;
        }

        // Initialize mediacapture with the source group.
        mediaCapture = new MediaCapture();
        MediaStreamType mediaStreamType = MediaStreamType.VideoPreview;
        if (HL == 1)
        {
            var settings = new MediaCaptureInitializationSettings
            {
                SourceGroup = allGroups[selectedGroupIndex],
                // This media capture can share streaming with other apps.
                SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                // Only stream video and don't initialize audio capture devices.
                StreamingCaptureMode = StreamingCaptureMode.Video,
                // Set to CPU to ensure frames always contain CPU SoftwareBitmap images
                // instead of preferring GPU D3DSurface images.
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };
            await mediaCapture.InitializeAsync(settings);
            DebugLog(TAG + ": MediaCapture is successfully initialized in SharedReadOnly mode for HoloLens 1.");
            mediaStreamType = MediaStreamType.VideoPreview;
        }
        else if (HL == 2)
        {
            string deviceId = allGroups[selectedGroupIndex].Id;
            // Look up for all video profiles
            IReadOnlyList<MediaCaptureVideoProfile> profileList = MediaCapture.FindAllVideoProfiles(deviceId);
            //MediaCaptureVideoProfile selectedProfile;
            //IReadOnlyList<MediaCaptureVideoProfile> profileList = MediaCapture.FindKnownVideoProfiles(deviceId, KnownVideoProfile.VideoConferencing);

            // Initialize mediacapture with the source group.
            var settings = new MediaCaptureInitializationSettings
            {
                SourceGroup = allGroups[selectedGroupIndex],
                //VideoDeviceId = deviceId,
                //VideoProfile = profileList[0],
                // This media capture can share streaming with other apps.
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                // Only stream video and don't initialize audio capture devices.
                StreamingCaptureMode = StreamingCaptureMode.Video,
                // Set to CPU to ensure frames always contain CPU SoftwareBitmap images
                // instead of preferring GPU D3DSurface images.
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };
            try
            {
                await mediaCapture.InitializeAsync(settings);
                DebugLog(TAG + ": MediaCapture is successfully initialized in ExclusiveControl mode for HoloLens 2.");

            }
            catch (Exception ex)
            {
                DebugLog(TAG + ": MediaCapture initialization failed: " + ex.Message);
                return false;
            }
            mediaStreamType = MediaStreamType.VideoRecord;
        }


        try
        {
            var mediaFrameSourceVideo = mediaCapture.FrameSources.Values.Single(x => x.Info.MediaStreamType == mediaStreamType);
            MediaFrameFormat targetResFormat = null;
            float framerateDiffMin = 60f;
            foreach (var f in mediaFrameSourceVideo.SupportedFormats.OrderBy(x => x.VideoFormat.Width * x.VideoFormat.Height))
            {

                // Check current media frame source resolution versus target resolution
                if (f.VideoFormat.Width == _targetVideoWidth && f.VideoFormat.Height == _targetVideoHeight)
                {
                    if (targetResFormat == null)
                    {
                        targetResFormat = f;
                        framerateDiffMin = Mathf.Abs(f.FrameRate.Numerator / f.FrameRate.Denominator - _targetVideoFrameRate);
                    }
                    else if (Mathf.Abs(f.FrameRate.Numerator / f.FrameRate.Denominator - _targetVideoFrameRate) < framerateDiffMin)
                    {
                        targetResFormat = f;
                        framerateDiffMin = Mathf.Abs(f.FrameRate.Numerator / f.FrameRate.Denominator - _targetVideoFrameRate);
                    }
                }
            }
            if (targetResFormat == null)
            {
                targetResFormat = mediaFrameSourceVideo.SupportedFormats[0];
                DebugLog(TAG + ": Unable to choose the selected format, fall back");
            }
            // choose the smallest resolution
            //var targetResFormat = mediaFrameSourceVideoPreview.SupportedFormats.OrderBy(x => x.VideoFormat.Width * x.VideoFormat.Height).FirstOrDefault();
            // choose the specific resolution
            //var targetResFormat = mediaFrameSourceVideoPreview.SupportedFormats.OrderBy(x => (x.VideoFormat.Width == 1344 && x.VideoFormat.Height == 756)).FirstOrDefault();
            await mediaFrameSourceVideo.SetFormatAsync(targetResFormat);
            DebugLog(TAG + ": mediaFrameSourceVideo.SetFormatAsync()");
            frameReader = await mediaCapture.CreateFrameReaderAsync(mediaFrameSourceVideo, targetResFormat.Subtype);
            DebugLog(TAG + ": mediaCapture.CreateFrameReaderAsync()");
            frameReader.FrameArrived += OnFrameArrived;
            videoWidth = Convert.ToInt32(targetResFormat.VideoFormat.Width);
            videoHeight = Convert.ToInt32(targetResFormat.VideoFormat.Height);
            DebugLog(TAG + ": FrameReader is successfully initialized, " + videoWidth + "x" + videoHeight +
                ", Framerate: " + targetResFormat.FrameRate.Numerator + "/" + targetResFormat.FrameRate.Denominator);
        }
        catch (Exception e)
        {
            DebugLog(TAG + ": FrameReader is not initialized");
            DebugLog(TAG + ": Exception: " + e);
            return false;
        }

        captureStatus = CaptureStatus.Initialized;
        return true;
    }

    private async Task<bool> StartFrameReaderAsync()
    {
        //DebugLog(TAG + " StartFrameReaderAsync() thread ID is " + Thread.CurrentThread.ManagedThreadId);
        if (captureStatus != CaptureStatus.Initialized)
        {
            //DebugLog(TAG + ": StartFrameReaderAsync() fails because of incorrect status");
            return false;
        }

        MediaFrameReaderStartStatus status = await frameReader.StartAsync();
        if (status == MediaFrameReaderStartStatus.Success)
        {
            //DebugLog(TAG + ": StartFrameReaderAsync() is successful");
            captureStatus = CaptureStatus.Running;
            return true;
        }
        else
        {
            //DebugLog(TAG + ": StartFrameReaderAsync() is successful, status = " + status);
            return false;
        }
    }

    private async Task<bool> StopFrameReaderAsync()
    {
        if (captureStatus != CaptureStatus.Running)
        {
            DebugLog(TAG + ": StopFrameReaderAsync() fails because of incorrect status");
            return false;
        }
        await frameReader.StopAsync();
        frameReader.FrameArrived -= OnFrameArrived;
        mediaCapture.Dispose();
        mediaCapture = null;
        captureStatus = CaptureStatus.Initialized;
        DebugLog(TAG + ": StopFrameReaderAsync() is successful");
        return true;
    }

    private bool onFrameArrivedProcessing = false;

    private unsafe void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        // TryAcquireLatestFrame will return the latest frame that has not yet been acquired.
        // This can return null if there is no such frame, or if the reader is not in the
        // "Started" state. The latter can occur if a FrameArrived event was in flight
        // when the reader was stopped.
        if (onFrameArrivedProcessing)
        {
            //DebugLog(TAG + " OnFrameArrived() is still processing");
            return;
        }
        onFrameArrivedProcessing = true;
        using (var frame = sender.TryAcquireLatestFrame())
        {
            if (frame != null)
            {
                if (frame.VideoMediaFrame.CameraIntrinsics != null)
                {                                       
                    SpatialCoordinateSystem w_coordinateSystem = frame.CoordinateSystem;
                    worldToCameraTransform = (wMatrix4x4)unityWorldCoordinateSystem.TryGetTransformTo(w_coordinateSystem);
                    cameraIntrinsics = frame.VideoMediaFrame.CameraIntrinsics;
                    isIntrinsicsReady = true;
                }
                else
                {
                    isIntrinsicsReady = false;
                }

                //fpsDisplayHUD.PreviewTick();
                var softwareBitmap = SoftwareBitmap.Convert(frame.VideoMediaFrame.SoftwareBitmap, BitmapPixelFormat.Rgba8, BitmapAlphaMode.Ignore);
                Interlocked.Exchange(ref _tempBitmap, softwareBitmap);
                frame.VideoMediaFrame.SoftwareBitmap?.Dispose();
            }
        }
        onFrameArrivedProcessing = false;
    }

    static wMatrix4x4 ByteArrayToMatrix(byte[] bits)
    {
        var matrix = wMatrix4x4.Identity;

        var handle = GCHandle.Alloc(bits, GCHandleType.Pinned);
        matrix = Marshal.PtrToStructure<wMatrix4x4>(handle.AddrOfPinnedObject());
        handle.Free();

        return (matrix);
    }

    async void InitializeMediaCaptureAsyncWrapper()
    {
        await InitializeMediaCaptureAsync();
    }

    async void StartFrameReaderAsyncWrapper()
    {
        await StartFrameReaderAsync();
    }

    async void StopFrameReaderAsyncWrapper()
    {
        await StopFrameReaderAsync();
    }

    private bool textureInitialized = false;
    bool isRunning = true;

    SpatialCoordinateSystem unityWorldCoordinateSystem;
    void Start()
    {
        // 一开始就用，获取世界零点坐标系
        unityWorldCoordinateSystem = SpatialLocator.GetDefault().CreateStationaryFrameOfReferenceAtCurrentLocation().CoordinateSystem;

        // 报错"object no reference" 或许 是_videoPanelUI?
        //_videoPanelUI = GameObject.FindObjectOfType<VideoPanel>();

        //Application.targetFrameRate = 60;
        captureStatus = CaptureStatus.Clean;
        InitializeMediaCaptureAsyncWrapper();

        // Cache values to target width and height from media capture
        // profiles enum.
        switch (mediaCaptureProfiles)
        {
            case MediaCaptureProfiles.HL1_1280x720:
                _targetVideoWidth = 1280;
                _targetVideoHeight = 720;
                break;
            case MediaCaptureProfiles.HL1_1408x792:
                _targetVideoWidth = 1408;
                _targetVideoHeight = 792;
                break;
            case MediaCaptureProfiles.HL1_1344x756:
                _targetVideoWidth = 1344;
                _targetVideoHeight = 756;
                break;
            case MediaCaptureProfiles.HL1_896x504:
                _targetVideoWidth = 896;
                _targetVideoHeight = 504;
                break;
            case MediaCaptureProfiles.HL2_2272x1278:
                _targetVideoWidth = 2272;
                _targetVideoHeight = 1278;
                break;
            case MediaCaptureProfiles.HL2_896x504:
                _targetVideoWidth = 896;
                _targetVideoHeight = 504;
                break;
            case MediaCaptureProfiles.HL2_1280x720:
                _targetVideoWidth = 1280;
                _targetVideoHeight = 720;
                break;
            default:
                break;
        }

        isRunning = true;
        StartCoroutine("GetFrame");

        /*if (!textureInitialized && captureStatus == CaptureStatus.Initialized)
        {
            mediaTexture = new Texture2D(videoWidth, videoHeight, TextureFormat.RGBA32, false);
            mediaMaterial.mainTexture = mediaTexture;
            textureInitialized = true;
            ToggleVideo();
            calibrationText.GetComponent<TextMeshPro>().text = "Calibration initializing";
        }
        else
        {
            calibrationText.GetComponent<TextMeshPro>().text = "Calibration not started";
        }*/
    }

    IEnumerator GetFrame()
    {
        while (isRunning)
        {
            if (!textureInitialized && captureStatus == CaptureStatus.Initialized)
            {
                DebugLog(TAG + " Starting.");
                mediaTexture = new Texture2D(videoWidth, videoHeight, TextureFormat.RGBA32, false);
                //mediaMaterial.mainTexture = mediaTexture;
                textureInitialized = true;
                ToggleVideo();
            }
            //Debug.Log(TAG + " Update() thread ID is " + Thread.CurrentThread.ManagedThreadId);

            if (_tempBitmap != null && textureInitialized)
            {
                //DebugLog(TAG + " Running.");
                GetFrameTextureAsyncWrapper();
            }

            if (captureStatus == CaptureStatus.Clean)
                calibrationText.GetComponent<TextMeshPro>().text = "Calibration not started";
            else if (captureStatus == CaptureStatus.Initialized)
                calibrationText.GetComponent<TextMeshPro>().text = "Calibration initializing";
            else if (captureStatus == CaptureStatus.Running)
                calibrationText.GetComponent<TextMeshPro>().text = videoHeight + ":"  + videoWidth + " : Calibration: " + saveCount + " images saved";
            
            yield return new WaitForSeconds(0.15f);
        }
    }

    unsafe void GetFrameTextureAsyncWrapper()
    {
        Interlocked.Exchange(ref upBitmap, _tempBitmap);
        /*byte[] data = await EncodedBytes(upBitmap, BitmapEncoder.JpegEncoderId);

        if (data != null && data.Length > 1)
        {
            *//*mediaTexture.LoadRawTextureData(data);
            mediaTexture.Apply();*//*

            _videoPanelUI.SetBytes(data);
            saveCount++;

            calibrationText.GetComponent<TextMeshPro>().text = data.Length + " : Calibration: " + saveCount + " images saved";
        }
        else
        {
            calibrationText.GetComponent<TextMeshPro>().text = TAG + ": data[0]";
        }*/

        using (var input = upBitmap.LockBuffer(BitmapBufferAccessMode.Read))
        using (var inputReference = input.CreateReference())
        {
            byte* inputBytes;
            uint inputCapacity;
            ((IMemoryBufferByteAccess)inputReference).GetBuffer(out inputBytes, out inputCapacity);

            // 该部分用时过多
            mediaTexture.LoadRawTextureData((IntPtr)inputBytes, videoWidth * videoHeight * 4);
            mediaTexture.Apply();

            /*Texture2D frameTexture = new Texture2D(videoWidth, videoHeight, TextureFormat.BGRA32, false);
            frameTexture.LoadRawTextureData((IntPtr)inputBytes, videoWidth * videoHeight * 4);
            frameTexture.Apply();*/

            //byte[] bytes = mediaTexture.EncodeToJPG();
            //_videoPanelUI.SetBytes(bytes);

            _videoPanelUI.SetTexture(mediaTexture);

            DebugLog(saveCount + ": Trigger sending or saving");
            saveCount++;
        }
    }


    private async Task<byte[]> EncodedBytes(SoftwareBitmap soft, Guid encoderId)
    {
        byte[] array = null;

        // First: Use an encoder to copy from SoftwareBitmap to an in-mem stream (FlushAsync)
        // Next:  Use ReadAsync on the in-mem stream to get byte[] array

        using (var ms = new InMemoryRandomAccessStream())
        {
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, ms);
            encoder.SetSoftwareBitmap(soft);

            encoder.BitmapTransform.ScaledWidth = 256;
            encoder.BitmapTransform.ScaledHeight = 144;
            //encoder.BitmapTransform.Rotation = BitmapRotation.Clockwise180Degrees;

            encoder.IsThumbnailGenerated = true;

            try
            {
                await encoder.FlushAsync();
            }
            catch (Exception ex)
            {
                DebugLog(TAG + ": byte[0] : " + ex);
                return array;
            }

            array = new byte[ms.Size];
            await ms.ReadAsync(array.AsBuffer(), (uint)ms.Size, InputStreamOptions.None);
        }
        return array;
    }

    private void OnApplicationQuit()
    {
        if (captureStatus == CaptureStatus.Running)
        {
            isRunning = false;
            StopFrameReaderAsyncWrapper();
        }
    }

    public void ToggleVideo()
    {
        //DebugLog(TAG + " ToggleVideo()");
        if (captureStatus == CaptureStatus.Initialized)
        {
            //DebugLog(TAG + ": CaptureStatus.Initialized");
            StartFrameReaderAsyncWrapper();
        }
        else if (captureStatus == CaptureStatus.Running)
        {
            //DebugLog(TAG + ": CaptureStatus.Running");
            StopFrameReaderAsyncWrapper();
        }
    }

#else

    public void ToggleVideo()
    {
        ;
    }

    private void Start()
    {
        debugText.GetComponent<TextMeshPro>().text = "Calibration not started";
        calibrationText.GetComponent<TextMeshPro>().text = "Calibration not started";
    }

#endif

    //点击启动ToggleVideo();代替msg="s"
    public void ClickS()
    {
        ToggleVideo();
    }


}