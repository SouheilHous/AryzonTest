using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine.XR;

using UnityEngine;
#if ARYZON_ARFOUNDATION
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#elif ARYZON_ARCORE
using GoogleARCore;
#elif ARYZON_VUFORIA
using Vuforia;
#endif
using UnityEngine.Experimental.XR.Interaction;
using UnityEngine.SpatialTracking;

namespace Aryzon
{

    public class AryzonPose : BasePoseProvider, IAryzonEventHandler
    {
        [Tooltip("This creates a helper object so you can easily look around in the editor. Use 'alt' to look around. Does not work with Vuforia.")]
        public bool poseSimulatorInEditor = true;

        [Tooltip("Only set this value if you are not using ARFoundation. E.g. for Vuforia, set this to the transform of the AR Camera object.")]
        [SerializeField]
        private Transform _poseProvidingTransform;
        public Transform poseProvidingTransform
        {
            get { return _poseProvidingTransform; }
            set { _poseProvidingTransform = value; aryzonPoseProvider.externalPoseTransform = _poseProvidingTransform; }
        }
        [Space]
        [Tooltip("If you want other objects to move with the headset you can add them here.")]
        public List<Transform> applyPoseToTransforms = new List<Transform>();

        [Tooltip("Set this to true if you want the pose to always be set to the transforms in the list above, even if not in Aryzon mode.")]
        public bool alwaysApplyPose = false;

        [Tooltip("Set this to true if you want to be able to look around even when no positional tracker is active.")]
        public bool rotateWhenNotTracking = true;

#if ARYZON_ARFOUNDATION
        private ARCameraManager mainCameraManager;
#if !UNITY_EDITOR
        private ARSession arSession;
#endif
        private bool receivingUpdates = false;
#endif
        public AryzonPoseProvider aryzonPoseProvider;

        private int sleepTimeOut;


        private bool processing = false;
        private bool backgroundsDisabled = false;
#if ARYZON_ARCORE && !UNITY_EDITOR
        private long previousTimestamp = -1;
#endif
        private List<Camera> disabledCameras = new List<Camera>();

        private List<TrackedPoseDriver> trackedPoseDrivers = new List<TrackedPoseDriver>();
#if ARYZON_ARFOUNDATION
        private List<ARCameraBackground> cameraBackgrounds = new List<ARCameraBackground>();
#elif ARYZON_ARCORE
        private List<ARCoreBackgroundRenderer> cameraBackgrounds = new List<ARCoreBackgroundRenderer>();
        List<CameraMetadataValue> metadataValues = new List<CameraMetadataValue>();
#else
        private List<MonoBehaviour> cameraBackgrounds = new List<MonoBehaviour>();
#endif
        private GameObject editorPoseProvider;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport ("__Internal")]
        private static extern long uptime();
#elif UNITY_ANDROID && !UNITY_EDITOR
                private long uptime()
        { 
            if (Application.platform == RuntimePlatform.Android) {
                AndroidJavaClass SystemClock = new AndroidJavaClass("android.os.SystemClock");
                System.Int64 t = SystemClock.CallStatic<System.Int64>("elapsedRealtimeNanos");
                return (long)t;
            }
            return 0;
        }
#else
        private static long uptime() { return 0; }
#endif

        private void Awake()
        {
            AryzonSettings.Instance.RegisterEventHandler(this);
            Application.targetFrameRate = 60;

            aryzonPoseProvider = gameObject.AddComponent<AryzonPoseProvider>();
#if UNITY_EDITOR && !ARYZON_VUFORIA
            if (poseSimulatorInEditor)
            {
                editorPoseProvider = new GameObject("Editor PoseProvider");
                editorPoseProvider.transform.position = Vector3.zero;
                editorPoseProvider.transform.rotation = Quaternion.identity;

                editorPoseProvider.AddComponent<EditorPoseProvider>();
                _poseProvidingTransform = editorPoseProvider.transform;
            }
#elif UNITY_ANDROID && ARYZON_ARCORE
            GameObject arcorePoseProvider = new GameObject("ARCore PoseProvider");
            arcorePoseProvider.transform.position = Vector3.zero;
            arcorePoseProvider.transform.rotation = Quaternion.identity;

            TrackedPoseDriver tpd = arcorePoseProvider.AddComponent<TrackedPoseDriver>();
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.BeforeRender;
            tpd.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.ColorCamera);
            _poseProvidingTransform = arcorePoseProvider.transform;
#endif
#if ARYZON_VUFORIA
            StartCoroutine(AddTrackedPoseDriverWhenVuforiaReady());
#endif
            aryzonPoseProvider.externalPoseTransform = _poseProvidingTransform;
        }
#if ARYZON_VUFORIA
        IEnumerator AddTrackedPoseDriverWhenVuforiaReady()
        {
            float timer = 0f;
            while ((!VuforiaBehaviour.Instance || !AryzonSettings.Instance || !AryzonSettings.Instance.aryzonManager) && timer < 10f)
            {
                timer += Time.deltaTime;
                yield return null;
            }
            if (VuforiaBehaviour.Instance && AryzonSettings.Instance && AryzonSettings.Instance.aryzonManager)
            {
                TrackedPoseDriver tpd = VuforiaBehaviour.Instance.gameObject.GetComponent<TrackedPoseDriver>();
                if (!tpd)
                {
                    tpd = VuforiaBehaviour.Instance.gameObject.AddComponent<TrackedPoseDriver>();
                }
                tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
                tpd.updateType = TrackedPoseDriver.UpdateType.BeforeRender;
                tpd.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.ColorCamera);
                _poseProvidingTransform = VuforiaBehaviour.Instance.transform;
                if (AryzonSettings.Instance.aryzonManager.stereoscopicMode || alwaysApplyPose)
                {
                    StartPoseProcessing(AryzonSettings.Instance.aryzonManager.stereoscopicMode);
                }
            }
            else
            {
                Debug.Log("[Aryzon] Could not add TrackedPoseDriver because no Vuforia Instance was found.");
            }
        }
#endif
        void Start()
        {
            if (AryzonSettings.Instance.aryzonManager)
            {
                AryzonSettings.Instance.aryzonManager.alwaysApplyPose = alwaysApplyPose;
            }
#if !ARYZON_VUFORIA
            if (AryzonSettings.Instance.aryzonManager.stereoscopicMode || alwaysApplyPose)
            {
                StartPoseProcessing(AryzonSettings.Instance.aryzonManager.stereoscopicMode);
            }
#endif
        }

        public override PoseDataFlags GetPoseFromProvider(out Pose output)
        {


#if ARYZON_ARCORE
            if (aryzonPoseProvider.predict)
            {
#if UNITY_EDITOR
                //aryzonPoseProvider.DataUpdate();
#else
                metadataValues.Clear();
                Frame.CameraMetadata.TryGetValues(CameraMetadataTag.SensorTimestamp, metadataValues);
                if (metadataValues.Count == 1)
                {
                    CameraMetadataValue value = metadataValues[0];
                    long timestamp = value.AsLong();
                    if (timestamp != previousTimestamp)
                    {
                        long now = uptime();
                        long then = timestamp;
                        long difference = now - then;
                        
                        aryzonPoseProvider.cameraFrameTime = difference / 1000000.0;
                        //aryzonPoseProvider.DataUpdate();
                        previousTimestamp = timestamp;
                    }
                }
                else
                {
                    aryzonPoseProvider.cameraFrameTime = 0.0;
                }
                //aryzonPoseProvider.DataUpdate();
#endif
            }
            else if (!aryzonPoseProvider.predict && (aryzonPoseProvider.externalPoseTransform || alwaysApplyPose))
            {
                //aryzonPoseProvider.DataUpdate();
            }
#else
            if (aryzonPoseProvider.externalPoseTransform || (alwaysApplyPose && !aryzonPoseProvider.predict))
            {
                //aryzonPoseProvider.DataUpdate();
            }
#endif
            output = aryzonPoseProvider.GetPredictedPose();
            foreach (Transform t in applyPoseToTransforms)
            {
                t.position = output.position;
                t.rotation = output.rotation;
            }

            return (PoseDataFlags.Position | PoseDataFlags.Rotation);
        }

#if ARYZON_ARFOUNDATION
        void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
        {
            if (aryzonPoseProvider)
            {
                if (!eventArgs.timestampNs.HasValue)
                {
                    aryzonPoseProvider.cameraFrameTime = 0.0;
                }
                else
                {
                    long now = uptime();
                    long then = (long)eventArgs.timestampNs;
                    long difference = now - then;
                    aryzonPoseProvider.cameraFrameTime = (double)(difference / 1000000.0);
                    //aryzonPoseProvider.DataUpdate();
                }
            }
        }
#endif
        public void OnStartStereoscopicMode(AryzonModeEventArgs args)
        {
            sleepTimeOut = Screen.sleepTimeout;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            StartPoseProcessing(true);
        }

        public void OnStopStereoscopicMode(AryzonModeEventArgs args)
        {
            Screen.sleepTimeout = sleepTimeOut;
            StopPoseProcessing(false);
        }

        private void StartPoseProcessing(bool stereo)
        {
            if (!processing)
            {
                disabledCameras.Clear();
                processing = true;

#if UNITY_EDITOR
                aryzonPoseProvider.predict = false;
#else
                aryzonPoseProvider.predict = stereo &&  AryzonSettings.Phone.predict;
#endif
                aryzonPoseProvider.rotateWhenNoTracking = rotateWhenNotTracking;

                aryzonPoseProvider.externalPoseTransform = poseProvidingTransform;
                cameraBackgrounds.Clear();
                trackedPoseDrivers.Clear();

                foreach (Camera cam in Camera.allCameras)
                {
                    TrackedPoseDriver trackedPoseDriver = cam.GetComponent<TrackedPoseDriver>();
#if ARYZON_ARFOUNDATION
                    ARPoseDriver poseDriver = cam.GetComponent<ARPoseDriver>();
                    if (poseDriver)
                    {
                        poseDriver.enabled = false;
                        if (trackedPoseDriver)
                        {
                            trackedPoseDriver.enabled = true;
                            trackedPoseDriver.updateType = TrackedPoseDriver.UpdateType.BeforeRender;
                        } else
                        {
                            trackedPoseDriver = cam.gameObject.AddComponent<TrackedPoseDriver>();
                            trackedPoseDriver.updateType = TrackedPoseDriver.UpdateType.BeforeRender;
                        }
                    }
                    ARCameraBackground cameraBackground = cam.GetComponent<ARCameraBackground>();
                    ARCameraManager cameraManager = cam.GetComponent<ARCameraManager>();

                    if (cameraManager && cameraManager.enabled)
                    {
                        mainCameraManager = cameraManager;
                    }
#elif ARYZON_ARCORE
                    ARCoreBackgroundRenderer cameraBackground = cam.GetComponent<ARCoreBackgroundRenderer>();
#elif !ARYZON_VUFORIA
                    MonoBehaviour cameraBackground = null;
#endif
#if !ARYZON_VUFORIA
                    if (stereo && AryzonSettings.Instance.aryzonManager.blackBackgroundInStereoscopicMode)
                    {
                        if (cameraBackground && cameraBackground.enabled)
                        {
                            cameraBackgrounds.Add(cameraBackground);
                            cameraBackground.enabled = false;

                            disabledCameras.Add(cam);
                            cam.enabled = false;
                        } else if (trackedPoseDriver)
                        {
                            disabledCameras.Add(cam);
                            cam.enabled = false;
                        }

                        backgroundsDisabled = true;
                    }

                    if (trackedPoseDriver && trackedPoseDriver.enabled)
                    {
                        trackedPoseDrivers.Add(trackedPoseDriver);
                        if (trackedPoseDriver.poseProviderComponent)
                        {
                            Debug.Log("[Aryzon] Already contains a poseProviderComponent");
                        }
                        else
                        {
                            trackedPoseDriver.poseProviderComponent = this;
                        }
                    }
#else
                    if (trackedPoseDriver && trackedPoseDriver.enabled)
                    {
                        trackedPoseDrivers.Add(trackedPoseDriver);
                        if (trackedPoseDriver.poseProviderComponent)
                        {
                            Debug.Log("[Aryzon] Already contains a poseProviderComponent");
                        }
                        else
                        {
                            trackedPoseDriver.poseProviderComponent = this;
                        }
                    }
#endif
                }
#if ARYZON_ARFOUNDATION && !UNITY_EDITOR
                if (mainCameraManager)
                {
                    if (!arSession)
                    {
                        arSession = GameObject.FindObjectOfType<ARSession>();
                    }

                    if (arSession && !arSession.matchFrameRateEnabled)
                    {
                        Debug.LogWarning("[Aryzon] it is recommended to set 'Match Frame Rate' to true on the ARSession instance.");
                    } else
                    {
                        mainCameraManager.frameReceived += OnCameraFrameReceived;
                    }

                    
                    receivingUpdates = true;
                }
#elif ARYZON_VUFORIA
                if (stereo && AryzonSettings.Instance.aryzonManager.blackBackgroundInStereoscopicMode)
                {
                    if (VuforiaBehaviour.Instance)
                    {
                        BackgroundPlaneBehaviour bpb = VuforiaBehaviour.Instance.GetComponentInChildren<BackgroundPlaneBehaviour>();
                        if (bpb)
                        {
                            bpb.gameObject.SetActive(false);
                        }
                        Camera cam = VuforiaBehaviour.Instance.GetComponent<Camera>();
                        if (cam)
                        {
                            disabledCameras.Add(cam);
                            cam.enabled = false;
                        }
                    }
                    backgroundsDisabled = true;
                }
#endif
            }
            else if (!backgroundsDisabled && stereo)
            {
#if !UNITY_EDITOR
                aryzonPoseProvider.predict =  AryzonSettings.Phone.predict;
#endif
                foreach (Camera cam in Camera.allCameras)
                {

#if ARYZON_ARFOUNDATION
                    ARCameraBackground cameraBackground = cam.GetComponent<ARCameraBackground>();
#elif ARYZON_ARCORE
                    ARCoreBackgroundRenderer cameraBackground = cam.GetComponent<ARCoreBackgroundRenderer>();
#elif !ARYZON_VUFORIA
                    MonoBehaviour cameraBackground = null;
#endif
#if !ARYZON_VUFORIA
                    if (AryzonSettings.Instance.aryzonManager.blackBackgroundInStereoscopicMode)
                    {
                        if (cameraBackground && cameraBackground.enabled)
                        {
                            cameraBackgrounds.Add(cameraBackground);
                            cameraBackground.enabled = false;
                            disabledCameras.Add(cam);
                            cam.enabled = false;
                        } else if (cam.GetComponent<TrackedPoseDriver>())
                        {
                            disabledCameras.Add(cam);
                            cam.enabled = false;
                        }

                        backgroundsDisabled = true;
                    }              
#endif
                }
#if ARYZON_VUFORIA
                if (AryzonSettings.Instance.aryzonManager.blackBackgroundInStereoscopicMode)
                {
                    if (VuforiaBehaviour.Instance)
                    {
                        BackgroundPlaneBehaviour bpb = VuforiaBehaviour.Instance.GetComponentInChildren<BackgroundPlaneBehaviour>();
                        if (bpb)
                        {
                            bpb.gameObject.SetActive(false);
                        }
                        Camera cam = VuforiaBehaviour.Instance.GetComponent<Camera>();
                        if (cam)
                        {
                            disabledCameras.Add(cam);
                            cam.enabled = false;
                        }
                    }
                    backgroundsDisabled = true;
                    
                }
#endif
            }
        }

        private void StopPoseProcessing(bool stereo)
        {
            if (processing)
            {
#if UNITY_EDITOR
                aryzonPoseProvider.predict = false;
#else
                aryzonPoseProvider.predict = stereo && AryzonSettings.Phone.predict;
#endif
                if (!stereo)
                {
#if !ARYZON_VUFORIA
                    foreach (MonoBehaviour cameraBackground in cameraBackgrounds)
                    {
                        if (cameraBackground)
                        {
                            cameraBackground.enabled = true;
                        }
                    }
#else
                    if (VuforiaBehaviour.Instance)
                    {
                        BackgroundPlaneBehaviour bpb = VuforiaBehaviour.Instance.GetComponentInChildren<BackgroundPlaneBehaviour>(true);
                        if (bpb)
                        {
                            bpb.gameObject.SetActive(true);
                        }
                    }
#endif
                    backgroundsDisabled = false;
                }
                if (!alwaysApplyPose)
                {
                    foreach (TrackedPoseDriver trackedPoseDriver in trackedPoseDrivers)
                    {
                        if (trackedPoseDriver)
                        {
                            trackedPoseDriver.poseProviderComponent = null;
                        }
                    }
#if ARYZON_ARFOUNDATION
                    if (mainCameraManager && receivingUpdates)
                    {
#if !UNITY_EDITOR
                        if (arSession && !arSession.matchFrameRateEnabled)
                        {
                            Debug.LogWarning("[Aryzon] it is recommended to set 'Match Frame Rate' to true on the ARSession instance.");
                        } else
                        {
                            mainCameraManager.frameReceived -= OnCameraFrameReceived;
                        }
#endif
                        TrackedPoseDriver trackedPoseDriver = mainCameraManager.gameObject.GetComponent<TrackedPoseDriver>();
                        ARPoseDriver poseDriver = mainCameraManager.gameObject.GetComponent<ARPoseDriver>();
                        if (poseDriver && trackedPoseDriver)
                        {
                            poseDriver.enabled = true;
                            Destroy(trackedPoseDriver);
                        }

                        receivingUpdates = false;
                    }
#endif
                    }
                foreach (Camera cam in disabledCameras)
                {
                    cam.enabled = true;
                }
            }
            processing = false;
        }

        public void OnStartAryzonMode(AryzonModeEventArgs args) { }
        public void OnStopAryzonMode(AryzonModeEventArgs args) { }

        private void OnDestroy()
        {
            if (AryzonSettings.Instance)
            {
                AryzonSettings.Instance.UnregisterEventHandler(this);
                if (AryzonSettings.Instance.aryzonManager && AryzonSettings.Instance.aryzonManager.stereoscopicMode)
                {
                    alwaysApplyPose = false;
                    StopPoseProcessing(true);
                }
            }
        }
    }
}