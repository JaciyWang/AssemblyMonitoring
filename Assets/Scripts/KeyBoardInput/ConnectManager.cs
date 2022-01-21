using Microsoft.MixedReality.Toolkit.Experimental.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ConnectManager : MonoBehaviour
{
    //public InputExample ipInput;
    //public InputObject portInput;

    public GameObject ipInput;
    public GameObject portInput;

    public GameObject warningInfo;
    public GameObject inputHandler;
    public ConnectParameter connectParameter;

    string ip;
    string port;


    // Start is called before the first frame update
    void Start()
    {
        DontDestroyOnLoad(connectParameter);
        warningInfo.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void GetIPandPort()
    {
        ip = ipInput.GetComponent<TMP_InputField>().text;
        port = portInput.GetComponent<TMP_InputField>().text;
        i++;
        if(IsValid(ip, port))
        {
            print(i + " True");
            connectParameter.SetValue(ip, port);
            //(string ii, string pp) = connectParameter.GetValue();
            //print(ii + " : " + pp);
            SceneManager.LoadScene(1);
        }
        else
        {
            print(i + " False");
            warningInfo.SetActive(true);
            inputHandler.SetActive(false);
        }
    }

    public void OnClickConfirm()
    {
        warningInfo.SetActive(false);
        inputHandler.SetActive(true);
    }

    string ipPattern = @"^((2(5[0-5]|[0-4]\d))|[0-1]?\d{1,2})(\.((2(5[0-5]|[0-4]\d))|[0-1]?\d{1,2})){3}$"; //[0~255].[0~255].[0~255].[0~255]
    string portPattern = @"^([1-9][0-9]{0,3}|[1-5][0-9]{4}|6[0-4][0-9]{3}|65[0-4][0-9]{2}|655[0-2][0-9]{1}|6553[0-5])$"; //1~65533
    int i = 0;
    bool IsValid(string ip, string port)
    {
        if (Regex.IsMatch(ip, ipPattern) && Regex.IsMatch(port, portPattern))
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
