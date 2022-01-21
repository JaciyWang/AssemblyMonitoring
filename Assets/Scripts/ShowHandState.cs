using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

using UnityEngine.UI;

public class ShowHandState : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        /*Transform handAct = transform.GetChild(1);
        handAct.GetComponent<TextMeshPro>().text = "按压";

        Transform img = transform.GetChild(2).GetChild(0);
        img.GetComponent<Image>().sprite = Resources.Load<Sprite>("正确");*/
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // 更改工件展示板
    string[] state = { "正确", "警告" };
    public void ChangeObjState(int type, Info info) // state 新：0正常，1警告  // 旧 ：1警告，2正常
    {
        Transform objPart = transform.GetChild(0);
        Transform objState = transform.GetChild(1);
        Transform img = transform.GetChild(2).GetChild(0);

        switch (type)
        {
            case 1:
                {
                    objPart.GetComponent<TextMeshPro>().text = "工件类别";
                    if(info.left == 0)
                    {
                        objState.GetComponent<TextMeshPro>().text = state[0];
                    }
                    else
                    {
                        objState.GetComponent<TextMeshPro>().text = state[1];
                    }                    
                    img.GetComponent<Image>().sprite = Resources.Load<Sprite>($"Objects/{info.left}");
                    //print(info.obj_id);
                    break;
                }
            case 2:
                {
                    objPart.GetComponent<TextMeshPro>().text = "工件位置";
                    objState.GetComponent<TextMeshPro>().text = state[info.left];
                    img.GetComponent<Image>().sprite = Resources.Load<Sprite>($"ObjSign/{info.left}");
                    //print(info.obj_pos[0]);
                    break;
                }
            case 3:
                {
                    objPart.GetComponent<TextMeshPro>().text = "工件姿态";

                    if(info.left == 0)
                    {
                        objState.GetComponent<TextMeshPro>().text = state[0];
                        img.GetComponent<Image>().sprite = Resources.Load<Sprite>($"ObjSign/0");
                    }
                    else
                    {
                        objState.GetComponent<TextMeshPro>().text = state[1];
                        img.GetComponent<Image>().sprite = Resources.Load<Sprite>($"ObjSign/{info.obj_id}_{info.left}");
                    }                    
                    break;
                }
            case 4:
                {
                    objPart.GetComponent<TextMeshPro>().text = "待安装工件";
                    objState.GetComponent<TextMeshPro>().text = "类别";
                    img.GetComponent<Image>().sprite = Resources.Load<Sprite>($"Objects/{info.left}");
                    break;
                }
            default:
                break;
        }
    }

    // 更改手部展示板，换text以及image： state = 0错误，=1正确，obj_id = 1-7, 工件编号
    string[] actList = { "拿捏", "托举", "握住" };
    public void ChangeAct(int side, int is_wrong, int act, int obj_id)
    {
        Transform handAct = transform.GetChild(1);
        Transform img = transform.GetChild(2).GetChild(0);

        // 修改Text
        handAct.GetComponent<TextMeshPro>().text = actList[act];

        //修改img
        // id=0 表示正常, is_wrong=0 表示正常
        if (is_wrong == 0 || obj_id == 0)
        {
            img.GetComponent<Image>().sprite = Resources.Load<Sprite>("HandSign/0");
        }
        else if (1 <= obj_id && obj_id <= 7)
        {
            img.GetComponent<Image>().sprite = Resources.Load<Sprite>($"HandAct/{side}_{obj_id}_{act}");
        }
    }

    // 只探测到一方手部的情况
    string[] hands = { "左手", "右手" };
    public void OnlyDetectOneHand(int side, int wrong, int act, int obj_id)
    {
        Transform mainTitle = transform.GetChild(0);
        Transform handAct = transform.GetChild(1);
        Transform img = transform.GetChild(2).GetChild(0);

        mainTitle.GetComponent<TextMeshPro>().text = "正常操作";
        handAct.GetComponent<TextMeshPro>().text = $"{hands[side]}{actList[act]}";

        if (wrong == 0)
        {
            img.GetComponent<Image>().sprite = Resources.Load<Sprite>("HandSign/0");
        }
        else if (1 <= obj_id && obj_id <= 7)
        {
            img.GetComponent<Image>().sprite = Resources.Load<Sprite>($"HandAct/{side}_{obj_id}_{act}");
        }
    }

    //线缆插孔模式
    public void ShowFinish(int state)
    {
        Transform mainTitle = transform.GetChild(0);
        Transform act = transform.GetChild(1);
        Transform img = transform.GetChild(2).GetChild(0);

        act.GetComponent<TextMeshPro>().text = "完成";
        if (state == 0)
        {
            mainTitle.GetComponent<TextMeshPro>().text = "工具检测";
            img.GetComponent<Image>().sprite = Resources.Load<Sprite>("Finish/0");
        }
        else if (state == 1)
        {
            mainTitle.GetComponent<TextMeshPro>().text = "线缆安装";
            img.GetComponent<Image>().sprite = Resources.Load<Sprite>("Finish/1");
        }
    }
}
