using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

#if ARYZON_ARFOUNDATION
using UnityEngine.XR.ARFoundation;
#endif

#if ARYZON_VUFORIA
using Vuforia;
#endif

namespace Aryzon
{
    public class AryzonTrackingEngineHandler
    {
        private List<SavedCameraSettings> savedCameraSettings = new List<SavedCameraSettings>();
        private int savedSleepTimeout;

        private List<MonoBehaviour> cameraBackgroundComponents = new List<MonoBehaviour>();
        private List<GameObject> cameraBackgroundObjects = new List<GameObject>();


        public void UpdateCamerasForStereoscopicMode(List<Camera> cameras)
        {
            savedSleepTimeout = Screen.sleepTimeout;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            savedCameraSettings.Clear();

            cameraBackgroundComponents.Clear();
            cameraBackgroundObjects.Clear();

            foreach (Camera cam in cameras)
            {
                if (cam.stereoTargetEye != StereoTargetEyeMask.None)
                {
                    SavedCameraSettings savedCameraSetting = new SavedCameraSettings();
                    savedCameraSetting.camera = cam;
                    savedCameraSettings.Add(savedCameraSetting);

                    cam.enabled = false;

                    if (AryzonSettings.Instance.aryzonManager.blackBackgroundInStereoscopicMode)
                    {
                        DisableBackgroundRenderer(cam);
                    }
                    /* For future use
                    SavedCameraSettings savedCameraSetting = new SavedCameraSettings();
                    savedCameraSetting.camera = cam;
                    savedCameraSetting.bgColor = cam.backgroundColor;
                    savedCameraSetting.clearFlags = cam.clearFlags;
                    savedCameraSettings.Add(savedCameraSetting);
                    if (AryzonSettings.Instance.aryzonTracking.blackBackgroundInStereoscopicMode)
                    {
                        cam.clearFlags = CameraClearFlags.SolidColor;
                        cam.backgroundColor = Color.black;
                    }
                
                    XRDevice.DisableAutoXRCameraTracking(cam, true);
                    */
                }
            }
        }

        public void ResetCameras()
        {
            EnableBackgroundRenderers();

            foreach (SavedCameraSettings savedCameraSetting in savedCameraSettings)
            {
                Camera cam = savedCameraSetting.camera;
                if (cam)
                {
                    cam.enabled = true;
                    /* For future use
                    cam.clearFlags = savedCameraSetting.clearFlags;
                    cam.backgroundColor = savedCameraSetting.bgColor;
                    */
                }
            }

            Screen.sleepTimeout = savedSleepTimeout;
        }

        public void DisableBackgroundRenderer(Camera cam)
        {
#if ARYZON_ARFOUNDATION
            if (AryzonSettings.Instance.aryzonManager.trackingEngine == TrackingEngine.ARFoundation)
            {
                ARCameraBackground cameraBackground = cam.GetComponent<ARCameraBackground>();

                if (cameraBackground && cameraBackground.enabled)
                {
                    cameraBackgroundComponents.Add(cameraBackground);
                }
            }
#endif
#if ARYZON_VUFORIA
            if (AryzonSettings.Instance.aryzonManager.trackingEngine == TrackingEngine.Vuforia)
            {
                if (VuforiaBehaviour.Instance)
                {
                    foreach (BackgroundPlaneBehaviour bpb in VuforiaBehaviour.Instance.GetComponentsInChildren<BackgroundPlaneBehaviour>())
                    {
                        if (bpb.gameObject.activeSelf)
                        {
                            cameraBackgroundObjects.Add(bpb.gameObject);
                        }
                    }
                }
            }
#endif
            foreach (MonoBehaviour background in cameraBackgroundComponents)
            {
                if (background)
                {
                    background.enabled = false;
                }
            }

            foreach (GameObject background in cameraBackgroundObjects)
            {
                if (background)
                {
                    background.SetActive(false);
                }
            }
        }

        public void EnableBackgroundRenderers()
        {
            foreach (MonoBehaviour background in cameraBackgroundComponents)
            {
                if (background)
                {
                    background.enabled = true;
                }
            }

            foreach (GameObject background in cameraBackgroundObjects)
            {
                if (background)
                {
                    background.SetActive(true);
                }
            }
        }
    }

    

    public class SavedCameraSettings
    {
        public CameraClearFlags clearFlags;
        public Color bgColor;
        public Camera camera;
    }
}

