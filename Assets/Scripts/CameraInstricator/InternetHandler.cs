using UnityEngine;
using UnityEngine.UI;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using System.Threading.Tasks;

#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;
#else
using System.Net;
using System.Threading;
using System.Net.Sockets;
#endif


public class InternetHandler : MonoBehaviour
{
	// Soure1: https://stackoverflow.com/questions/42717713/unity-live-video-streaming
	void Awake() // 比Start还要前面，为了赋值stopCommunication
	{
		//Application.runInBackground = true;
		//stopCommunication = true;

#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
		receiverSocket = new StreamSocket();
#else
		receiverClient = new TcpClient();
#endif
	}

    private void Start()
    {
		stopCommunication = false;
		InitializeCommunicationOverTCP();
	}

    float timeCounter = 0.0f;
	public int currentNumberOfPacketsReceived = 0;
	int previousNumberOfPacketsReceived = -1;
	float refreshTime = 5.0f;
	void Update()
	{
		//Debug.Log("Number of packets recieved: " + currentNumberOfPacketsReceived + " " + previousNumberOfPacketsReceived);

		if (timeCounter < refreshTime)
		{
			timeCounter += Time.deltaTime;
		}
		else
		{
			if (isConnected)
			{
				previousNumberOfPacketsReceived = currentNumberOfPacketsReceived;

				//UpdateDataToSend(Encoding.UTF8.GetBytes("qwe"));
			}
			currentNumberOfPacketsReceived = 0;
			timeCounter = 0;
		}
	}

	// Converts integer to byte array。 发送帧的bytes大小->字节组
	void StoreIntegerValueToByteArray(int byteLength, byte[] fullBytes)
	{
		// Clear old data
		Array.Clear(fullBytes, 0, fullBytes.Length);

		// Convert int to bytes
		byte[] bytesToSendCount = BitConverter.GetBytes((uint)byteLength);

		// Copy result to fullBytes
		bytesToSendCount.CopyTo(fullBytes, 0);
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// Start connection over TCP
	private IEnumerator startConnection = null;
	int instanceNumber = 0;
	public void InitializeCommunicationOverTCP()
	{
		instanceNumber++;
		//Debug.Log("Attempting to connect");

		if (startConnection != null)
			StopCoroutine(startConnection);
		print("Start connecting");
		startConnection = StartConnection();
		StartCoroutine(startConnection);
	}

	public bool GetNetworkIsNotActive()
	{
		return stopCommunication;
	}

	public void SetNetworkToActive()
	{
		stopCommunication = false;
	}

	// For setting up sending request
	public string receiverIPAddress = "10.28.158.81";
	public int receiverPort = 10003;

	// This must be the-same with SEND_COUNT on the server
	const int SEND_RECEIVE_COUNT = 4;

	private bool stopCommunication;
	private BinaryWriter binWriter = null;
	private BinaryReader binReader = null;

#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
	StreamSocket receiverSocket;
#else
	TcpClient receiverClient;
	NetworkStream receiverClientStream = null;
#endif

	bool isConnected = false;
	IEnumerator StartConnection()
	{
		// Debug.Log("Really trying to connect");
#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
		Task.Run(async () =>
#else
		Task.Run(() =>
#endif
		{
			while (!stopCommunication)
			{
				// Debug.Log("Connecting to receiver ...");

#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
				HostName receiverHostName = new Windows.Networking.HostName(receiverIPAddress);
				await receiverSocket.ConnectAsync(receiverHostName, receiverPort.ToString());
				Stream streamIn = receiverSocket.InputStream.AsStreamForRead();
				Stream streamOut = receiverSocket.OutputStream.AsStreamForWrite();
				binReader = new BinaryReader(streamIn);
				binWriter = new BinaryWriter(streamOut);
#else
				receiverClient.Connect(IPAddress.Parse(receiverIPAddress), receiverPort);
				receiverClientStream = receiverClient.GetStream();
				binReader = new BinaryReader(receiverClientStream);
				binWriter = new BinaryWriter(receiverClientStream);
#endif
				isConnected = true;
			}
		});

		while (!isConnected)
		{
			Debug.Log("Not connect " + instanceNumber);
			yield return null;
		}

		DebugLog("Connected with receiver");
		// Start receiving data as well
		ReceiveData();
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// Send Data over TCP connection
	byte[] dataPacketSize = new byte[SEND_RECEIVE_COUNT];
	byte[] dataPacketBytes;

	int totalPacketsSent = 0;
	public GameObject debugText;


	private void DebugLog(string s)
    {
		debugText.transform.GetComponent<TextMeshPro>().text = s;
	}

	bool isSending = false;
	public void UpdateDataToSend(byte[] newData)
	{
        if (isSending)
        {
			return;
        }
		isSending = true;
		dataPacketBytes = newData;
		// Fill total byte length to send. Result is stored in dataPacketSize
		StoreIntegerValueToByteArray(dataPacketBytes.Length, dataPacketSize);

		// Send total byte count first
		binWriter.Write(dataPacketSize, 0, dataPacketSize.Length);

		// Send the image bytes
		binWriter.Write(dataPacketBytes, 0, dataPacketBytes.Length);
		binWriter.Flush();
		DebugLog("TotalSent : " + totalPacketsSent + " Sending Image byte array data : " + dataPacketBytes.Length);

		totalPacketsSent++;
		isSending = false;
	}

	// To stop sending/receiving on purpose
	private void KillConnection()
	{
#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
		if (receiverSocket != null)
		{
			receiverSocket.Dispose();
		}

		if (binWriter != null)
		{
			binWriter.Dispose();
		}

		if (binReader != null)
		{
			binReader.Dispose();
		}

		receiverSocket = null;
#else
		if (receiverClient != null)
			receiverClient.Close();

		if (binWriter != null)
		{
			binWriter.Close();
		}

		if (binReader != null)
			binReader.Close();

		if (receiverClientStream != null)
			receiverClientStream.Close();

		receiverClient = null;
		receiverClientStream = null;
#endif

		binWriter = null;
		binReader = null;
		isConnected = false;
		stopCommunication = true;
		previousNumberOfPacketsReceived = -1;
		Debug.Log("Connection killed");
	}

	// Stop everything on GameObject destroy
	void OnDestroy()
	{
		KillConnection();
		Debug.Log("OnDestroy");
	}

	// Stop everything on Application quit
	private void OnApplicationQuit()
	{
		// 有或没有都可以，主要看python端reciver的写法，以防万一最好有
		UpdateDataToSend(Encoding.UTF8.GetBytes("exit"));

		KillConnection();
		DebugLog("OnApplicationQuit");
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// Receive data over TCP connection
	void ReceiveData()
	{
		//Debug.Log("All set to receive data");
		// While loop in another Thread is fine so we don't block main Unity Thread

#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
		Task.Run(() =>
#else
		Task.Run(() =>
#endif
		{
			Debug.Log("Enabled to receive: " + (!stopCommunication).ToString());
			while (!stopCommunication)
			{
				// Read Data Size
				int dataSize = ReadDataPacketSize(SEND_RECEIVE_COUNT);
				Debug.Log("Received datat-packet size: " + dataSize);

				// Read Data Packet
				ReadDataPacket(dataSize);

				currentNumberOfPacketsReceived++;
			}
		});
	}

	// Read incoming header bytes to get size of incoming image. 读取先发过来的帧的长度
	private int ReadDataPacketSize(int size)
	{
		//Debug.Log("All set to read data-packet size");
		bool disconnected = false;

		byte[] dataBytesCount = new byte[size];
		var total = 0;
		do
		{
			//Debug.Log("Reading incoming data-packet size");
			var read = binReader.Read(dataBytesCount, total, size - total);
			if (read == 0)
			{
				disconnected = true;
				break;
			}
			total += read;
		} while (total != size);

		//Debug.Log("Finished reading incoming data-packet size ： " + disconnected);

		int byteLength;
		if (disconnected)
		{
			byteLength = -1;
		}
		else
		{
			byteLength = BitConverter.ToInt32(dataBytesCount, 0);
		}

		//Debug.Log("Data-packet size: " + byteLength);
		return byteLength;
	}

	// Read incoming bytes from stream to prepare image for display. 读帧
	void ReadDataPacket(int size)
	{
		// Debug.Log("All set to read data-packet");

		var total = 0;
		bool disconnected = false;
		byte[] dataPacket = new byte[size];

		do
		{
			// Debug.Log("Reading incoming data-packet");
			var read = binReader.Read(dataPacket, total, size - total);
			if (read == 0)
			{
				disconnected = true;
				break;
			}
			total += read;
		} while (total != size);

		bool readyToReadAgain = false;

		// Display Image
		if (!disconnected)
		{
			StoreReceivedTextDataPackets(dataPacket);
			readyToReadAgain = true;
		}

		// Wait until old Image is displayed
		while (!readyToReadAgain)
		{
#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
			WaitForSomeTime(0.001);
#else
			System.Threading.Thread.Sleep(1);
#endif
		}

		// Debug.Log("Finished reading incoming data-packet");
	}

#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
	private async void WaitForSomeTime(double timeInSeconds)
	{
		await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(timeInSeconds));
	}
#endif

	// Store received data in a queue or a byte
	public int recevingBufferSize;
	[HideInInspector]
	public Queue<byte[]> queueOfReceivedDataPackets = new Queue<byte[]>();
	[HideInInspector]
	public byte[] latestByte = null; //从线程拿出来的收到的包packet
	void StoreReceivedTextDataPackets(byte[] receivedDataPacket)
	{
		latestByte = receivedDataPacket;
		Debug.Log(Encoding.UTF8.GetString(latestByte));
	}

}