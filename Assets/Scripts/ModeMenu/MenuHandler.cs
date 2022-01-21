using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;


[Serializable]
public class Info
{
    public int left;     //0错误，1正确
    public int right;
    public int left_act; //0按压，1扶持
    public int right_act;
    public int obj_id;
    public List<float> obj_pos;
}

[Serializable]
public class StateData
{
    public int mode; //0线缆插孔，1支撑件警告，2支撑件正常
    public int state; //当前进行流程：0，不显示：1 工件类别，2：手势，3：工件姿态，4：请安装下一工件，5：安装流程结束
    public int part; //0手部，1物体
    public Info infolist;
}


public class MenuHandler : MonoBehaviour
{
    public GameObject menu;
    public GameObject menuTitle;
    public GameObject userManual;

    public GameObject handState;
    public GameObject objState;
    public GameObject leftHand;
    public GameObject rightHand;

    public GameObject StateText;

    public GestureHandler gestureHandler;

    // 0线缆插孔，1支撑件
    int mode = 0;

    // Start is called before the first frame update
    void Start()
    {
        mode = 1;
        MySetActive(userManual, false);        
        ChangeMenuTitle();
    }

    // Update is called once per frame
    bool startTimer_state = false;
    float m_timer_state = 0;
    bool startTimer_warning = false;
    float m_timer_warning = 0;
    void Update()
    {
        if (startTimer_state)
        {
            m_timer_state += Time.deltaTime;
            if (m_timer_state >= 2)
            {
                m_timer_state = 0;
                startTimer_state = false;
                StateText.GetComponent<TextMeshPro>().text = "";
            }
        }
        if (startTimer_warning)
        {
            m_timer_warning += Time.deltaTime;
            if (m_timer_warning >= 2)
            {
                m_timer_warning = 0;
                startTimer_warning = false;

                MySetActive(handState, false);
                MySetActive(objState, false);
            }
        }
    }

    void StartTimer_state()
    {
        m_timer_state = 0;
        startTimer_state = true;
    }

    void StartTimer_warning()
    {
        m_timer_warning = 0;
        startTimer_warning = true;
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
            Debug.Log(mode + "  " + warningInfo.mode);            
            if (mode == warningInfo.mode)
            {
                StartTimer_state();
                ChangeStateText(warningInfo.state);

                // 支撑件模式
                if (mode == 1 && (0 <= warningInfo.part && warningInfo.part <= 4)) {
                    StartTimer_warning();
                    if (warningInfo.part == 0) //手部
                    {
                        if (warningInfo.infolist.left == 2 && warningInfo.infolist.right != 2)
                        {
                            MySetActive(handState, false);
                            MySetActive(objState, true);
                            objState.GetComponent<ShowHandState>().OnlyDetectOneHand(1, warningInfo.infolist.right, warningInfo.infolist.right_act, warningInfo.infolist.obj_id);
                        }
                        else if (warningInfo.infolist.left != 2 && warningInfo.infolist.right == 2)
                        {
                            MySetActive(handState, false);
                            MySetActive(objState, true);
                            objState.GetComponent<ShowHandState>().OnlyDetectOneHand(0, warningInfo.infolist.left, warningInfo.infolist.left_act, warningInfo.infolist.obj_id);
                        }
                        else if (warningInfo.infolist.left != 2 && warningInfo.infolist.right != 2)
                        {
                            MySetActive(handState, true);
                            MySetActive(objState, false);

                            /*gestureHandler.ShowWarning(0, warningInfo.infolist.left);
                            gestureHandler.ShowWarning(1, warningInfo.infolist.right);*/

                            leftHand.GetComponent<ShowHandState>().ChangeAct(0, warningInfo.infolist.left, warningInfo.infolist.left_act, warningInfo.infolist.obj_id);
                            rightHand.GetComponent<ShowHandState>().ChangeAct(1, warningInfo.infolist.right, warningInfo.infolist.right_act, warningInfo.infolist.obj_id);
                        }
                    }
                    else// 物体
                    {
                        MySetActive(handState, false);
                        MySetActive(objState, true);

                        /*gestureHandler.ShowWarning(0, 1);
                        gestureHandler.ShowWarning(1, 1);*/

                        objState.GetComponent<ShowHandState>().ChangeObjState(warningInfo.part, warningInfo.infolist);
                    }
                }
                // 线缆插孔模式
                else if(mode == 0 && (0 <= warningInfo.part && warningInfo.part <= 1))
                {
                    StartTimer_warning();
                    MySetActive(handState, false);
                    MySetActive(objState, true);
                    objState.GetComponent<ShowHandState>().ShowFinish(warningInfo.part);
                }
            }
        }
    }

    public void ChangeStateText(int state)
    {
        switch (state)
        {
            case 1:
                StateText.GetComponent<TextMeshPro>().text = "当前进行流程: 工件类别检测";
                break;
            case 2:
                StateText.GetComponent<TextMeshPro>().text = "当前进行流程: 手势检测";
                break;
            case 3:
                StateText.GetComponent<TextMeshPro>().text = "当前进行流程: 工件姿态检测";
                break;
            case 4:
                StateText.GetComponent<TextMeshPro>().text = "请安装下一工件";
                break;
            case 5:
                StateText.GetComponent<TextMeshPro>().text = "安装流程结束";
                break;
            default:
                // 少用setActive(true/false)，或许减少资源使用？
                // MySetActive(StateText, false);
                StateText.GetComponent<TextMeshPro>().text = "";
                break;
        }
        if (state == 2)
        {
            gestureHandler.setShow(true);
            gestureHandler.setShow(true);
        }
        else
        {
            gestureHandler.setShow(false);
        }
    }

    public void ChangeMode()
    {
        mode = mode == 0 ? 1 : 0;
        ChangeMenuTitle();
    }

    public byte[] GetMode()
    {
        // string modeStr = mode.ToString();
        return Encoding.UTF8.GetBytes(mode.ToString());
    }

    void ChangeMenuTitle()
    {
        if (mode == 0)
        {
            menuTitle.GetComponent<TextMeshPro>().text = "当前场景模式 : 线缆插孔模式";
            /*handState.SetActive(true);
            objState.SetActive(false);*/
        }
        else
        {
            menuTitle.GetComponent<TextMeshPro>().text = "当前场景模式 : 支撑件安装模式";
            /*handState.SetActive(false);
            objState.SetActive(true);*/
        }
        MySetActive(objState, false);
        MySetActive(handState, false);
    }

    public void EndApplication()
    {
        SceneManager.LoadScene(0);
    }

    public void ShowUserManual()
    {
        // main camera
        /*userManual.SetActive(true);
        userManual.transform.position = new Vector3(
            Camera.main.transform.position.x, Camera.main.transform.position.y, (float)Camera.main.transform.position.z + 0.4f);
        print(Camera.main.transform.position);
        print("manual : " + userManual.transform.position);*/

        if (userManual.activeSelf)
        {
            userManual.SetActive(false);
        }
        else
        {
            userManual.SetActive(true);
            userManual.transform.position = new Vector3(
                menu.transform.position.x, (float)(menu.transform.position.y + 0.2), (float)menu.transform.position.z);
        }        
    }

    public void MySetActive(GameObject obj, bool to_state)
    {
        if(obj.activeSelf != to_state)
        {
            obj.SetActive(to_state);
        }
    }
}
