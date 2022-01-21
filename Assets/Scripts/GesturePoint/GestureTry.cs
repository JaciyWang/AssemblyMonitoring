using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.Input;
using System.Text;
using System;

public class GestureTry : MonoBehaviour
{

    public GameObject handCube;
    public float dist = 0f;


    MixedRealityPose pose;


    float width = Screen.width;
    float height = Screen.height;

    void Start()
    {
        
    }

    // ~Metacarpal 接近手腕的关节，不考虑该点，就有21个点了，否则26个
    void Update()
    {
        
        if (HandJointUtils.TryGetJointPose((TrackedHandJoint)2, Handedness.Left, out pose))
        {
            /*tipsL[i] = pose.Position;
            fingerObjectsL[i].GetComponent<Renderer>().enabled = true;*/

            handCube.transform.position = pose.Position;
        }

    }


}