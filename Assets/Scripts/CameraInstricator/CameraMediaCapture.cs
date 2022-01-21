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
using Windows.Perception.Spatial;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using System.Threading.Tasks;
using System;
using Windows.Graphics.Imaging;
using System.Threading;
using System.Linq;

using wMatrix4x4 = System.Numerics.Matrix4x4;
using Windows.Storage.Streams;
#endif


public class CameraMediaCapture : MonoBehaviour
{
    //public Material mediaMaterial;
    private Texture2D mediaTexture;

    public enum MediaCaptureProfiles
    {
        HL1_1280x720,
        HL1_1408x792,
        HL1_1344x756,
        HL1_896x504,
        HL2_2272x1278,
        HL2_896x504
    }

    public MediaCaptureProfiles mediaCaptureProfiles;
    private int _targetVideoWidth, _targetVideoHeight;
    private float _targetVideoFrameRate = 30f;

    public GameObject calibrationText;
    public GameObject debugText;
    public GameObject CameraText;

    public InternetHandler internetHandler;


    void DebugLog(string s)
    {
        debugText.GetComponent<TextMeshPro>().text = s;
    }

    void CameraLog(string s)
    {
        CameraText.GetComponent<TextMeshPro>().text = s;
    }


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


#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)

    static readonly Guid MFSampleExtension_Spatial_CameraCoordinateSystem = new Guid("9D13C82F-2199-4E67-91CD-D1A4181F2534");
    static readonly Guid MFSampleExtension_Spatial_CameraProjectionTransform = new Guid("47F9FCB5-2A02-4F26-A477-792FDF95886A");
    static readonly Guid MFSampleExtension_Spatial_CameraViewTransform = new Guid("4E251FA4-830F-4770-859A-4B8D99AA809B");

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

    /*private MediaCapture depthMediaCapture;
    private MediaFrameReader depthFrameReader = null;
*/
    private int videoWidth = 0;
    private int videoHeight = 0;
    private int HL = 0;

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

        //depthMediaCapture = new MediaCapture();
        //MediaStreamType depthMediaStreamType = MediaStreamType.VideoRecord;

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

            // Depth Camera
            //string depthDeviceId = allGroups[0].Id;
            // Look up for all video profiles
            //IReadOnlyList<MediaCaptureVideoProfile> depthProfileList = MediaCapture.FindAllVideoProfiles(depthDeviceId);
            // MediaCaptureVideoProfile selectedProfile;
            // IReadOnlyList<MediaCaptureVideoProfile> profileList = MediaCapture.FindKnownVideoProfiles(deviceId, KnownVideoProfile.VideoConferencing);

            // Initialize mediacapture with the source group.
            /*var depthSettings = new MediaCaptureInitializationSettings
            {
                SourceGroup = allGroups[0],
                //VideoDeviceId = deviceId,
                //VideoProfile = profileList[0],
                // This media capture can share streaming with other apps.
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                // Only stream video and don't initialize audio capture devices.
                StreamingCaptureMode = StreamingCaptureMode.Video,
                // Set to CPU to ensure frames always contain CPU SoftwareBitmap images
                // instead of preferring GPU D3DSurface images.
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };*/


            try
            {
                await mediaCapture.InitializeAsync(settings);
                //await depthMediaCapture.InitializeAsync(depthSettings);

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

            //Depth Camera
            //var depthMediaFrameSourceVideo = depthMediaCapture.FrameSources.Values.Single(x => x.Info.MediaStreamType == mediaStreamType);
            //MediaFrameFormat depthTargetResFormat = depthMediaFrameSourceVideo.SupportedFormats[0];


            await mediaFrameSourceVideo.SetFormatAsync(targetResFormat);
            //await depthMediaFrameSourceVideo.SetFormatAsync(depthTargetResFormat);

            frameReader = await mediaCapture.CreateFrameReaderAsync(mediaFrameSourceVideo, targetResFormat.Subtype);
           // depthFrameReader = await depthMediaCapture.CreateFrameReaderAsync(depthMediaFrameSourceVideo, depthTargetResFormat.Subtype);

            frameReader.FrameArrived += OnFrameArrived;
            /*depthFrameReader.FrameArrived += OnDepthFrameArrived;

            depthFrameRateNumerator = depthTargetResFormat.FrameRate.Numerator;
            depthFrameRateDenominator = depthTargetResFormat.FrameRate.Denominator;
*/
            videoWidth = Convert.ToInt32(targetResFormat.VideoFormat.Width);
            videoHeight = Convert.ToInt32(targetResFormat.VideoFormat.Height);
            
            frameRateNumerator = targetResFormat.FrameRate.Numerator;
            frameRateDenominator = targetResFormat.FrameRate.Denominator;
        }
        catch (Exception e)
        {
            DebugLog(TAG + ": FrameReader is not initialized: " + e);
            return false;
        }

        captureStatus = CaptureStatus.Initialized;
        return true;
    }


    static SpatialCoordinateSystem GetRgbFrameProjectionAndCoordinateSystemDetails(
        MediaFrameReference rgbFrame,
        out wMatrix4x4 projectionTransform, 
        out wMatrix4x4 viewTransform,
        out wMatrix4x4 invertedViewTransform)
    {
        SpatialCoordinateSystem rgbCoordinateSystem = null;
        viewTransform = wMatrix4x4.Identity;
        projectionTransform = wMatrix4x4.Identity;
        invertedViewTransform = wMatrix4x4.Identity;

        object value;

        if (rgbFrame.Properties.TryGetValue(MFSampleExtension_Spatial_CameraCoordinateSystem, out value))
        {
            // I'm not sure that this coordinate system changes per-frame so I could maybe do this once?
            rgbCoordinateSystem = value as SpatialCoordinateSystem;
        }
        if (rgbFrame.Properties.TryGetValue(MFSampleExtension_Spatial_CameraProjectionTransform, out value))
        {
            // I don't think that this transform changes per-frame so I could maybe do this once?
            projectionTransform = ByteArrayToMatrix(value as byte[]);
        }
        if (rgbFrame.Properties.TryGetValue(MFSampleExtension_Spatial_CameraViewTransform, out value))
        {
            // I think this transform changes per frame.
            viewTransform = ByteArrayToMatrix(value as byte[]);
            wMatrix4x4.Invert(viewTransform, out invertedViewTransform);
        }
        return (rgbCoordinateSystem);
    }


    uint frameRateNumerator = 0;
    uint frameRateDenominator = 0;

    private async Task<bool> StartFrameReaderAsync()
    {
        //DebugLog(TAG + " StartFrameReaderAsync() thread ID is " + Thread.CurrentThread.ManagedThreadId);
        if (captureStatus != CaptureStatus.Initialized)
        {
            //DebugLog(TAG + ": StartFrameReaderAsync() fails because of incorrect status");
            return false;
        }

        MediaFrameReaderStartStatus status = await frameReader.StartAsync();

        // Depth Camera
        //MediaFrameReaderStartStatus depthStatus = await depthFrameReader.StartAsync();

        if (status == MediaFrameReaderStartStatus.Success)
        {
            //DebugLog(TAG + ": StartFrameReaderAsync() is successful");
            captureStatus = CaptureStatus.Running;
            return true;
        }
        else
        {
            DebugLog(TAG + ": StartFrameReaderAsync() is not successful, status = " + status);
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

        /*await depthFrameReader.StopAsync();
        depthFrameReader.FrameArrived -= OnDepthFrameArrived;
        depthMediaCapture.Dispose();
        depthMediaCapture = null;
*/
        captureStatus = CaptureStatus.Initialized;
        DebugLog(TAG + ": StopFrameReaderAsync() is successful");
        return true;
    }
/*
    private bool onDepthFrameArrivedProcessing = false;
    string depth_text = "";
    bool isDepthReady = false;
    //byte[] rgb_bytes;
    string depth_byte_text = "";
    bool isDepth_bytesReady = false;

    uint depthFrameRateNumerator;
    uint depthFrameRateDenominator;

    private unsafe void OnDepthFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (onDepthFrameArrivedProcessing)
        {
            //DebugLog(TAG + " OnFrameArrived() is still processing");
            return;
        }
        onDepthFrameArrivedProcessing = true;
        using (var frame = sender.TryAcquireLatestFrame())
        {
            if (frame != null)
            {
                //fpsDisplayHUD.PreviewTick();

                if (frame.VideoMediaFrame.CameraIntrinsics != null)
                {
                    // 打印一下depthCamera的内置参数
                    //DepthLog(depthFrame.VideoMediaFrame.CameraIntrinsics.ToString());
                    depth_text = "DepthCamera : \n";
                    depth_text += "[ImageWidth, ImageHeight] : [" + frame.VideoMediaFrame.CameraIntrinsics.ImageWidth + ", " + frame.VideoMediaFrame.CameraIntrinsics.ImageHeight + "]\n";
                    depth_text += "[FocalLength.X, FocalLength.Y] : [" + frame.VideoMediaFrame.CameraIntrinsics.FocalLength.X + ", " + frame.VideoMediaFrame.CameraIntrinsics.FocalLength.Y + "]\n";
                    depth_text += "[PrincipalPoint.X, PrincipalPoint.Y] : [" + frame.VideoMediaFrame.CameraIntrinsics.PrincipalPoint.X + ", " + frame.VideoMediaFrame.CameraIntrinsics.PrincipalPoint.Y + "]\n";
                    depth_text += "[RadialDistortion] : [" + frame.VideoMediaFrame.CameraIntrinsics.RadialDistortion + "]\n";
                    depth_text += "[TangentialDistortion] : [" + frame.VideoMediaFrame.CameraIntrinsics.TangentialDistortion + "]\n";
                    depth_text += "[UndistortedProjectionTransform] : [" + frame.VideoMediaFrame.CameraIntrinsics.UndistortedProjectionTransform + "]\n";
                    depth_text += "DepthFrameRate : " + depthFrameRateNumerator + "/" + depthFrameRateDenominator + "\n";

                    isDepthReady = true;
                }
                else
                {
                    depth_text = "RgbCamera : NUll";
                    isDepthReady = true;
                }

                wMatrix4x4 projectionTransform = wMatrix4x4.Identity;
                wMatrix4x4 viewTransform = wMatrix4x4.Identity;
                wMatrix4x4 invertedViewTransform = wMatrix4x4.Identity;
                var rgbCoordinateSystem = GetRgbFrameProjectionAndCoordinateSystemDetails(
                    frame, out projectionTransform, out viewTransform, out invertedViewTransform);

                depth_byte_text = "DepthCamera:\n";
                depth_byte_text += "projectionTransform : " + projectionTransform + "\n" +
                                  "viewTransform : " + viewTransform + "\n" +
                                  "invertedViewTransform : " + invertedViewTransform + "\n";
                isDepth_bytesReady = true;

                /*var softwareBitmap = SoftwareBitmap.Convert(frame.VideoMediaFrame.SoftwareBitmap, BitmapPixelFormat.Rgba8, BitmapAlphaMode.Ignore);
                Interlocked.Exchange(ref _tempBitmap, softwareBitmap);*//*
                frame.VideoMediaFrame.SoftwareBitmap?.Dispose();
            }
        }
        onDepthFrameArrivedProcessing = false;
    }
*/

    string rgb_text = "";
    bool isRgbReady = false;
    byte[] rgb_bytes;
    string rgb_depth_text = "";
    bool isRgb_bytesReady = false;
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
                //fpsDisplayHUD.PreviewTick();

                if (frame.VideoMediaFrame.CameraIntrinsics != null)
                {
                    // 打印一下depthCamera的内置参数
                    //DepthLog(depthFrame.VideoMediaFrame.CameraIntrinsics.ToString());
                    rgb_text = "RgbCamera : \n";
                    rgb_text += "[ImageWidth, ImageHeight] : [" + frame.VideoMediaFrame.CameraIntrinsics.ImageWidth + ", " + frame.VideoMediaFrame.CameraIntrinsics.ImageHeight + "]\n";
                    rgb_text += "[FocalLength.X, FocalLength.Y] : [" + frame.VideoMediaFrame.CameraIntrinsics.FocalLength.X + ", " + frame.VideoMediaFrame.CameraIntrinsics.FocalLength.Y + "]\n";
                    rgb_text += "[PrincipalPoint.X, PrincipalPoint.Y] : [" + frame.VideoMediaFrame.CameraIntrinsics.PrincipalPoint.X + ", " + frame.VideoMediaFrame.CameraIntrinsics.PrincipalPoint.Y + "]\n";
                    rgb_text += "[RadialDistortion] : [" + frame.VideoMediaFrame.CameraIntrinsics.RadialDistortion + "]\n";
                    rgb_text += "[TangentialDistortion] : [" + frame.VideoMediaFrame.CameraIntrinsics.TangentialDistortion + "]\n";
                    rgb_text += "[UndistortedProjectionTransform] : [" + frame.VideoMediaFrame.CameraIntrinsics.UndistortedProjectionTransform + "]\n";
                    isRgbReady = true;
                }
                else
                {
                    rgb_text = "RgbCamera : NUll";
                    isRgbReady = true;
                }

                wMatrix4x4 projectionTransform = wMatrix4x4.Identity;
                wMatrix4x4 viewTransform = wMatrix4x4.Identity;
                wMatrix4x4 invertedViewTransform = wMatrix4x4.Identity;
                var rgbCoordinateSystem = GetRgbFrameProjectionAndCoordinateSystemDetails(
                    frame, out projectionTransform, out viewTransform, out invertedViewTransform);

                rgb_depth_text = "RGBCamera:\n";
                rgb_depth_text += "projectionTransform : " + projectionTransform + "\n" +
                                  "viewTransform : " + viewTransform + "\n" +
                                  "invertedViewTransform : " + invertedViewTransform + "\n";
                isRgb_bytesReady = true;

                var softwareBitmap = SoftwareBitmap.Convert(frame.VideoMediaFrame.SoftwareBitmap, BitmapPixelFormat.Rgba8, BitmapAlphaMode.Ignore);
                Interlocked.Exchange(ref _tempBitmap, softwareBitmap);
                frame.VideoMediaFrame.SoftwareBitmap?.Dispose();
            }
        }
        onFrameArrivedProcessing = false;
    }

    private void Update()
    {
        if (isRgbReady)
        {
            CameraLog(rgb_text);
        }
        if (isRgb_bytesReady)
        {
            internetHandler.UpdateDataToSend(Encoding.UTF8.GetBytes(rgb_text + rgb_depth_text));
            DebugLog(rgb_depth_text);
            isRgb_bytesReady = false;
        }

/*        if (isDepthReady)
        {
            CameraLog(depth_text);
        }
        if (isDepth_bytesReady)
        {
            internetHandler.UpdateDataToSend(Encoding.UTF8.GetBytes(depth_text + depth_byte_text));
            DebugLog(depth_byte_text);
            isDepth_bytesReady = false;
        }
*/    }

    string MatrixByteToString(wMatrix4x4 matrix)
    {
        string matrixArray =
            matrix.M11.ToString("0.00") + ", " + matrix.M12.ToString("0.00") + ", " + matrix.M13.ToString("0.00") + ", " + matrix.M14.ToString("0.00") + "\n" +
            matrix.M21.ToString("0.00") + ", " + matrix.M22.ToString("0.00") + ", " + matrix.M23.ToString("0.00") + ", " + matrix.M24.ToString("0.00") + "\n" +
            matrix.M31.ToString("0.00") + ", " + matrix.M32.ToString("0.00") + ", " + matrix.M33.ToString("0.00") + ", " + matrix.M34.ToString("0.00") + "\n" +
            matrix.M41.ToString("0.00") + ", " + matrix.M42.ToString("0.00") + ", " + matrix.M43.ToString("0.00") + ", " + matrix.M44.ToString("0.00");
        return matrixArray;
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
    void Start()
    {
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
            default:
                break;
        }

        isRunning = true;
        StartCoroutine("GetFrame");
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
                GetFrameTextureAsyncWrapperAsync();
            }

            if (captureStatus == CaptureStatus.Clean)
                calibrationText.GetComponent<TextMeshPro>().text = "Calibration not started";
            else if (captureStatus == CaptureStatus.Initialized)
                calibrationText.GetComponent<TextMeshPro>().text = "Calibration initializing";
            else if (captureStatus == CaptureStatus.Running)
                calibrationText.GetComponent<TextMeshPro>().text = 
                    videoHeight + ":" + videoWidth + " : " + frameRateNumerator + "/" + frameRateDenominator
                    + "\nCalibration: " + saveCount + " images saved";

            yield return new WaitForSeconds(0.1f);
        }
    }

    async void GetFrameTextureAsyncWrapperAsync()
    {
        await GetFrameTextureAsync();
    }

    async Task<bool> GetFrameTextureAsync()
    {
        Interlocked.Exchange(ref upBitmap, _tempBitmap);

        byte[] data = await EncodedBytes(upBitmap, BitmapEncoder.JpegEncoderId);

        if (data != null && data.Length > 0)
        {
            //InternetHandler + 发送字节
            //_videoPanelUI.SetBytes(data);
            
            //internetHandler.UpdateDataToSend(data);

            saveCount++;

            //DebugLog(data.Length + " : Calibration: " + saveCount + " images saved");
        }
        else
        {
            DebugLog(TAG + ": data[0]");
        }

        return true;
        /*using (var input = upBitmap.LockBuffer(BitmapBufferAccessMode.Read))
        using (var inputReference = input.CreateReference())
        {
            byte* inputBytes;
            uint inputCapacity;
            ((IMemoryBufferByteAccess)inputReference).GetBuffer(out inputBytes, out inputCapacity);

            // 该部分用时过多
            mediaTexture.LoadRawTextureData((IntPtr)inputBytes, videoWidth * videoHeight * 4);
            mediaTexture.Apply();

            *//*Texture2D frameTexture = new Texture2D(videoWidth, videoHeight, TextureFormat.BGRA32, false);
            frameTexture.LoadRawTextureData((IntPtr)inputBytes, videoWidth * videoHeight * 4);
            frameTexture.Apply();*//*

            //byte[] bytes = mediaTexture.EncodeToJPG();
            //_videoPanelUI.SetBytes(bytes);

            _videoPanelUI.SetTexture(mediaTexture);

            DebugLog(saveCount + ": Trigger sending or saving");
            saveCount++;
        }*/
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