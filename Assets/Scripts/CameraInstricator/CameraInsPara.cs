using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.XR.WSA;
#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
using Windows.Perception.Spatial;
using Windows.Media.FaceAnalysis;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Devices;
using Windows.Media.Devices.Core;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using uVector3 = UnityEngine.Vector3;
using wVector3 = System.Numerics.Vector3;
using wVector4 = System.Numerics.Vector4;
using wMatrix4x4 = System.Numerics.Matrix4x4;
#else

#endif

class MediaCapture_FrameSource
{
#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
    public MediaCapture mediaCapture;
    public MediaFrameSource frameSource;

    public MediaCapture_FrameSource(MediaCapture mediaCapture, MediaFrameSource frameSource)
    {
        this.mediaCapture = mediaCapture;
        this.frameSource = frameSource;
    }
#else

#endif
}

[ComImport]
[Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
unsafe interface IMemoryBufferByteAccess
{
    void GetBuffer(out byte* buffer, out uint capacity);
}


public class CameraInsPara : MonoBehaviour
{
    //public GameObject cameraText;
    public GameObject debugText;
    public InternetHandler internetHandler;

    public GameObject rgbText;
    public GameObject depthText;

#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)

    static readonly Guid MFSampleExtension_Spatial_CameraCoordinateSystem = new Guid("9D13C82F-2199-4E67-91CD-D1A4181F2534");
    static readonly Guid MFSampleExtension_Spatial_CameraProjectionTransform = new Guid("47F9FCB5-2A02-4F26-A477-792FDF95886A");
    static readonly Guid MFSampleExtension_Spatial_CameraViewTransform = new Guid("4E251FA4-830F-4770-859A-4B8D99AA809B");
    static readonly float MAGIC_DEPTH_FRAME_HEIGHT_RATIO_CENTRE = 0.25f;
    static readonly float MAGIC_DEPTH_FRAME_WIDTH_RATIO_CENTRE = 0.5f;


    private CameraIntrinsics cameraIntrinsics;
    private MediaCapture mediaCapture;
    private MediaFrameReader frameReader = null;

    private int videoWidth = 0;
    private int videoHeight = 0;
    private int HL = 0;

    private bool isReady = false;

    void DebugLog(string s)
    {
        debugText.transform.GetComponent<TextMeshPro>().text = s;
    }

    /*void CameraLog(string s)
    {
        cameraText.transform.GetComponent<TextMeshPro>().text = s;
    }*/
    void DepthLog(string s)
    {
        depthText.transform.GetComponent<TextMeshPro>().text = s;
    }

    void RgbLog(string s)
    {
        rgbText.transform.GetComponent<TextMeshPro>().text = s;
    }

    async void GetAllGroupSourceAsyncWrapper()
    {
        await GetAllGroupSource();
    }

#if HUNT_DEPTH_PIXEL_GRID

    static readonly int DEPTH_SEARCH_GRID_SIZE = 32;

#endif // HUNT_DEPTH_PIXEL_GRID

    // Start is called before the first frame update
    void Start()
    {
        isReady = false;
        GetAllGroupSourceAsyncWrapper();

        ProcessingLoopAsync();
    }

    string camText = "";
    private async Task<bool> GetAllGroupSource()
    {
        var allGroups = await MediaFrameSourceGroup.FindAllAsync();
        //打印所有camera SourceInfo
        foreach (var group in allGroups)
        {
            camText += $"Group {group.DisplayName}\n";
            foreach (var source in group.SourceInfos)
            {
                camText += $"\tSource {source.MediaStreamType}, {source.SourceKind}, {source.Id}\n";

                foreach (var profile in source.VideoProfileMediaDescription)
                {
                    camText += $"\t\tProfile {profile.Width}x{profile.Height}@{profile.FrameRate:N0}\n";
                }
            }
        }
        isReady = true;
        /*try
        {
            CameraLog(camText);
        }
        catch(Exception e)
        {
            camText = e.ToString();
        }*/
        return true;
    }


    async Task<MediaCapture_FrameSource> GetMediaCaptureForDescriptionAsync(
           MediaFrameSourceKind sourceKind,
           int width,
           int height,
           int frameRate,
           string[] bitmapFormats = null)
    {
        MediaCapture mediaCapture = null;
        MediaFrameSource frameSource = null;

        var allSources = await MediaFrameSourceGroup.FindAllAsync();

        // Ignore frame rate here on the description as both depth streams seem to tell me they are
        // 30fps whereas I don't think they are (from the docs) so I leave that to query later on.
        // NB: LastOrDefault here is a NASTY, NASTY hack - just my way of getting hold of the 
        // *LAST* depth stream rather than the *FIRST* because I'm assuming that the *LAST*
        // one is the longer distance stream rather than the short distance stream.
        // I should fix this and find a better way of choosing the right depth stream rather
        // than relying on some ordering that's not likely to always work!
        var sourceInfo =
            allSources.SelectMany(group => group.SourceInfos)
            .LastOrDefault(
                si =>
                    (si.MediaStreamType == MediaStreamType.VideoRecord) &&
                    (si.SourceKind == sourceKind) &&
                    (si.VideoProfileMediaDescription.Any(
                        desc =>
                            desc.Width == width &&
                            desc.Height == height &&
                            desc.FrameRate == frameRate)));

        if (sourceInfo != null)
        {
            var sourceGroup = sourceInfo.SourceGroup;

            mediaCapture = new MediaCapture();

            await mediaCapture.InitializeAsync(
               new MediaCaptureInitializationSettings()
               {
                   // I want software bitmaps
                   MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                   SourceGroup = sourceGroup,
                   StreamingCaptureMode = StreamingCaptureMode.Video
               }
            );
            frameSource = mediaCapture.FrameSources[sourceInfo.Id];

            var selectedFormat = frameSource.SupportedFormats.First(
                format =>
                    format.VideoFormat.Width == width && format.VideoFormat.Height == height &&
                    format.FrameRate.Numerator / format.FrameRate.Denominator == frameRate &&
                    ((bitmapFormats == null) || (bitmapFormats.Contains(format.Subtype.ToLower()))));

            await frameSource.SetFormatAsync(selectedFormat);
        }
        MediaCapture_FrameSource mediaCapture_frameSource = new MediaCapture_FrameSource(mediaCapture, frameSource);
        return mediaCapture_frameSource;
    }


    /// <summary>
    /// This is just one big lump of code right now which should be factored out into some kind of
    /// 'frame reader' class which can then be subclassed for depth frame and video frame but
    /// it was handy to have it like this while I experimented with it - the intention was
    /// to tidy it up if I could get it doing more or less what I wanted :-)
    /// </summary>
    string depth_text = "";
    byte[] depth_bytes;
    bool isDepth_bytesReady = false;
    bool isDepthReady = false;
    string rgb_text = "";
    byte[] rgb_bytes;
    bool isRgb_bytesReady = false;
    bool isRgbReady = false;
    async Task ProcessingLoopAsync()
    {
      
        long rgbProcessedCount = 0;
        long facesPresentCount = 0;
        long rgbDroppedCount = 0;

        MediaFrameReference lastRgbFrame = null;

        var faceBitmapFormats = FaceTracker.GetSupportedBitmapPixelFormats().Select(
            format => format.ToString().ToLower()).ToArray();

        var faceTracker = await FaceTracker.CreateAsync();

        var rgbMediaCapture = await this.GetMediaCaptureForDescriptionAsync(
            MediaFrameSourceKind.Color, 896, 504, 30, faceBitmapFormats);

        if (rgbMediaCapture.frameSource == null || rgbMediaCapture.mediaCapture == null)
        {
            rgb_text = "RgbCamera : mediaCapture || frameSource : NUll";
            isRgbReady = true;
            return;
        }


        var rgbFrameReader = await rgbMediaCapture.mediaCapture.CreateFrameReaderAsync(rgbMediaCapture.frameSource);

        rgbFrameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;

        int busyProcessingRgbFrame = 0;

        /*var unityWorldCoordinateSystem =
            Marshal.GetObjectForIUnknown(WorldManager.GetNativeISpatialCoordinateSystemPtr()) as SpatialCoordinateSystem;
*/
        // Expecting this to run at 30fps.
        TypedEventHandler<MediaFrameReader, MediaFrameArrivedEventArgs> rgbFrameHandler =
           (sender, args) =>
           {
               using (var rgbFrame = rgbFrameReader.TryAcquireLatestFrame())
               {
                   if ((rgbFrame != null) && (rgbFrame != lastRgbFrame))
                   {
                       ++rgbProcessedCount;

                       lastRgbFrame = rgbFrame;
                       var facePosition = uVector3.zero;


                       if (rgbFrame.VideoMediaFrame.CameraIntrinsics != null)
                       {
                           // 打印一下depthCamera的内置参数
                           //DepthLog(depthFrame.VideoMediaFrame.CameraIntrinsics.ToString());
                           rgb_text = "DepthCamera : \n";
                           rgb_text += "[ImageWidth, ImageHeight] : [" + rgbFrame.VideoMediaFrame.CameraIntrinsics.ImageWidth + ", " + rgbFrame.VideoMediaFrame.CameraIntrinsics.ImageHeight + "]\n";
                           rgb_text += "[FocalLength.X, FocalLength.Y] : [" + rgbFrame.VideoMediaFrame.CameraIntrinsics.FocalLength.X + ", " + rgbFrame.VideoMediaFrame.CameraIntrinsics.FocalLength.Y + "]\n";
                           rgb_text += "[PrincipalPoint.X, PrincipalPoint.Y] : [" + rgbFrame.VideoMediaFrame.CameraIntrinsics.PrincipalPoint.X + ", " + rgbFrame.VideoMediaFrame.CameraIntrinsics.PrincipalPoint.Y + "]\n";
                           rgb_text += "[RadialDistortion] : [" + rgbFrame.VideoMediaFrame.CameraIntrinsics.RadialDistortion + "]\n";
                           rgb_text += "[TangentialDistortion] : [" + rgbFrame.VideoMediaFrame.CameraIntrinsics.TangentialDistortion + "]\n";
                           rgb_text += "[UndistortedProjectionTransform] : [" + rgbFrame.VideoMediaFrame.CameraIntrinsics.UndistortedProjectionTransform + "]\n";
                           rgb_text += "[UndistortedProjectionTransform] : [" + MatrixByteToArray(rgbFrame.VideoMediaFrame.CameraIntrinsics.UndistortedProjectionTransform) + "]\n";
                           isRgbReady = true;
                       }
                       else
                       {
                           rgb_text = "RgbCamera : NUll";
                           isRgbReady = true;
                       }


                       SpatialCoordinateSystem w_coordinateSystem = null;
                       byte[] w_viewTransform = null;
                       byte[] w_projectionTransform = null;
                       object w_value;

                       if (rgbFrame.Properties.TryGetValue(MFSampleExtension_Spatial_CameraCoordinateSystem, out w_value))
                       {
                           w_coordinateSystem = w_value as SpatialCoordinateSystem;
                       }
                       if (rgbFrame.Properties.TryGetValue(MFSampleExtension_Spatial_CameraProjectionTransform, out w_value))
                       {
                           w_projectionTransform = w_value as byte[];
                       }
                       if (rgbFrame.Properties.TryGetValue(MFSampleExtension_Spatial_CameraViewTransform, out w_value))
                       {
                           w_viewTransform = w_value as byte[];
                       }
                       byte[] split = Encoding.UTF8.GetBytes("000RGB000");
                       rgb_bytes = new byte[w_projectionTransform.Length + split.Length + w_viewTransform.Length];
                       w_projectionTransform.CopyTo(rgb_bytes, 0);
                       split.CopyTo(rgb_bytes, w_projectionTransform.Length);
                       w_viewTransform.CopyTo(rgb_bytes, w_projectionTransform.Length + split.Length);
                       isRgb_bytesReady = true;

                       

                       #region FaceTrack
                       /*using (var videoFrame = rgbFrame.VideoMediaFrame.GetVideoFrame())
                       {
                           var faces = await faceTracker.ProcessNextFrameAsync(videoFrame);
                           var firstFace = faces.FirstOrDefault();

                           if (firstFace != null)
                           {
                               ++facesPresentCount;

                               // Take the first face and the centre point of that face to try
                               // and simplify things for my limited brain.
                               var faceCentrePointInImageCoords =
                                  new Point(
                                      firstFace.FaceBox.X + (firstFace.FaceBox.Width / 2.0f),
                                      firstFace.FaceBox.Y + (firstFace.FaceBox.Height / 2.0f));

                               wMatrix4x4 projectionTransform = wMatrix4x4.Identity;
                               wMatrix4x4 viewTransform = wMatrix4x4.Identity;
                               wMatrix4x4 invertedViewTransform = wMatrix4x4.Identity;

                               var rgbCoordinateSystem = GetRgbFrameProjectionAndCoordinateSystemDetails(
                                   rgbFrame, out projectionTransform, out invertedViewTransform);

                               // Scale the RGB point (1280x720)
                               var faceCentrePointUnitScaleRGB = ScalePointMinusOneToOne(faceCentrePointInImageCoords, rgbFrame);

                               // Unproject the RGB point back at unit depth as per the locatable camera
                               // document.
                               var unprojectedFaceCentrePointRGB = UnProjectVector(
                                      new wVector3(
                                          (float)faceCentrePointUnitScaleRGB.X,
                                          (float)faceCentrePointUnitScaleRGB.Y,
                                          1.0f),
                                      projectionTransform);

                               // Transfrom this back by the inverted view matrix in order to put this into
                               // the RGB camera coordinate system
                               var faceCentrePointCameraCoordsRGB =
                                      wVector3.Transform(unprojectedFaceCentrePointRGB, invertedViewTransform);

                               // Get the transform from the camera coordinate system to the Unity world
                               // coordinate system, could probably cache this?
                               var cameraRGBToWorldTransform =
                                      rgbCoordinateSystem.TryGetTransformTo(unityWorldCoordinateSystem);

                               if (cameraRGBToWorldTransform.HasValue)
                               {
                                   // Transform to world coordinates
                                   var faceCentrePointWorldCoords = wVector4.Transform(
                                          new wVector4(
                                              faceCentrePointCameraCoordsRGB.X,
                                              faceCentrePointCameraCoordsRGB.Y,
                                              faceCentrePointCameraCoordsRGB.Z, 1),
                                          cameraRGBToWorldTransform.Value);

                                   // Where's the camera in world coordinates?
                                   var cameraOriginWorldCoords = wVector4.Transform(
                                          new wVector4(0, 0, 0, 1),
                                          cameraRGBToWorldTransform.Value);

                                   // Multiply Z by -1 for Unity
                                   var cameraPoint = new uVector3(
                                        cameraOriginWorldCoords.X,
                                        cameraOriginWorldCoords.Y,
                                        -1.0f * cameraOriginWorldCoords.Z);

                                   // Multiply Z by -1 for Unity
                                   var facePoint = new uVector3(
                                          faceCentrePointWorldCoords.X,
                                          faceCentrePointWorldCoords.Y,
                                          -1.0f * faceCentrePointWorldCoords.Z);

                                   facePosition =
                                       cameraPoint +
                                       (facePoint - cameraPoint).normalized * centrePointDepthInMetres;
                               }
                           }
                       }
                       if (facePosition != uVector3.zero)
                       {
                           UnityEngine.WSA.Application.InvokeOnAppThread(
                               () =>
                               {
                                   this.faceMarker.transform.position = facePosition;
                               },
                               false
                            );
                       }*/
                       #endregion
                   }
               }
           };


        var depthMediaCapture = await GetMediaCaptureForDescriptionAsync(MediaFrameSourceKind.Depth, 448, 450, 15);

        if (depthMediaCapture.frameSource == null || depthMediaCapture.mediaCapture == null)
        {
            depth_text = "DepthCamera : mediaCapture || frameSource : NUll";
            isDepthReady = true;
            return;
        }

        var depthFrameReader = await depthMediaCapture.mediaCapture.CreateFrameReaderAsync(depthMediaCapture.frameSource);

        depthFrameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;

        MediaFrameReference lastDepthFrame = null;

        long depthFrameCount = 0;
        float centrePointDepthInMetres = 0.0f;

        // Expecting this to run at 1fps although the API (seems to) reports that it runs at 15fps
        TypedEventHandler<MediaFrameReader, MediaFrameArrivedEventArgs> depthFrameHandler =
            (sender, args) =>
            {
                using (var depthFrame = sender.TryAcquireLatestFrame())
                {
                    if ((depthFrame != null) && (depthFrame != lastDepthFrame))
                    {
                        lastDepthFrame = depthFrame;

                        //Interlocked.Increment(ref depthFrameCount);

                        // Always try to grab the depth value although, clearly, this is subject
                        // to a bunch of race conditions etc. as other thread access it.
                        /*centrePointDepthInMetres =
                            GetDepthValueAtCoordinate(depthFrame,
                                (int)(depthFrame.Format.VideoFormat.Width * MAGIC_DEPTH_FRAME_WIDTH_RATIO_CENTRE),
                                (int)(depthFrame.Format.VideoFormat.Height * MAGIC_DEPTH_FRAME_HEIGHT_RATIO_CENTRE)) ?? 0.0f;
*/
                        if (depthFrame.VideoMediaFrame.CameraIntrinsics != null)
                        {
                            // 打印一下depthCamera的内置参数
                            //DepthLog(depthFrame.VideoMediaFrame.CameraIntrinsics.ToString());
                            depth_text = "DepthCamera : \n";
                            depth_text += "[ImageWidth, ImageHeight] : [" + depthFrame.VideoMediaFrame.CameraIntrinsics.ImageWidth + ", " + depthFrame.VideoMediaFrame.CameraIntrinsics.ImageHeight + "]\n";
                            depth_text += "[FocalLength.X, FocalLength.Y] : [" + depthFrame.VideoMediaFrame.CameraIntrinsics.FocalLength.X + ", " + depthFrame.VideoMediaFrame.CameraIntrinsics.FocalLength.Y + "]\n";
                            depth_text += "[PrincipalPoint.X, PrincipalPoint.Y] : [" + depthFrame.VideoMediaFrame.CameraIntrinsics.PrincipalPoint.X + ", " + depthFrame.VideoMediaFrame.CameraIntrinsics.PrincipalPoint.Y + "]\n";
                            depth_text += "[RadialDistortion] : [" + depthFrame.VideoMediaFrame.CameraIntrinsics.RadialDistortion + "]\n";
                            depth_text += "[TangentialDistortion] : [" + depthFrame.VideoMediaFrame.CameraIntrinsics.TangentialDistortion + "]\n";
                            depth_text += "[UndistortedProjectionTransform] : [" + depthFrame.VideoMediaFrame.CameraIntrinsics.UndistortedProjectionTransform + "]\n";
                            depth_text += "[UndistortedProjectionTransform] : [" + MatrixByteToArray(depthFrame.VideoMediaFrame.CameraIntrinsics.UndistortedProjectionTransform) + "]\n";
                            isDepthReady = true;
                        }
                        else
                        {
                            depth_text = "DepthCamera : NUll";
                            isDepthReady = true;
                        }


                        SpatialCoordinateSystem coordinateSystem = null;
                        byte[] viewTransform = null;
                        byte[] projectionTransform = null;
                        object value;

                        if (depthFrame.Properties.TryGetValue(MFSampleExtension_Spatial_CameraCoordinateSystem, out value))
                        {
                            coordinateSystem = value as SpatialCoordinateSystem;
                        }
                        if (depthFrame.Properties.TryGetValue(MFSampleExtension_Spatial_CameraProjectionTransform, out value))
                        {
                            projectionTransform = value as byte[];
                        }
                        if (depthFrame.Properties.TryGetValue(MFSampleExtension_Spatial_CameraViewTransform, out value))
                        {
                            viewTransform = value as byte[];
                        }
                        byte[] split = Encoding.UTF8.GetBytes("000Depth000");
                        depth_bytes = new byte[projectionTransform.Length + split.Length + viewTransform.Length];
                        projectionTransform.CopyTo(depth_bytes, 0);
                        split.CopyTo(depth_bytes, projectionTransform.Length);
                        viewTransform.CopyTo(depth_bytes, projectionTransform.Length + split.Length);
                        isDepth_bytesReady = true;
                    }
                }
            };


        depthFrameReader.FrameArrived += depthFrameHandler;
        rgbFrameReader.FrameArrived += rgbFrameHandler;

        await depthFrameReader.StartAsync();
        await rgbFrameReader.StartAsync();

        // Wait forever then dispose...just doing this to keep track of what needs disposing.
        await Task.Delay(-1);

        depthFrameReader.FrameArrived -= depthFrameHandler;
        rgbFrameReader.FrameArrived -= rgbFrameHandler;

        rgbFrameReader.Dispose();
        depthFrameReader.Dispose();

        rgbMediaCapture.mediaCapture.Dispose();
        depthMediaCapture.mediaCapture.Dispose();

        //Marshal.ReleaseComObject(unityWorldCoordinateSystem);
    }

    static SpatialCoordinateSystem GetRgbFrameProjectionAndCoordinateSystemDetails(
        MediaFrameReference rgbFrame,
        out wMatrix4x4 projectionTransform,
        out wMatrix4x4 invertedViewTransform)
    {
        SpatialCoordinateSystem rgbCoordinateSystem = null;
        wMatrix4x4 viewTransform = wMatrix4x4.Identity;
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

    unsafe static float? GetDepthValueAtCoordinate(MediaFrameReference frame, int x, int y)
    {
        float? depthValue = null;

        var bitmap = frame.VideoMediaFrame.SoftwareBitmap;

        using (var buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read))
        using (var reference = buffer.CreateReference())
        {
            var description = buffer.GetPlaneDescription(0);

            byte* pBits;
            uint size;
            var byteAccess = reference as IMemoryBufferByteAccess;

            byteAccess.GetBuffer(out pBits, out size);

            // Try the pixel value itself and see if we get anything there.
            depthValue = GetDepthValueFromBufferAtXY(
                pBits, x, y, description, (float)frame.VideoMediaFrame.DepthMediaFrame.DepthFormat.DepthScaleInMeters);

#if HUNT_DEPTH_PIXEL_GRID
            if (depthValue == null)
            {
                // If we don't have a value, look for one in the surrounding space (the sub-function copes
                // with us using bad values of x,y).
                var minDistance = double.MaxValue;

                for (int i = 0 - DEPTH_SEARCH_GRID_SIZE; i < DEPTH_SEARCH_GRID_SIZE; i++)
                {
                    for (int j = 0 - DEPTH_SEARCH_GRID_SIZE; j < DEPTH_SEARCH_GRID_SIZE; j++)
                    {
                        var newX = x + i;
                        var newY = y + j;

                        var testValue = GetDepthValueFromBufferAtXY(
                            pBits,
                            newX,
                            newY,
                            description,
                            (float)frame.VideoMediaFrame.DepthMediaFrame.DepthFormat.DepthScaleInMeters);

                        if (testValue != null)
                        {
                            var distance =
                                Math.Sqrt(Math.Pow(newX - x, 2.0) + Math.Pow(newY - y, 2.0));

                            if (distance < minDistance)
                            {
                                depthValue = testValue;
                                minDistance = distance;
                            }
                        }
                    }
                }
            }
#endif // HUNT_DEPTH_PIXEL_GRID
        }
        return (depthValue);
    }


    unsafe static float? GetDepthValueFromBufferAtXY(byte* pBits, int x, int y, BitmapPlaneDescription desc,
        float scaleInMeters)
    {
        float? depthValue = null;

        var bytesPerPixel = desc.Stride / desc.Width;
        Debug.Assert(bytesPerPixel == Marshal.SizeOf<UInt16>());

        int offset = (desc.StartIndex + desc.Stride * y) + (x * bytesPerPixel);

        if ((offset > 0) && (offset < ((long)pBits + (desc.Height * desc.Stride))))
        {
            depthValue = *(UInt16*)(pBits + offset) * scaleInMeters;

            if (!IsValidDepthDistance((float)depthValue))
            {
                depthValue = null;
            }
        }
        return (depthValue);
    }

    static bool IsValidDepthDistance(float depthDistance)
    {
        // If that depth value is > 4.0m then we discard it because it seems like 
        // 4.**m (4.09?) comes back from the sensor when it hasn't really got a value
        return ((depthDistance > 0.5f) && (depthDistance <= 4.0f));
    }



    static wMatrix4x4 ByteArrayToMatrix(byte[] bits)
    {
        var matrix = wMatrix4x4.Identity;

        var handle = GCHandle.Alloc(bits, GCHandleType.Pinned);
        matrix = Marshal.PtrToStructure<wMatrix4x4>(handle.AddrOfPinnedObject());
        handle.Free();

        return (matrix);
    }

    float[] MatrixByteToArray(wMatrix4x4 matrix)
    {
        var matrixValue = wMatrix4x4.Identity;
        float[] matrixArray = new float[]
        {
            matrix.M11, matrix.M12, matrix.M13, matrix.M14,
            matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            matrix.M41, matrix.M42, matrix.M43, matrix.M44
        };
        return matrixArray;
    }

    // Update is called once per frame
    void Update()
    {
        /*byte[] dataToBeProcessed = internetHandler.latestByte;

        if (dataToBeProcessed.Length == 0)
            return;

        var recvInfo = Encoding.UTF8.GetString(dataToBeProcessed);
        DebugLog(recvInfo);*/

        if (isReady)
        {
            internetHandler.UpdateDataToSend(Encoding.UTF8.GetBytes(camText));
            DebugLog(camText);
            isReady = false;
        }
        if (isDepth_bytesReady)
        {
            internetHandler.UpdateDataToSend(depth_bytes);
            isDepth_bytesReady = false;
        }
        if (isDepthReady)
        {
            DepthLog(depth_text);
            internetHandler.UpdateDataToSend(Encoding.UTF8.GetBytes(depth_text));
            isDepthReady = false;
        }
        if (isRgb_bytesReady)
        {
            internetHandler.UpdateDataToSend(rgb_bytes);
            isRgb_bytesReady = false;
        }
        if (isRgbReady)
        {
            RgbLog(rgb_text);
            internetHandler.UpdateDataToSend(Encoding.UTF8.GetBytes(rgb_text));
            isRgbReady = false;
        }
    }

#else

#endif
}

