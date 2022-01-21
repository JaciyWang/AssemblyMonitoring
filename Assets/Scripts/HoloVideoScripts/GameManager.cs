using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Text;
using TMPro;

public class GameManager : MonoBehaviour
{
	// public DisplayInfo displayExtraInfo;
	public TCPNetworking tcpNetworkingScript;
	public VideoPanel videoPanelScript;
	//public ModeHandler modeHandler;
	public GestureHandler gestureHandler;
	public MenuHandler menuHandler;


	ConnectParameter connectParameter;

	public GameObject debugText;

	// For checking if camera has started
	[Space(10)]
	private bool camReady = false;

	// Use this for initialization
	void Start()
	{
		//ip端口输入场景跳转用
		//connectParameter = GameObject.FindObjectOfType<ConnectParameter>();
		StartCoroutine("CleanStorage");
	}

	IEnumerator CleanStorage()
    {
        while (true)
        {
			Resources.UnloadUnusedAssets();
			GC.Collect();
			yield return new WaitForSeconds(1f);
		}
	}

	float m_timeCounter = 0.0f;
	float m_lastFramerate = 0.0f;
	public float m_refreshTime = 1f;
	int countSent = 0;

	bool isConnected = false;
	void Update()
	{
		if (!camReady)
		{
			if (videoPanelScript.GetCameraStatus())
			{
				Debug.Log("Camera is Working");
				camReady = true;
			}
			else
			{
				Debug.Log("Camera not started");
				camReady = false;
			}
		}
		if (tcpNetworkingScript.GetNetworkIsNotActive())
		{
			//Debug.Log(videoPanelScript.startSending);
			if (videoPanelScript.startSending)
			{
				// IP and Port 专用
                /*(string ip, string port) = connectParameter.GetValue();
				print(ip + ":" + port);
				tcpNetworkingScript.receiverIPAddress = ip;
				tcpNetworkingScript.receiverPort = port;*/

				tcpNetworkingScript.SetNetworkToActive();
				tcpNetworkingScript.InitializeCommunicationOverTCP();
				isSended = true;
			}
		}
		isConnected = tcpNetworkingScript.isConnected;

		// print(Screen.width + "*" + Screen.height);
		ProcessWarningInfo();

		// 计算帧率
		if (m_timeCounter < m_refreshTime)
		{
			m_timeCounter += Time.deltaTime;
		}
		else
		{
			//This code will break if you set your m_refreshTime to 0, which makes no sense.
			m_lastFramerate = (float)countSent / m_timeCounter;
			countSent = 0;
			m_timeCounter = 0.0f;
		}

		debugText.GetComponent<TextMeshPro>().text = "SendFPS : " + Mathf.Ceil(m_lastFramerate).ToString();

	}

	/////////////////////////////////////////// SEND START //////////////////////////////////////////////////////
	bool isSended = false;
	public void PrepareToSend(byte[] imgByte)
	{
		//debugText.GetComponent<TextMesh>().text = $" GameManager : {isSended}";
		/*print("WriteAgain : " + writeAgain);
        if (firstSend || writeAgain == "1") { */

		// 发完一个再发下一个
		if (isSended && isConnected)
		{
			isSended = false;
            
            // 一帧里发送[mode + img]mode只占一个字节 fps=20左右
            //byte[] modeByte = modeHandler.GetMode();
			byte[] modeByte = menuHandler.GetMode();
            if (modeByte.Length < 1)
            {
                return;
            }

			byte[] guestureRatioL = gestureHandler.GetFingerPositionRatioL();
			byte[] guestureRatioR = gestureHandler.GetFingerPositionRatioR();
			if (guestureRatioL.Length < 21 * 3 * 4 || guestureRatioR.Length < 21 * 3 * 4)
			{
				return;
			}

#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
			byte[] intrinsics;
			byte[] worldToCamera;
			if ( !gestureHandler.GetIntrinsics(out intrinsics) || !gestureHandler.GetWorldToCameraTransform(out worldToCamera))
            {
				return;
            }	

			byte[] newByte = new byte[modeByte.Length + guestureRatioL.Length + guestureRatioR.Length 
				+ intrinsics.Length + worldToCamera.Length + imgByte.Length];

			modeByte.CopyTo(newByte, 0);
			guestureRatioL.CopyTo(newByte, modeByte.Length);
			guestureRatioR.CopyTo(newByte, modeByte.Length + guestureRatioL.Length);
			intrinsics.CopyTo(newByte, modeByte.Length + guestureRatioL.Length + guestureRatioR.Length);
			worldToCamera.CopyTo(newByte, modeByte.Length + guestureRatioL.Length + guestureRatioR.Length + intrinsics.Length);
			imgByte.CopyTo(newByte, modeByte.Length + guestureRatioL.Length + guestureRatioR.Length + intrinsics.Length + worldToCamera.Length);
#else

			byte[] newByte = new byte[modeByte.Length + guestureRatioL.Length + guestureRatioR.Length + imgByte.Length];
            modeByte.CopyTo(newByte, 0);
			guestureRatioL.CopyTo(newByte, modeByte.Length);
			guestureRatioR.CopyTo(newByte, modeByte.Length + guestureRatioL.Length);
			imgByte.CopyTo(newByte, modeByte.Length + guestureRatioL.Length + guestureRatioR.Length);

#endif

			tcpNetworkingScript.UpdateDataToSend(newByte);
			countSent++;

            // 一帧里只发送图像 fps=20左右
            //tcpNetworkingScript.UpdateDataToSend(imgByte);

            isSended = true;
            //firstSend = false;
		}
    }

	// TCP收到的数据存于队列，在队列里拿数据再处理（这里未用到该函数）
	void ProcessReceivedTextData()
	{
		byte[] dataToBeProcessed;
		if (!(tcpNetworkingScript.queueOfReceivedDataPackets.Count > 0))
		{
			return;
		}

		dataToBeProcessed = tcpNetworkingScript.queueOfReceivedDataPackets.Dequeue();
		string dataText = System.Text.Encoding.UTF8.GetString(dataToBeProcessed);
		Debug.Log(dataText);
	}

	// 处理TCP最新收到的数据

	//byte[] writeAgainByte = new byte[1];
	//string writeAgain = "1";

	void ProcessWarningInfo()
    {
		byte[] dataToBeProcessed = tcpNetworkingScript.latestByte;

		if (dataToBeProcessed.Length == 0)
			return;

		/*Array.Clear(writeAgainByte, 0, writeAgainByte.Length);
		byte[] warningByte = new byte[dataToBeProcessed.Length - 1];
		Array.Copy(dataToBeProcessed, 0, writeAgainByte, 0, 1);
		Array.Copy(dataToBeProcessed, 1, warningByte, 0, dataToBeProcessed.Length - 1);

		writeAgain = Encoding.UTF8.GetString(writeAgainByte);
		var recvInfo = Encoding.UTF8.GetString(warningByte);*/
		//print("WriteAgain : " + Encoding.UTF8.GetString(writeAgainByte));

		var recvInfo = Encoding.UTF8.GetString(dataToBeProcessed);
		//modeHandler.ChangeState(recvInfo.Trim('\''));
		menuHandler.ChangeState(recvInfo.Trim('\''));
	}
}
