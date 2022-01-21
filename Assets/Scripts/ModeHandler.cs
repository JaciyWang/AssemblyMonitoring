using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;


public class ModeHandler : MonoBehaviour
{
    private int mode = 0;
    //private bool isModeChanged = false;
    public GameObject handState;
    public GameObject objState;

    public GameObject leftHand;
    public GameObject rightHand;

    /*public string myIp = "10.28.131.39";
    public int myPort = 10001;
    private bool isConnected = false;

    private byte[] recevieBytes = new byte[512];
    private string recMsg = null;
    private int recTimes = 0;
    private bool isReceived = false;*/
    // private byte[] sendBytes = new byte[512];

    void Start()
    {
        /*Client(myIp, myPort);
        if (isConnected)
        {
            StartCoroutine("ChangeState");
        }*/
        //OnModeChangeto0();
    }

    void Update()
    {
        
    }

    // string 解析出json数据
    StateData GetJson(string msg)
    {
        StateData info = JsonUtility.FromJson<StateData>(msg);
        return info;
    }

    StateData warningInfo;
    public void ChangeState(string warningInfoStr)
    {
        if (warningInfoStr != null && warningInfoStr.Length > 0)
        {
            warningInfo = GetJson(warningInfoStr);
            // print("Changing...");
            if (warningInfo.mode == 0) //线缆插孔
            {
               /*leftHand.GetComponent<ShowHandState>().ChangeState(warningInfo.infolist.left, warningInfo.infolist);
                rightHand.GetComponent<ShowHandState>().ChangeState(warningInfo.infolist.right, warningInfo.infolist);
                leftHand.GetComponent<ShowHandState>().ChangeAct(warningInfo.infolist.left_act);
                rightHand.GetComponent<ShowHandState>().ChangeAct(warningInfo.infolist.right_act);*/
            }
            else if (warningInfo.mode == 1 || warningInfo.mode == 2) //支撑件警告
            {
                //objState.GetComponent<ShowHandState>().ChangeObjState(warningInfo.mode);
            }
        }
    }

    public byte[] GetMode()
    {
        // string modeStr = mode.ToString();
        return Encoding.UTF8.GetBytes(mode.ToString());
    }

    
    public void OnModeChangeto0()
    {
        mode = 0;
        //isModeChanged = true;
        print(mode);
        handState.SetActive(true);
        objState.SetActive(false);
        // GameObject.FindGameObjectWithTag("LH").GetComponent<ShowHandState>().ChangeState(0, 0);
    }

    public void OnModeChangeto1()
    {
        mode = 1;
        //isModeChanged = true;
        print(mode);
        handState.SetActive(false);
        objState.SetActive(true);
    }
}
