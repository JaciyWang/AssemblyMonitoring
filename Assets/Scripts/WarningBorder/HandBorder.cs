using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.Input;
using System.Text;
using System;
using UnityEngine.UI;

public class HandBorder : MonoBehaviour
{
    public RawImage alertBox;

    public GameObject fingerObject;
    public GameObject wristObject;

    public float dist = 0f;


    List<GameObject> fingerObjectsL = new List<GameObject>();
    List<GameObject> fingerObjectsR = new List<GameObject>();

    Vector3[] fingerPositionRatioL = new Vector3[21];
    Vector3[] fingerPositionRatioR = new Vector3[21];

    MixedRealityPose pose;

    int times = 0;

    float width = Screen.width;
    float height = Screen.height;

    void Start()
    {
        for (int i = 0; i < 21; i++)
        {
            GameObject obj1 = Instantiate(fingerObject, this.transform);
            fingerObjectsL.Add(obj1);
            GameObject obj2 = Instantiate(fingerObject, this.transform);
            fingerObjectsR.Add(obj2);

        }
        /*wristObjectL = Instantiate(wristObject, this.transform);
        wristObjectR = Instantiate(wristObject, this.transform);*/
    }

    float minxL = 1.0f;
    float minyL = 1.0f;
    float maxxL = 0.0f;
    float maxyL = 0.0f;
    float minzL = 1.0f;
    float alertBoxWidth;
    float alertBoxHeight;


    // ~Metacarpal 接近手腕的关节，不考虑该点，就有21个点了，否则26个
    void Update()
    {
        times++;
        width = (float)Screen.width;
        height = (float)Screen.height;
        //print(width + "*" + height);

        //only render if hand is tracked
        for (int i = 0; i < 21; i++)
        {
            fingerObjectsL[i].GetComponent<Renderer>().enabled = false;
            fingerObjectsR[i].GetComponent<Renderer>().enabled = false;
        }
        /*wristObjectL.GetComponent<Renderer>().enabled = false;
        wristObjectR.GetComponent<Renderer>().enabled = false;*/


        minxL = 1000.0f;
        minyL = 1000.0f;
        maxxL = -1000.0f;
        maxyL = -1000.0f;
        minzL = 1000.0f;

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
                /*tipsL[i] = pose.Position;
                fingerObjectsL[i].GetComponent<Renderer>().enabled = true;*/

                print(i + ":" + Camera.main.WorldToScreenPoint(pose.Position));
                print(i + ":" + pose.Position);

                if (pose.Position.x < minxL)
                {
                    minxL = pose.Position.x;
                }
                if (pose.Position.y < minyL)
                {
                    minyL = pose.Position.y;
                }

                if (pose.Position.x > maxxL)
                {
                    maxxL = pose.Position.x;
                }
                if (pose.Position.y > maxyL)
                {
                    maxyL = pose.Position.y;
                }

                if (pose.Position.z < minzL)
                {
                    minzL = pose.Position.z;
                }


                fingerPositionRatioL[i] = new Vector3(
                    Camera.main.WorldToScreenPoint(pose.Position).x / width,
                    1.0f - Camera.main.WorldToScreenPoint(pose.Position).y / height,
                    Camera.main.WorldToScreenPoint(pose.Position).z);
                print(i + ": (" + fingerPositionRatioL[i].x.ToString("0.000000") + "," + fingerPositionRatioL[i].y.ToString("0.000000") + "," + fingerPositionRatioL[i].z.ToString("0.000000") + ")");

            }
            count++;
        }
       

        for (int i = 0; i < 21; i++)
        {
            fingerObjectsL[i].GetComponent<Renderer>().enabled = true;
            fingerObjectsL[i].transform.position = Camera.main.ScreenToWorldPoint(new Vector3(fingerPositionRatioL[i].x * width, fingerPositionRatioL[i].y * height, fingerPositionRatioL[i].z));
        }

        print(minxL + " - " + maxxL + " ," + minyL + " - " + maxyL);

        alertBoxWidth = (maxxL - minxL) * width;
        alertBoxHeight = (maxyL - minyL) * height;
        // alertBox.transform.position = Camera.main.ScreenToWorldPoint(new Vector3((maxxL + minxL) / 2.0f * width, (maxyL + minyL) / 2 * height, 5.0f));
        // alertBox.transform.GetComponent<RectTransform>().sizeDelta = new Vector2((maxxL - minxL) * width, (maxyL - minyL) * height);

        alertBox.transform.position = new Vector3((maxxL + minxL) / 2.0f, (maxyL + minyL) / 2.0f, minzL);
        alertBox.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(maxxL - minxL, maxyL - minyL);

        print(alertBox.transform.position + " : " + alertBox.transform.GetComponent<RectTransform>().sizeDelta);

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
                /*tipsR[i] = pose.Position;
                fingerObjectsR[i].GetComponent<Renderer>().enabled = true;*/
                fingerPositionRatioR[i] = new Vector3(
                    (float)Camera.main.WorldToScreenPoint(pose.Position).x / width,
                    1.0f - (float)Camera.main.WorldToScreenPoint(pose.Position).y / height,
                    Camera.main.WorldToScreenPoint(pose.Position).z);
            }
            count++;
        }

        for (int i = 0; i < 21; i++)
        {
            fingerObjectsR[i].GetComponent<Renderer>().enabled = true;
            fingerObjectsR[i].transform.position = Camera.main.ScreenToWorldPoint(new Vector3(fingerPositionRatioR[i].x * width, fingerPositionRatioR[i].y * height, fingerPositionRatioR[i].z));
        }
    }


    float GetValidRatio(float data)
    {
        if (data < 0)
        {
            return 0f;
        }
        else if (data > 1)
        {
            return 1f;
        }
        else
        {
            return data;
        }
    }

    public byte[] GetFingerPositionRatioL()
    {
        byte[] GuestureByteL = FloatVector3ToByteBuffer(fingerPositionRatioL);
        return GuestureByteL;
    }

    public byte[] GetFingerPositionRatioR()
    {
        byte[] GuestureByteR = FloatVector3ToByteBuffer(fingerPositionRatioR);
        return GuestureByteR;
    }

    static byte[] FloatVector3ToByteBuffer(Vector3[] datas)
    {
        byte[] result = new byte[21 * 3 * 4]; // 一个float占4个byte，一个Vector2占2个float
        for (int i = 0; i < 21; i++)
        {
            byte[] signalBytesX = BitConverter.GetBytes((float)datas[i].x);
            /*print(signalBytesX.Length);
            print(datas[i].x);
            print((float)datas[i].x);*/

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