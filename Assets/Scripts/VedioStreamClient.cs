using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UI;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System;
using System.Threading;
using System.Text;

public class VedioStreamClient : MonoBehaviour
{
    WebCamTexture webCam;
    // ublic RawImage myImage;
    // public bool enableLog = false;

    Texture2D currentTexture;

    private bool stop = false;

    //private List<TcpClient> clients = new List<TcpClient>();

    //This must be the-same with SEND_COUNT on the client
    const int SEND_RECEIVE_COUNT = 4;

    public string myIp = "10.28.131.39";
    public int myPort = 10002;
    /*private void Start()
    {
        Application.runInBackground = true;

        //Start WebCam coroutine
        StartCoroutine(initAndWaitForWebCamTexture());
    }*/

    void Start()
    {
        Client(myIp, myPort);

        StartCoroutine(initAndWaitForWebCamTexture());
    }


    //TcpClient client;
    private Socket socketSend;
    private bool isConnected = false;
    private void Client(string ip, int port)
    {
        //client = new TcpClient();
        socketSend = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            socketSend.Connect(IPAddress.Parse(ip), port);//同步方法，连接成功、抛出异常、服务器不存在等之前程序会被阻塞  
            isConnected = true;
            //print("LocalEndPoint = " + client.Client.LocalEndPoint + ". RemoteEndPoint = " + client.Client.RemoteEndPoint);

            IPAddress _ip = IPAddress.Parse(ip);
            IPEndPoint _port = new IPEndPoint(_ip, port);
            print("连接成功：" + ip + ":" + port + "=>" + _ip + ":" + _port);


            // 接收进程
            /*Thread r_thread = new Thread(Received);
            r_thread.IsBackground = true;
            r_thread.Start();*/

            // 发送进程
            /*Thread s_thread = new Thread(SendMessage);
            s_thread.IsBackground = true;
            s_thread.Start();*/
        }
        catch (Exception ex)
        {
            print("客户端连接异常：" + ex.Message);
            isConnected = false;
        }
    }


    //Converts the data size to byte array and put result to the fullBytes array
    void byteLengthToFrameByteArray(int byteLength, byte[] fullBytes)
    {
        //Clear old data
        Array.Clear(fullBytes, 0, fullBytes.Length);
        //Convert int to bytes
        byte[] bytesToSendCount = BitConverter.GetBytes(byteLength);
        //Copy result to fullBytes
        bytesToSendCount.CopyTo(fullBytes, 0);
    }

    //Converts the byte array to the data size and returns the result
    int frameByteArrayToByteLength(byte[] frameBytesLength)
    {
        int byteLength = BitConverter.ToInt32(frameBytesLength, 0);
        return byteLength;
    }

    IEnumerator initAndWaitForWebCamTexture()
    {
        // Open the Camera on the desired device, in my case IPAD pro
        webCam = new WebCamTexture();
        // Get all devices , front and back camera
        //webCam.deviceName = WebCamTexture.devices[WebCamTexture.devices.Length - 1].name;
        webCam.deviceName = WebCamTexture.devices[0].name;
        print(webCam.deviceName);

        WebCamDevice[] devices = WebCamTexture.devices;
        for (int i = 0; i < devices.Length; i++)
            print(devices[i].name);

        // request the lowest width and heigh possible
        webCam.requestedHeight = 10;
        webCam.requestedWidth = 10;

        // myImage.texture = webCam;

        webCam.Play();
        print(webCam.width + "," + webCam.height);
        currentTexture = new Texture2D(webCam.width, webCam.height);

        // Connect to the server
        // listner = new TcpListener(IPAddress.Any, port);
        // listner.Start();

        while (webCam.width < 100)
        {
            yield return null;
        }

        //Start sending coroutine
        StartCoroutine(senderCOR());
    }

    WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
    IEnumerator senderCOR()
    {
        if (isConnected)
        {
            bool readyToGetFrame = true;

            byte[] frameBytesLength = new byte[SEND_RECEIVE_COUNT];

            while (!stop)
            {
                //Wait for End of frame
                yield return endOfFrame;

                currentTexture.SetPixels(webCam.GetPixels());
                byte[] pngBytes = currentTexture.EncodeToJPG();// EncodeToPNG();
                //Fill total byte length to send. Result is stored in frameBytesLength
                byteLengthToFrameByteArray(pngBytes.Length, frameBytesLength);
                print("pngBytes.Length:" + pngBytes.Length);
                //Set readyToGetFrame false
                readyToGetFrame = false;

                //Send total byte count first
                //stream.Write(frameBytesLength, 0, frameBytesLength.Length);
                socketSend.Send(frameBytesLength);
                print("Sent Image byte Length: " + frameBytesLength.Length);

                //Send the image bytes
                //stream.Write(pngBytes, 0, pngBytes.Length);
                socketSend.Send(pngBytes);
                print("Sending Image byte array data : " + pngBytes.Length);

                //Sent. Set readyToGetFrame true
                readyToGetFrame = true;

                //Wait until we are ready to get new frame(Until we are done sending data)
                while (!readyToGetFrame)
                {
                    print("Waiting To get new frame");
                    yield return null;
                }
            }
        }        
    }

    private void Update()
    {
        //myImage.texture = webCam;
    }

    // stop everything
    private void OnDisable()
    {
        print("Begin OnDisable()");
        if (webCam != null && webCam.isPlaying)
        {
            webCam.Stop();
            stop = true;
        }
        if (socketSend.Connected)
        {
            try
            {
                socketSend.Shutdown(SocketShutdown.Both);
                socketSend.Close();
                isConnected = false;
            }
            catch (Exception e)
            {
                print(e.Message);
            }
        }
        print("End OnDisable()");
    }
}
