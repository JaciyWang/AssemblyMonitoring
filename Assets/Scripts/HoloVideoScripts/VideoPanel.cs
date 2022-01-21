// Copyright (c) 2017 Vulcan, Inc. All rights reserved.  
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

using VacuumShaders.TextureExtensions;

public class VideoPanel : MonoBehaviour
{

	public GameManager gameManagerScript;
	[Space(10)]
	// public GameObject camDisplayQuad;
	//public DisplayInfo displayInfo;
	//public DisplayFPS displayFPS;
	//public bool displayLogData = false;

	[Space(10)]
	public bool startSending = false;
	public bool useQuality;
	public int quality;
	public int bufferSize;
	[Space(10)]

	public int resizeToWidth;
	public int resizeToHeight;
	public int requestedFrameRate;

	int finalWidth;
	int finalHeight;
	int finalFrameRate;

	string status;
	int countOfFramesApp = 0;
	int countOfFramesActual = 0;
	int countOfFramesSent = 0;
	byte[] stackedImage;

	[HideInInspector]
	public byte[] compressedImage;
	[HideInInspector]
	public int compressedImageSize = 0;
	[HideInInspector]
	public Texture2D resizedFrameTexture;
	Texture2D stackedFrameTexture;

	Queue<byte[]> queueOfFrames = new Queue<byte[]>();
	Queue<Texture2D> queueOfTexture = new Queue<Texture2D>();

	//////////////////////////////////////////////// SET CAMERA FEED  START /////////////////////////////////////////////////////////////
	// Configure Webcam output object
	public void SetResolution(int width, int height, int framerate)
    {
        finalWidth = width;
        finalHeight = height;
        finalFrameRate = framerate;

        float aspectRatio = (float)finalWidth / (float)finalHeight;
        resizeToHeight = Mathf.CeilToInt(resizeToWidth / aspectRatio);
    }

    ///////////////////////////////////////////////// SET CAMERA FEED  END //////////////////////////////////////////////////////////////
	// use for Unity
    public void SetBytes(byte[] image)
	{
		countOfFramesActual++;
		if (queueOfFrames.Count < bufferSize)
			queueOfFrames.Enqueue(image);
	}

	// use for HoloLens
	public void SetTexture(Texture2D image)
	{
		countOfFramesActual++;
		if (queueOfTexture.Count < bufferSize)
			queueOfTexture.Enqueue(image);
	}

	private void OnDestroy()
    {
		isRunning = false;
    }

    bool isRunning = true;

    IEnumerator PreProcessFrame()
	{
		while (isRunning)
		{
			//if (queueOfFrames.Count > 0)
			if (queueOfTexture.Count > 0)
			{
				countOfFramesSent++;

                // frame：PC 专用
                /*byte[] stackedImage = queueOfFrames.Dequeue();

                stackedFrameTexture.LoadRawTextureData(stackedImage);
                stackedFrameTexture.Apply();*/

                // MediaCaptureHandler 专用，Textures：HoloLens专用
                stackedFrameTexture = queueOfTexture.Dequeue();


                status = "Texture Applied";
                //print("stackedImage.Length : " + stackedImage.Length);
                TextureResizePro.ResizePro(stackedFrameTexture, resizeToWidth, resizeToHeight, out resizedFrameTexture, false);

                status = "Texture Resized";

                //Encode to JPG for smallest size, Encode to PNG for better quality
                if (useQuality)
                {
                    compressedImage = resizedFrameTexture.EncodeToJPG(quality);
                }
                else
                {
                    compressedImage = resizedFrameTexture.EncodeToJPG();
                }
				status = "Texture Compressed";
				print(status);
				//compressedImage = queueOfFrames.Dequeue();
				gameManagerScript.PrepareToSend(compressedImage);
				compressedImageSize = compressedImage.Length;
				status = "Compression done" + compressedImage.Length;
			}

			//如果在遍历整个游戏对象层级视图后未访问到某资源（包括脚本组件），则将其视为未使用的资源。该操作也检查静态变量。
			//但是，该操作不检查脚本执行堆栈，因此，将卸载仅在脚本堆栈中引用的资源，并根据需要在下次使用其某个属性或方法时重新加载该资源。对于已在内存中修改的资源，需要格外注意。确保在触发资源垃圾回收前调用 EditorUtility.SetDirty。
			//Resources.UnloadUnusedAssets();
			//Debug.Log(status);

			if (!startSending)
				startSending = true;

			// 0.02f 大概30fps左右，0.01将近50~60fps
			yield return new WaitForSeconds(0.15f);
		}
	}

	void AllocateMemoryToTextures()
	{
		startSending = false;
		resizedFrameTexture = new Texture2D(resizeToWidth, resizeToHeight, TextureFormat.RGB24, false);

#if !UNITY_EDITOR && (UNITY_WSA || NETFX_CORE)
		stackedFrameTexture = new Texture2D (896, 504, TextureFormat.RGBA32, false);
#else
		stackedFrameTexture = new Texture2D(640, 480, TextureFormat.RGBA32, false);
#endif
	}


	
	/*void Update()
    {
		if (displayLogData)
		{
			string message1 = "\nWidth: " + finalWidth + "\nHeight: " + finalHeight;
			string message2 = "Status: " + status + "\nCompressed: " + compressedImageSize;
			string message3 = "Camera FPS: " + finalFrameRate + "\nCamera frames: " + countOfFramesActual + "\nFrames sent: " + countOfFramesSent + "\nBuffered Frames: " + queueOfFrames.Count;
			displayInfo.ClearAndSetDisplayText(message1 + "\n" + message2 + "\n" + message3);
		}
	}*/

	void Start()
	{
		isRunning = true;

		AllocateMemoryToTextures();
		//displayInfo.SetDisplayMode(displayLogData);
		//displayFPS.SetDisplayMode(displayLogData);

		/*if (displayLogData)
		{
			StartCoroutine(DisplayStatus());
		}*/

		StartCoroutine("PreProcessFrame");
	}

	// Get camera status
	public bool GetCameraStatus()
	{
		if (resizedFrameTexture == null)
		{
			return false;
		}
		return true;
	}
}
