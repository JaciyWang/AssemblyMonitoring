using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConnectParameter : MonoBehaviour
{
    string _ip;
    string _port;

    public void SetValue(string ip, string port)
    {
        _ip = ip;
        _port = port;
    }

    /*public (string, string) GetValue()
    {
        return (_ip, _port);
    }*/
}
