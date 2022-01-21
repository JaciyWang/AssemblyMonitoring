using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.Input;
using System.Text;
using System;
using UnityEngine.UI;
using TMPro;
using wVector3 = System.Numerics.Vector3;
using wVector4 = System.Numerics.Vector4;
using wMatrix4x4 = System.Numerics.Matrix4x4;
#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
using Windows.Foundation;
using Windows.Media.Devices.Core;
#endif

public class GestureHandler : MonoBehaviour
{
    public float dist = 0f;

    public GameObject debugText;

    public bool show = false;
    public GameObject line = null;
    List<GameObject> fingerLinesL = new List<GameObject>();
    List<GameObject> fingerLinesR = new List<GameObject>();

    private Color[] colors = new Color[5] { Color.red, Color.yellow, Color.green, Color.white, Color.blue }; 

    public MediaCaptureHandler mediaCaptureHandler;
    wMatrix4x4 newWorldToCameraTransform = wMatrix4x4.Identity;

    Vector3[] fingerPositionRatioL = new Vector3[21];
    Vector3[] fingerPositionRatioR = new Vector3[21];

    MixedRealityPose pose;

    int times = 0;

    float width = Screen.width;
    float height = Screen.height;

    Vector3 max = new Vector3(100000, 100000, 100000);
    Vector3 min = new Vector3(-100000, -100000, -100000);

    private void Awake()
    {
        // -1表示没有探测到手部
        for (int i = 0; i < 21; i++)
        {
            fingerPositionRatioL[i].x = -1;
            fingerPositionRatioL[i].y = -1;
            fingerPositionRatioL[i].z = -1;
            fingerPositionRatioR[i].x = -1;
            fingerPositionRatioR[i].y = -1;
            fingerPositionRatioR[i].z = -1;
        }

        for(int j = 0; j < 5; j++)
        {
            GameObject obj_L = Instantiate(line, new Vector3(0, 0, 0), transform.localRotation) as GameObject;
            fingerLinesL.Add(obj_L);
            LineRenderer finger_line_L = fingerLinesL[j].GetComponent<LineRenderer>();
            finger_line_L.startColor = colors[j];
            finger_line_L.endColor = colors[j];
            if (j == 0)
            {
                finger_line_L.positionCount = 4;
            }
            else
            {
                finger_line_L.positionCount = 5;
            }

            GameObject obj_R = Instantiate(line, new Vector3(0, 0, 0), transform.localRotation) as GameObject;
            fingerLinesR.Add(obj_R);
            LineRenderer finger_line_R = fingerLinesR[j].GetComponent<LineRenderer>();
            finger_line_R.startColor = colors[j];
            finger_line_R.endColor = colors[j];
            if (j == 0)
            {
                finger_line_R.positionCount = 4;
            }
            else
            {
                finger_line_R.positionCount = 5;
            }
        }

        show = false;
    }

    // ~Metacarpal 接近手腕的关节，不考虑该点，就有21个点了，否则26个
    void Update()
    {
        times++;
        width = (float)Screen.width;
        height = (float)Screen.height;
        //print(width + "*" + height);

        // debugText.GetComponent<TextMeshPro>().text = Mathf.Ceil(width).ToString() + " * " + Mathf.Ceil(height).ToString();

#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
        // world->camera坐标系

        // Camera coordinate systems are right-handed with -Z pointing forward out from the camera,
        // but camera intrinsics are left-handed with +Z pointing forward out from the camera.
        // Therefore we need to transform left-handed 3D coordinates from the intrinsics to right-handed
        // and back again when applying the coordinate system transforms which expect right-handed coordinates.
        wMatrix4x4 leftToRight = wMatrix4x4.CreateScale(1.0f, 1.0f, -1.0f);
        wMatrix4x4 rightToLeft = leftToRight;
        newWorldToCameraTransform = leftToRight * mediaCaptureHandler.worldToCameraTransform * rightToLeft;
#endif

        showFingerLinesL(false);
        showFingerLinesR(false);

        // Left Hand
        int count = 1;
        for (int i = 0; i < 21; i++)
        {
            if (count % 5 == 2)
            {
                i--;
                count++;
                continue;
            }
            if (HandJointUtils.TryGetJointPose((TrackedHandJoint)count, Handedness.Left, out pose))
            {
#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)  
                if (mediaCaptureHandler.isIntrinsicsReady)
                {
                    wVector3 fingerPositionL = wVector3.Transform(new wVector3(pose.Position.x, pose.Position.y, pose.Position.z), newWorldToCameraTransform);

                    // Transform the point from camera to image space.
                    Point fingerPixelPointL = mediaCaptureHandler.cameraIntrinsics.ProjectOntoFrame(fingerPositionL);

                    // Scale up the coordinates to match the color image.
                    // We must also scale up the coordinates to the size of the color image canves in order
                    // to map the bones onto people in the frame correctly.                
                    fingerPositionRatioL[i].x = (float)(fingerPixelPointL.X / mediaCaptureHandler.cameraIntrinsics.ImageWidth);
                    fingerPositionRatioL[i].y = (float)(fingerPixelPointL.Y / mediaCaptureHandler.cameraIntrinsics.ImageHeight);
                    fingerPositionRatioL[i].z = fingerPositionL.Z;
                }
#else
                print(times + ": " + i + ": " + Camera.main.WorldToScreenPoint(pose.Position) + " " + pose.Position.z.ToString("0.000000"));

                /*wMatrix4x4 leftToRight = wMatrix4x4.CreateScale(1.0f, 1.0f, -1.0f);
                wMatrix4x4 rightToLeft = leftToRight;
                print(leftToRight);*/

                fingerPositionRatioL[i].x = Camera.main.WorldToScreenPoint(pose.Position).x / width;
                fingerPositionRatioL[i].y = 1.0f - Camera.main.WorldToScreenPoint(pose.Position).y / height;
                fingerPositionRatioL[i].z = Camera.main.WorldToScreenPoint(pose.Position).z;
#endif
                // 省去new，查看内存变化
                /*fingerPositionRatioL[i] = new Vector3(
                    Camera.main.WorldToScreenPoint(pose.Position).x / width,
                    1.0f - Camera.main.WorldToScreenPoint(pose.Position).y / height,
                    Camera.main.WorldToScreenPoint(pose.Position).z);*/
                //print(i + ": (" + fingerPositionRatioL[i].x.ToString("0.000000") + "," + fingerPositionRatioL[i].y.ToString("0.000000") + "," + fingerPositionRatioL[i].z.ToString("0.000000") + ")");

                // 手指线条
                if (show == true && i > 0)
                {
                    if (i == 1)
                    {
                        showFingerLinesL(true);
                    }
                    int j = (int)Math.Ceiling((double)i / 4) - 1;
                    int k = (i - 1) % 4;
                    if (i > 4) // 大拇指之外的手指
                    {
                        if(k == 0)
                        {
                            fingerLinesL[j].GetComponent<LineRenderer>().SetPosition(0, fingerLinesL[0].GetComponent<LineRenderer>().GetPosition(0));
                        }
                        k += 1;
                    }
                    fingerLinesL[j].GetComponent<LineRenderer>().SetPosition(k, pose.Position);
                }
            }
            count++;
        }

        // Right Hand
        count = 1;
        for (int i = 0; i < 21; i++)
        {
            if (count % 5 == 2)
            {
                i--;
                count++;
                continue;
            }
            if (HandJointUtils.TryGetJointPose((TrackedHandJoint)count, Handedness.Right, out pose))
            {

#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
                if (mediaCaptureHandler.isIntrinsicsReady)
                {
                    wVector3 fingerPositionR = wVector3.Transform(new wVector3(pose.Position.x, pose.Position.y, pose.Position.z), newWorldToCameraTransform);
                    Point fingerPixelPointR = mediaCaptureHandler.cameraIntrinsics.ProjectOntoFrame(fingerPositionR);

                    fingerPositionRatioR[i].x = (float)(fingerPixelPointR.X / mediaCaptureHandler.cameraIntrinsics.ImageWidth);
                    fingerPositionRatioR[i].y = (float)(fingerPixelPointR.Y / mediaCaptureHandler.cameraIntrinsics.ImageHeight);
                    fingerPositionRatioR[i].z = fingerPositionR.Z;
                }
#else
                fingerPositionRatioR[i].x = Camera.main.WorldToScreenPoint(pose.Position).x / width;
                fingerPositionRatioR[i].y = 1.0f - Camera.main.WorldToScreenPoint(pose.Position).y / height;
                fingerPositionRatioR[i].z = Camera.main.WorldToScreenPoint(pose.Position).z;
#endif
                /*fingerPositionRatioR[i] = new Vector3(
                    (float)Camera.main.WorldToScreenPoint(pose.Position).x / width,
                    1.0f - (float)Camera.main.WorldToScreenPoint(pose.Position).y / height,
                    Camera.main.WorldToScreenPoint(pose.Position).z);*/

                /*if (notShowRightWarning == 0)
                {
                    minR = Vector3.Min(minR, pose.Position);
                    maxR = Vector3.Max(maxR, pose.Position);
                }*/

                // 手指线条
                if (show == true && i > 0)
                {
                    if (i == 1)
                    {
                        showFingerLinesR(true);
                    }
                    int j = (int)Math.Ceiling((double)i / 4) - 1;
                    int k = (i - 1) % 4;
                    if (i > 4) // 大拇指之外的手指
                    {
                        if (k == 0)
                        {
                            fingerLinesR[j].GetComponent<LineRenderer>().SetPosition(0, fingerLinesR[0].GetComponent<LineRenderer>().GetPosition(0));
                        }
                        k += 1;
                    }
                    fingerLinesR[j].GetComponent<LineRenderer>().SetPosition(k, pose.Position);
                }
            }
            count++;
        }
    }

    public void setShow(bool flag)
    {
        show = flag;
    }

    public void showFingerLinesL(bool flag)
    {
        for (int i = 0; i < 5; i++)
        {
            MySetActive(fingerLinesL[i], flag);
        }
    }

    public void showFingerLinesR(bool flag)
    {
        for (int i = 0; i < 5; i++)
        {
            MySetActive(fingerLinesR[i], flag);
        }
    }

    void MySetActive(GameObject obj, bool to_state)
    {
        if (obj.activeSelf != to_state)
        {
            obj.SetActive(to_state);
        }
    }

    float GetValidRatio(float data)
    {
        if (data < 0)
        {
            return 0f;
        }
        else if(data > 1)
        {
            return 1f;
        }
        else
        {
            return data;
        }
    }


    public Vector3[] GetFingerPositionL()
    {
        return fingerPositionRatioL;
    }

    public Vector3[] GetFingerPositionR()
    {
        return fingerPositionRatioR;
    }

    //byte[] result = new byte[21 * 3 * 4];
    public byte[] GetFingerPositionRatioL()
    {
        byte[] GuestureByteL = FloatVector3ToByteBuffer(fingerPositionRatioL);

        for(int i = 0; i < 21; i++)
        {
            fingerPositionRatioL[i].x = -1;
            fingerPositionRatioL[i].y = -1;
            fingerPositionRatioL[i].z = -1;
        }

        return GuestureByteL;
    }

    public byte[] GetFingerPositionRatioR()
    {
        byte[] GuestureByteR = FloatVector3ToByteBuffer(fingerPositionRatioR);

        // -1是没有探测到手部位置
        for (int i = 0; i < 21; i++)
        {
            fingerPositionRatioR[i].x = -1;
            fingerPositionRatioR[i].y = -1;
            fingerPositionRatioR[i].z = -1;
        }

        return GuestureByteR;
    }

    static byte[] FloatVector3ToByteBuffer(Vector3[] datas)
    {
        byte[] result = new byte[21 * 3 * 4]; // 一个float占4个byte，一个Vector3占3个float
        for (int i = 0; i < 21; i++)
        {

            byte[] signalBytesX = BitConverter.GetBytes((float)datas[i].x);

            result[12 * i] = signalBytesX[0];
            result[12 * i + 1] = signalBytesX[1];
            result[12 * i + 2] = signalBytesX[2];
            result[12 * i + 3] = signalBytesX[3];

            byte[] signalBytesY = BitConverter.GetBytes((float)datas[i].y);
            result[12 * i + 4] = signalBytesY[0];
            result[12 * i + 5] = signalBytesY[1];
            result[12 * i + 6] = signalBytesY[2];
            result[12 * i + 7] = signalBytesY[3];

            byte[] signalBytesZ = BitConverter.GetBytes((float)datas[i].z);
            result[12 * i + 8] = signalBytesZ[0];
            result[12 * i + 9] = signalBytesZ[1];
            result[12 * i + 10] = signalBytesZ[2];
            result[12 * i + 11] = signalBytesZ[3];
        }

        return result;
    }

#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
    public bool GetWorldToCameraTransform(out byte[] matrixBytes)
    {
        matrixBytes = new byte[4 * 4 * 4];
        if (mediaCaptureHandler.isIntrinsicsReady)
        {
            float[] matrixArray = new float[]
            {
            newWorldToCameraTransform.M11, newWorldToCameraTransform.M12, newWorldToCameraTransform.M13, newWorldToCameraTransform.M14,
            newWorldToCameraTransform.M21, newWorldToCameraTransform.M22, newWorldToCameraTransform.M23, newWorldToCameraTransform.M24,
            newWorldToCameraTransform.M31, newWorldToCameraTransform.M32, newWorldToCameraTransform.M33, newWorldToCameraTransform.M34,
            newWorldToCameraTransform.M41, newWorldToCameraTransform.M42, newWorldToCameraTransform.M43, newWorldToCameraTransform.M44
            };

            byte[] elementBytes;

            for (int i = 0; i < 16; i++)
            {
                elementBytes = BitConverter.GetBytes(matrixArray[i]);
                matrixBytes[4 * i] = elementBytes[0];
                matrixBytes[4 * i + 1] = elementBytes[1];
                matrixBytes[4 * i + 2] = elementBytes[2];
                matrixBytes[4 * i + 3] = elementBytes[3];
            }
            return true;
        }
        else
        {
            return false;
        }

        //return matrixBytes;
    }

    /*private byte[] MatrixToBytes(wMatrix4x4 matrix)
    {
        float[] matrixArray = new float[]
        {
            matrix.M11, matrix.M12, matrix.M13, matrix.M14,
            matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            matrix.M41, matrix.M42, matrix.M43, matrix.M44
        };

        byte[] matrixBytes = new byte[4 * 4 * 4];
        byte[] elementBytes;

        for (int i = 0; i < 16; i++)
        {
            elementBytes = BitConverter.GetBytes(matrixArray[i]);
            matrixBytes[4 * i] = elementBytes[0];
            matrixBytes[4 * i + 1] = elementBytes[1];
            matrixBytes[4 * i + 2] = elementBytes[2];
            matrixBytes[4 * i + 3] = elementBytes[3];
        }

        return matrixBytes;
    }*/

    public bool GetIntrinsics(out byte[] intrinsicsBytes)
    {
        intrinsicsBytes = new byte[4 * 4];
        if (mediaCaptureHandler.isIntrinsicsReady)
        {
            (BitConverter.GetBytes(mediaCaptureHandler.cameraIntrinsics.FocalLength.X)).CopyTo(intrinsicsBytes, 0);
            (BitConverter.GetBytes(mediaCaptureHandler.cameraIntrinsics.FocalLength.Y)).CopyTo(intrinsicsBytes, 4);
            (BitConverter.GetBytes(mediaCaptureHandler.cameraIntrinsics.PrincipalPoint.X)).CopyTo(intrinsicsBytes, 8);
            (BitConverter.GetBytes(mediaCaptureHandler.cameraIntrinsics.PrincipalPoint.Y)).CopyTo(intrinsicsBytes, 12);

            return true;
        }
        else
        {
            return false;
        }
        //return bytes;
    }
#endif

    private static float ToFloat(byte[] data)
    {
        unsafe
        {
            float a = 0.0F;
            byte i;
            byte[] x = data;
            void* pf;
            fixed (byte* px = x)
            {
                pf = &a;
                for (i = 0; i < data.Length; i++)
                {
                    *((byte*)pf + i) = *(px + i);
                }
            }
            return a;
        }
    }
}