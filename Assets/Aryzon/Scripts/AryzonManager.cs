using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;

using System.Runtime.InteropServices;

#if ARYZON_ARFOUNDATION
using UnityEngine.XR.ARFoundation;
#endif

#if ARYZON_VUFORIA
using Vuforia;
#endif

namespace Aryzon
{
	public class AryzonManager : MonoBehaviour
	{
#if UNITY_IPHONE
		[DllImport("__Internal")]
		private static extern void setBrightnessToValue(float value);
		[DllImport("__Internal")]
		private static extern void setBrightnessToHighest();
		[DllImport("__Internal")]
		private static extern float getBrightness();
		private float resetBrightness = 1f;
#endif

		[System.Obsolete("Use ARFoundation instead: useARFoundation")]
		public bool useARCore;
		[System.Obsolete("Use ARFoundation instead: useARFoundation")]
		public bool useARKit;
		public bool useARFoundation;
		public bool useVuforia;
		public bool useOther;

		private bool reticleEnabled;

		public Transform tracker;
		public Transform eyes;

		public AryzonUIController aryzonUIController;

		public bool setAryzonModeOnStart;
		public bool aryzonMode;
		public bool stereoscopicMode;
		public bool blackBackgroundInStereoscopicMode = true;

		public TrackingEngine trackingEngine;

		[Serializable]
		public class ButtonClickedEvent : UnityEvent { }

		public ButtonClickedEvent onClick = new ButtonClickedEvent();

		public UnityEvent onARFoundationStart;
		public UnityEvent onARFoundationStop;
		public UnityEvent onVuforiaStart;
		public UnityEvent onVuforiaStop;
		public UnityEvent onVuforiaSet;
		public UnityEvent onOtherStart;
		public UnityEvent onOtherStop;
		public UnityEvent onOtherSet;
		public UnityEvent onStart;
		public UnityEvent onStop;
		public UnityEvent onRotationToLandscape;
		public UnityEvent onRotationToPortrait;

		public bool showARFoundationEvents;
		public bool showVuforiaEvents;
		public bool showOtherEvents;
		public bool showAryzonModeEvents;
		public bool showRotationEvents;

		public bool startTrackingBool;
		public bool stopTrackingBool;

		public bool showReticle;

		public bool alwaysApplyPose;

		private bool objectsConnected = false;

		private Camera cameraLeft;
		private Camera cameraRight;

		private GameObject cameras;
		private GameObject uiOverlay;
		private GameObject portraitUI;
		private GameObject landscapeUI;
		private Transform stereoscopic;

		public ARCameraShifter cameraShifter;

		private UnityAction arFoundationStart;
		private UnityAction vuforiaStart;

		private bool checkingOrientation;
		private bool landscape;
		private bool landscapeMode;

		private bool autorotateToLandscapeRight;
		private bool autorotateToLandscapeLeft;
		private bool autorotateToPortrait;
		private bool autorotateToPortraitUpsideDown;
		private ScreenOrientation orientation;

		private AryzonTrackingEngineHandler _trackingEngineHandler;
		private AryzonTrackingEngineHandler trackingEngineHandler
		{
			get
			{
				if (_trackingEngineHandler == null)
				{
					_trackingEngineHandler = new AryzonTrackingEngineHandler();
				}
				return _trackingEngineHandler;
			}
		}

		#if UNITY_IOS && !UNITY_EDITOR
		private float timer;
		#endif
		private void OnEnable()
		{
			AryzonSettings.Instance.UpdateLayout += UpdateLayoutFromEvent;
		}

		private void OnDisable()
		{
			if (AryzonSettings.Instance != null)
			{
				AryzonSettings.Instance.UpdateLayout -= UpdateLayoutFromEvent;
			}
		}

		private void UpdateLayoutFromEvent()
		{
			cameraShifter.UpdateLayout();
		}

		private void ConnectObjects()
		{

			if (!objectsConnected)
			{
				Transform uiOverlayTransform = transform.Find("UI Overlay");
				Transform portraitUITransform = transform.Find("UI Overlay/Portrait");
				Transform landscapeUITransform = transform.Find("UI Overlay/Landscape");

				stereoscopic = transform.Find("Stereoscopic");
				Transform camerasTransform = transform.Find("Stereoscopic/Cameras");
				Transform cameraLeftTransform = transform.Find("Stereoscopic/Cameras/Left");
				Transform cameraRightTransform = transform.Find("Stereoscopic/Cameras/Right");

				Transform reticleTransform = transform.Find("Stereoscopic/Cameras/Right/Reticle/Dot");

				if (uiOverlayTransform)
				{
					uiOverlay = uiOverlayTransform.gameObject;
				}
				if (portraitUITransform)
				{
					portraitUI = portraitUITransform.gameObject;
				}
				if (landscapeUITransform)
				{
					landscapeUI = landscapeUITransform.gameObject;
				}
				if (camerasTransform)
				{
					cameras = camerasTransform.gameObject;
				}
				if (cameraLeftTransform)
				{
					cameraLeft = cameraLeftTransform.gameObject.GetComponent<Camera>();
				}
				if (cameraRightTransform)
				{
					cameraRight = cameraRightTransform.gameObject.GetComponent<Camera>();
				}

				tracker = stereoscopic;
				eyes = camerasTransform;

				aryzonUIController = portraitUITransform.gameObject.GetComponentInChildren<AryzonUIController>();

				reticleEnabled = false;

				objectsConnected = true;
			}
		}

		private void Awake()
		{
			AryzonSettings.Instance.aryzonManager = this;

			ConnectObjects();

			aryzonMode = false;
			landscapeMode = false;
			stereoscopicMode = false;

			showReticle = false;

			cameraShifter = new ARCameraShifter();
			cameraShifter.singleMode = false;
			cameraShifter.cameras = cameras;
			cameraShifter.left = cameraLeft;
			cameraShifter.right = cameraRight;

			cameraShifter.Setup();
		}

		void Start()
		{
			if (setAryzonModeOnStart)
			{
				StartAryzonMode();
			}
		}

		void Update()
		{
#if UNITY_IOS && !UNITY_EDITOR
			if (aryzonMode && landscapeMode && Application.platform == RuntimePlatform.IPhonePlayer)
			{
				if (timer > 5f)
				{
					float randomFactor = UnityEngine.Random.Range(0.0f, 0.05f);
					SetScreenBrightness(0.85f + randomFactor);
					timer = 0f;
				}
				timer += Time.deltaTime;
			}
#endif
#if UNITY_EDITOR
			if (startTrackingBool)
			{
				startTrackingBool = false;
				StartAryzonMode();
			}
			if (stopTrackingBool)
			{
				stopTrackingBool = false;
				StopAryzonMode();
			}
#endif
		}

		IEnumerator CheckAspectAfter(float seconds)
		{
			yield return new WaitForSeconds(seconds);
			CheckAspectRatio();
		}

		public void DoCheckOnAspect()
		{
			if (gameObject.activeInHierarchy)
            {
				StartCoroutine(CheckAspectAfter(0.2f));
			}
		}

		private void LateUpdate()
		{
			if (!reticleEnabled && showReticle)
			{
				reticleEnabled = true;
			}
			else if (reticleEnabled && !showReticle)
			{
				reticleEnabled = false;
			}
			showReticle = false;
		}

		public void StartAryzonMode()
		{
			if (!aryzonMode)
			{
				AryzonSettings.Instance.AryzonMode = true;
				AryzonSettings.Instance.reticleCamera = cameraRight;
				AryzonSettings.Instance.aryzonManager = this;

				if (!Application.isEditor)
				{
					SaveRotationParameters();
					SetRotationLandscapeAndPortrait();
				}
				aryzonMode = true;
				ConnectObjects();
				uiOverlay.SetActive(true);
				if (trackingEngine == TrackingEngine.ARFoundation)
				{
					onARFoundationStart.Invoke();
				}
				else if (trackingEngine == TrackingEngine.Vuforia)
				{
					onVuforiaStart.Invoke();
				}
				else if (trackingEngine == TrackingEngine.Other)
				{
					onOtherStart.Invoke();
				}

				CheckAspectRatio();

				onStart.Invoke();
				AryzonSettings.Instance.OnStartAryzonMode(new AryzonModeEventArgs());
			}
		}

		public void StopAryzonMode()
		{
			if (aryzonMode)
			{
				AryzonSettings.Instance.AryzonMode = false;
				uiOverlay.SetActive(false);
				if (trackingEngine == TrackingEngine.ARFoundation)
				{
					onARFoundationStop.Invoke();
				}
				else if (trackingEngine == TrackingEngine.Vuforia)
				{
					onVuforiaStop.Invoke();
				}
				else if (trackingEngine == TrackingEngine.Other)
				{
					onOtherStop.Invoke();
				}
				if (landscape)
				{
					ResetScreenBrightness();
				}

				stereoscopic.gameObject.SetActive(false);
				if (!Application.isEditor)
				{
					ResetRotationParameters();
				}
				onStop.Invoke();

				aryzonMode = false;
				AryzonSettings.Instance.OnStopAryzonMode(new AryzonModeEventArgs());
			}
		}

		private void SaveRotationParameters()
		{
			autorotateToLandscapeLeft = Screen.autorotateToLandscapeLeft;
			autorotateToLandscapeRight = Screen.autorotateToLandscapeRight;
			autorotateToPortrait = Screen.autorotateToPortrait;
			autorotateToPortraitUpsideDown = Screen.autorotateToPortraitUpsideDown;

			if (autorotateToLandscapeLeft || autorotateToLandscapeRight || autorotateToPortrait || autorotateToPortraitUpsideDown)
			{
				orientation = ScreenOrientation.AutoRotation;
			}
			else
			{
				orientation = Screen.orientation;
			}
		}

		private void ResetRotationParameters()
		{
			Screen.autorotateToLandscapeLeft = autorotateToLandscapeLeft;
			Screen.autorotateToLandscapeRight = autorotateToLandscapeRight;
			Screen.autorotateToPortrait = autorotateToPortrait;
			Screen.autorotateToPortraitUpsideDown = autorotateToPortraitUpsideDown;
			Screen.orientation = orientation;
		}

		public void SetRotationLandscapeAndPortrait()
		{
			Screen.autorotateToLandscapeRight = !AryzonSettings.Headset.landscapeLeft;
			Screen.autorotateToPortraitUpsideDown = false;
			Screen.autorotateToLandscapeLeft = AryzonSettings.Headset.landscapeLeft;
			Screen.autorotateToPortrait = true;
			Screen.orientation = ScreenOrientation.AutoRotation;
		}

		private void SetScreenBrightness(float value)
		{
			if (Application.platform == RuntimePlatform.Android)
			{
#if UNITY_ANDROID
				AndroidJavaClass ajc = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				AndroidJavaObject ajo = ajc.GetStatic<AndroidJavaObject>("currentActivity");
				
				ajo.Call("runOnUiThread", new AndroidJavaRunnable(() => { 
					using (
					AndroidJavaObject windowManagerInstance = ajo.Call<AndroidJavaObject>("getWindowManager"),
					windowInstance = ajo.Call<AndroidJavaObject>("getWindow"),
					layoutParams = windowInstance.Call<AndroidJavaObject>("getAttributes")
					) {
						layoutParams.Set<float>("screenBrightness",value);
						windowInstance.Call("setAttributes", layoutParams);//new AndroidJavaObject[1] {layoutParams}
					}
				}));
#endif
			}
			else if (Application.platform == RuntimePlatform.IPhonePlayer)
			{
#if UNITY_IPHONE
				resetBrightness = getBrightness();
				setBrightnessToValue(value);
#endif
			}
		}

		private void ResetScreenBrightness()
		{
			if (Application.platform == RuntimePlatform.Android)
			{
#if UNITY_ANDROID
				AndroidJavaClass ajc = new AndroidJavaClass("com.unity3d.player.UnityPlayer"); 
				AndroidJavaObject ajo = ajc.GetStatic<AndroidJavaObject>("currentActivity");
				ajo.Call("runOnUiThread", new AndroidJavaRunnable(() => { 
					using (
					AndroidJavaObject windowManagerInstance = ajo.Call<AndroidJavaObject>("getWindowManager"),
					windowInstance = ajo.Call<AndroidJavaObject>("getWindow"),
					layoutParams = windowInstance.Call<AndroidJavaObject>("getAttributes")
					) {
						layoutParams.Set<float>("screenBrightness",-1f);
						windowInstance.Call("setAttributes", layoutParams);
					}
				}));
#endif
			}
			else if (Application.platform == RuntimePlatform.IPhonePlayer)
			{
#if UNITY_IPHONE
				setBrightnessToValue(resetBrightness);
#endif
			}
		}

		private void CheckAspectRatio()
		{
			if (!checkingOrientation && this.gameObject.activeInHierarchy && aryzonMode)
			{
				checkingOrientation = true;

				landscape = false;
				if (Screen.width > Screen.height)
				{
					landscape = true;
				}
				if (landscape && !landscapeMode)
				{
					Debug.Log("[Aryzon] Entering stereoscopic mode");
					SetScreenBrightness(0.9f); //slightly less then 1 to reduce heat
					#if UNITY_IOS && !UNITY_EDITOR
					timer = 0f;
					#endif
					stereoscopic.gameObject.SetActive(true);

					foreach (FadeOutAndDisable fader in portraitUI.GetComponentsInChildren<FadeOutAndDisable>())
					{
						fader.Disable();
					}
					aryzonUIController.Inactivate();
					landscapeUI.SetActive(true);

					onRotationToLandscape.Invoke();
					landscapeMode = true;

					StartCoroutine(InvokeARModeAfterFrame());

					AryzonSettings.Instance.LandscapeMode = true;
					stereoscopicMode = true;
					AryzonSettings.Instance.OnStartStereoscopicMode(new AryzonModeEventArgs());

				}
				else if (!landscape && landscapeMode)
				{
					Debug.Log("[Aryzon] Exiting stereoscopic mode");
					ResetScreenBrightness();
					stereoscopic.gameObject.SetActive(false);

					foreach (FadeOutAndDisable fader in portraitUI.GetComponentsInChildren<FadeOutAndDisable>(true))
					{
						fader.fading = false;
						fader.gameObject.SetActive(false);
						fader.gameObject.SetActive(true);
					}
					aryzonUIController.Activate();
					landscapeUI.SetActive(false);

					onRotationToPortrait.Invoke();
					landscapeMode = false;

					AryzonSettings.Instance.PortraitMode = true;
					stereoscopicMode = false;
					AryzonSettings.Instance.OnStopStereoscopicMode(new AryzonModeEventArgs());
				} else if (!landscape && !landscapeMode)
                {
					aryzonUIController.Activate();
				}
				StartCoroutine(DoNotCheckOrientationForSeconds(0.5f));
			}
		}


		IEnumerator InvokeARModeAfterFrame()
		{
			yield return null;
			cameraShifter.UpdateLayout();
		}
		IEnumerator DoNotCheckOrientationForSeconds(float seconds)
		{
			yield return new WaitForSeconds(seconds);
			checkingOrientation = false;
		}
	}

	public enum TrackingEngine
	{
		Other,
		ARFoundation,
		Vuforia,
	}
}