using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using UnityEngine;
using UnityEngine.XR;

using System.Runtime.InteropServices;

namespace Aryzon
{
    public class AryzonPoseProvider : MonoBehaviour
    {// : BasePoseProvider {

        //public ExternalPoseProvider externalPoseProvider;

        public bool predict = true;
        //public bool updateOnGetPose = false;
        private int rotationPredictSteps
        {
            get { return AryzonSettings.Phone.rotationPredictSteps; }
        }
        private int positionPredictSteps
        {
            get { return AryzonSettings.Phone.positionPredictSteps; }
        }

        public int positionSmoothCount
        {
            get
            {
                if (px != null)
                {
                    return px.count;
                }
                return 0;
            }
            set
            {
                px.count = value;
                py.count = value;
                pz.count = value;
            }
        }

        public int rotationSmoothCount
        {
            get
            {
                if (rx != null)
                {
                    return rx.count;
                }
                return 0;
            }
            set
            {
                rx.count = value;
                ry.count = value;
                rz.count = value;
                rw.count = value;
            }
        }

        //public ARSession arSession;
        private int frameCount = 10;
        public bool rotateWhenNoTracking = false;

        public double cameraFrameTime;

        public Transform externalPoseTransform;

        private Quaternion startGyroRotation;
        private Quaternion startObjectRotation;

        private Quaternion[] baseRotations;
        private Quaternion[] rotations;
        private Vector3[] positions;

        //private double positionPredictTime = 0;
        //private double startUpdateTime = 0;
        //private double endOfFrameTime = 0;

        //private double[] deltaTimes;
        //private int deltaTimesCount = 10;
        //private int updateDataMilliseconds = 16;
        //private bool active = false;

        //private float rotationResetTime = 0f;
        //private int positionPredictFrames = 3;
        //private int rotationPredictFrames = 1;

        //private int predictorder = 1;

        private int initializeFrame = 0;

        private bool initialised = false;
        private bool initializing = false;
        private bool canPredict = false;
        private bool didUpdate = false;
        private bool didResetRotation = false;
        private bool intialisedRotationOnly;

        private float rotationZStart = 0f;

        private Pose basePose;
        private Pose lastPose;

        private PosePredict px;
        private PosePredict py;
        private PosePredict pz;

        private PosePredict rx;
        private PosePredict ry;
        private PosePredict rz;
        private PosePredict rw;

        //private bool moving = true;
        //private Vector3 movementSample = Vector3.zero;
        //private float movementThreshold = 0.02f;
        //private float movementTimer = 0f;
        //private float movementTimerThreshold = 0.1f;

        //private bool rotating = true;
        
        //private float rotateThreshold = 2f;
        //private float rotateTimer = 0f;
        //private float rotateTimerThreshold = 0.1f;

        //private bool lockData = false;

#if UNITY_ANDROID
        [DllImport("AryzonPose")]
        private extern static float[] GetRotationMatrix();

        private AndroidJavaObject context;
        private AndroidJavaObject poseManager;
        private bool startedGyroUpdates = false;
#elif UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private extern static Matrix4x4 getRotationMatrix();
        [DllImport("__Internal")]
        private extern static void startCoreMotion();
        [DllImport("__Internal")]
        private extern static void stopCoreMotion();
#elif UNITY_IOS
        private static Matrix4x4 getRotationMatrix() { return Matrix4x4.identity; }
        private static void startCoreMotion() { }
        private static void stopCoreMotion() { }
#endif

        private void OnDisable()
        {
#if UNITY_2020_1_OR_NEWER
            InputDevices.deviceConnected -= OnInputDeviceConnected;
#endif // UNITY_UNITY_2020_1_OR_NEWER
#if UNITY_ANDROID
            if (Application.platform == RuntimePlatform.Android)
            {
                if (poseManager != null)
                {
                    poseManager.Call("Stop");
                }
            }
            startedGyroUpdates = false;
#elif UNITY_IOS
            stopCoreMotion();
#endif
        }

        private void OnEnable()
        {
#if UNITY_2020_1_OR_NEWER
            List<InputDevice> devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.TrackedDevice, devices);
            foreach (var device in devices)
            {
                if (device.characteristics.HasFlag(InputDeviceCharacteristics.TrackedDevice))
                {
                    CheckConnectedDevice(device, false);
                }
            }

            InputDevices.deviceConnected += OnInputDeviceConnected;
#endif // UNITY_UNITY_2020_1_OR_NEWER

#if UNITY_ANDROID
            if (Application.platform == RuntimePlatform.Android)
            {
                if (poseManager == null || context == null)
                {
                    AndroidJavaClass ajc = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    AndroidJavaObject activity = ajc.GetStatic<AndroidJavaObject>("currentActivity");
                    context = activity.Call<AndroidJavaObject>("getApplicationContext");
                    poseManager = new AndroidJavaObject("com.aryzon.aryzonpose.PoseManager");
                }

                startedGyroUpdates = poseManager.Call<bool>("Start", new object[] { context });
                if (startedGyroUpdates)
                {
                    canPredict = true;
                }
            }
#elif UNITY_IOS
            startCoreMotion();
            canPredict = true;
#else
            Input.gyro.enabled = true;
#endif

            if (px == null)
            {
                px = new PosePredict(positionSmoothCount);
                py = new PosePredict(positionSmoothCount);
                pz = new PosePredict(positionSmoothCount);

                rx = new PosePredict(rotationSmoothCount);
                ry = new PosePredict(rotationSmoothCount);
                rz = new PosePredict(rotationSmoothCount);
                rw = new PosePredict(rotationSmoothCount);
            }
            positionSmoothCount = 4;
            rotationSmoothCount = 4;

            ResetState();
            /*deltaTimes = new double[deltaTimesCount];
            Array.Clear(deltaTimes, 0, deltaTimesCount);*/

            lastPose = new Pose(Vector3.zero, Quaternion.identity);
            //active = true;

            //endOfFrameTime = GetTiming();
            //positionPredictTime = (1 / 60f) * 5;
        }

        private float slerpTimer = 0f;
        private float resetTimeToTake = 3f;

        private Quaternion deltaRotation = Quaternion.identity;
        private Quaternion currentDeltaRotation = Quaternion.identity;
        private Quaternion previousDeltaRotation = Quaternion.identity;

        public void DataUpdate()
        {
            doAgain = true;
            didUpdate = true;
            //startUpdateTime = GetTiming();
            AryzonPoseDataFlags flags;
            if (externalPoseTransform)
            {
                flags = (AryzonPoseDataFlags.Position | AryzonPoseDataFlags.Rotation);
                basePose = new Pose(externalPoseTransform.position, externalPoseTransform.rotation);
            }
            else
            {
                flags = GetNodePoseData(XRNode.CenterEye, out basePose);
            }
            if (flags == (AryzonPoseDataFlags.Position | AryzonPoseDataFlags.Rotation))
            {
                lastPose = basePose;
                didResetRotation = false;
            }
            else
            {
                basePose = lastPose;
                if (didResetRotation)
                {
                    if (rotateWhenNoTracking)
                    {
                        if (Screen.orientation == ScreenOrientation.LandscapeLeft)
                        {
                            basePose.rotation = Quaternion.Euler(90f, 0f, -90f - rotationZStart) * GetAdjustedRotation();
                        }
                        else if (Screen.orientation == ScreenOrientation.LandscapeRight)
                        {
                            basePose.rotation = Quaternion.Euler(90f, 0f, 90f - rotationZStart) * GetAdjustedRotation();
                        }
                        else
                        {
                            basePose.rotation = Quaternion.Euler(90f, 0f, -rotationZStart) * GetAdjustedRotation();
                        }
                    }
                    if (!intialisedRotationOnly)
                    {
                        if (initializeFrame == 0)
                        {
                            #if !UNITY_ANDROID && !UNITY_IOS
                            if (Input.gyro.enabled)
                            {
                                canPredict = true;
                            }
                            #endif
                            rotations = new Quaternion[frameCount];
                        }

                        else if (initializeFrame == frameCount - 1)
                        {
                            intialisedRotationOnly = true;
                        }
                        if (canPredict)
                        {
                            rotations[initializeFrame] = basePose.rotation;
                        }
                        initializeFrame++;
                    }
                    else
                    {
                        for (int i = 0; i < frameCount - 1; i++)
                        {
                            rotations[i] = rotations[i + 1];
                        }
                        rotations[frameCount - 1] = basePose.rotation;
                    }
                }
                else if (!didResetRotation && Mathf.Abs(GyroRotation().eulerAngles.y) > 0.1f)
                {
                    rotationZStart = GyroRotation().eulerAngles.z;
                    ResetState();
                    didResetRotation = true;
                    initializeFrame = 0;
                    intialisedRotationOnly = false;
                }
                else
                {
                    basePose.rotation = Quaternion.identity;
                }
                return;
            }

            if (initializing)
            {
                Quaternion q = basePose.rotation;
                float length = Mathf.Sqrt(q.w * q.w + q.x * q.x + q.y * q.y + q.z * q.z);
                if (length < 0.5f || GyroRotation().w > 0.999f)
                {
                    return;
                }

                if (initializeFrame == 0)
                {
#if !UNITY_ANDROID && !UNITY_IOS
                    if (Input.gyro.enabled)
                    {
                        canPredict = true;
                    }
#endif
                    baseRotations = new Quaternion[frameCount];
                    rotations = new Quaternion[frameCount];
                    positions = new Vector3[frameCount];
                }
                else if (initializeFrame == frameCount - 1)
                {
                    initializing = false;
                    initialised = true;
                    if (canPredict)
                    {
                        startGyroRotation = GyroRotation();
                        startObjectRotation = basePose.rotation;
                        currentDeltaRotation = startObjectRotation * Quaternion.Inverse(startGyroRotation);
                        deltaRotation = currentDeltaRotation;
                    }
                }
                if (canPredict)
                {
                    baseRotations[initializeFrame] = basePose.rotation;
                    rotations[initializeFrame] = basePose.rotation;
                    positions[initializeFrame] = GetPosition();
                }
                initializeFrame++;
            }
            else if (canPredict && initialised)
            {
                if (!closingGap && !Rotating())
                {
                    startGyroRotation = GyroRotation();
                    startObjectRotation = baseRotations[frameCount - 1];

                    previousDeltaRotation = deltaRotation;
                    currentDeltaRotation = startObjectRotation * Quaternion.Inverse(startGyroRotation);

                    StartCoroutine(CloseErrorGap());
                }
                else if (!closingGap)
                {
                    UpdateArrays();
                }
            }
            
            //StartCoroutine(SetEndOfFrameTime(startUpdateTime));
        }

        private bool closingGap = false;

        private IEnumerator CloseErrorGap()
        {
            closingGap = true;
            slerpTimer = 0f;
            while (slerpTimer < resetTimeToTake)
            {
                float value = SmoothValue.GetSmoothValueForRange(0f, 1f, slerpTimer, resetTimeToTake, SmoothValue.AnimationType.SmoothInOut);
                //startGyroRotation = GyroRotation();
                //startObjectRotation = baseRotations[frameCount - 1];
                //currentDeltaRotation = startObjectRotation * Quaternion.Inverse(startGyroRotation);
                deltaRotation = Quaternion.Lerp(previousDeltaRotation, currentDeltaRotation, value);
                UpdateArrays();
                slerpTimer += Time.deltaTime;
                yield return null;
            }
            deltaRotation = currentDeltaRotation;
            UpdateArrays();
            closingGap = false;
        }

        private bool Rotating()
        {
            float wValue = Mathf.Abs((Quaternion.Inverse(rotations[0]) * rotations[frameCount - 1]).w);
            if (wValue < 0.999f)
            {
                return true;
            }
            return false;
        }

        private Quaternion GyroRotation()
        {
#if UNITY_ANDROID
    #if UNITY_EDITOR
            return Quaternion.identity;
    #else
            Matrix4x4 rotationMatrix = new Matrix4x4();
            float[] rotationFloatArray = poseManager.CallStatic<float[]>("GetRotationMatrix");

            for (int i = 0; i < 16; i++)
            {
                rotationMatrix[i] = rotationFloatArray[i];
            }
            Quaternion androidQ = rotationMatrix.rotation;
            if (Screen.orientation == ScreenOrientation.Portrait)
            {
                return new Quaternion(androidQ.x, androidQ.y, -androidQ.z, androidQ.w);
            }
            else if (Screen.orientation == ScreenOrientation.LandscapeLeft)
            {
                return new Quaternion(androidQ.y, -androidQ.x, androidQ.z, -androidQ.w);
            }
            else if (Screen.orientation == ScreenOrientation.LandscapeRight)
            {
                return new Quaternion(-androidQ.y, androidQ.x, androidQ.z, -androidQ.w);
            }
            else
            {
                return Quaternion.identity;
            }
    #endif
#elif UNITY_IOS
            Matrix4x4 rotationMatrix = getRotationMatrix();
            Quaternion Q = rotationMatrix.rotation;
            if (Screen.orientation == ScreenOrientation.Portrait)
            {
                return new Quaternion(Q.x, Q.y, -Q.z, Q.w);
            }
            else if (Screen.orientation == ScreenOrientation.LandscapeLeft)
            {
                return new Quaternion(Q.y, -Q.x, Q.z, -Q.w);
            }
            else if (Screen.orientation == ScreenOrientation.LandscapeRight)
            {
                return new Quaternion(-Q.y, Q.x, Q.z, -Q.w);
            }
            else
            {
                return Quaternion.identity;
            }
#else
            return Input.gyro.attitude;
#endif
        }

        private Pose savedOutput;
        private bool doAgain = true;

        private void AddLatestPrediction(double[] predictionArray, double prediction)
        {
            for (int i = 1; i < predictionArray.Length; i++)
            {
                predictionArray[i - 1] = predictionArray[i];
            }
            predictionArray[predictionArray.Length - 1] = prediction;
        }

        public Pose GetPredictedPose()
        {
            if (!didUpdate)
            {
                DataUpdate();
            }
            didUpdate = false;
            Pose output = basePose;

            if (initialised && doAgain && canPredict && predict)
            {

                doAgain = false;
                Vector3 predictedPosition = Vector3.zero;
                Quaternion predictedRotation = rotations[frameCount - 1];

                //double captureTime = positionPredictTime + cameraFrameTime;

                //positionPredictFrames = Mathf.Clamp(Mathf.RoundToInt((float)(captureTime / updateDataMilliseconds)), 0, 3);
                //rotationPredictFrames = 1;
                //rotationPredictSteps = 1;

                predictedPosition.x = px.NextValue(basePose.position.x, positionPredictSteps);
                predictedPosition.y = py.NextValue(basePose.position.y, positionPredictSteps);
                predictedPosition.z = pz.NextValue(basePose.position.z, positionPredictSteps);

                predictedRotation.x = rx.NextValue(rotations[frameCount - 1].x, rotationPredictSteps);
                predictedRotation.y = ry.NextValue(rotations[frameCount - 1].y, rotationPredictSteps);
                predictedRotation.z = rz.NextValue(rotations[frameCount - 1].z, rotationPredictSteps);
                predictedRotation.w = rw.NextValue(rotations[frameCount - 1].w, rotationPredictSteps);

                output = new Pose(predictedPosition, predictedRotation);
                savedOutput = output;
            } else if (intialisedRotationOnly && doAgain && canPredict && predict)
            {
                doAgain = false;

                Quaternion predictedRotation = rotations[frameCount - 1];

                predictedRotation.x = rx.NextValue(rotations[frameCount - 1].x, rotationPredictSteps);
                predictedRotation.y = ry.NextValue(rotations[frameCount - 1].y, rotationPredictSteps);
                predictedRotation.z = rz.NextValue(rotations[frameCount - 1].z, rotationPredictSteps);
                predictedRotation.w = rw.NextValue(rotations[frameCount - 1].w, rotationPredictSteps);

                output = new Pose(basePose.position, predictedRotation);
            }
            else if (initialised && canPredict && !doAgain && predict)
            {
                output = savedOutput;
            }
            else if (!initializing && predict)
            {
                initializing = true;
            }
            AryzonSettings.Instance.aryzonManager.tracker.position = output.position;
            AryzonSettings.Instance.aryzonManager.tracker.rotation = output.rotation;

            if (AryzonSettings.Instance.aryzonManager.stereoscopicMode)
            {
                output.position = AryzonSettings.Instance.aryzonManager.eyes.position;
                output.rotation = AryzonSettings.Instance.aryzonManager.eyes.rotation;
            }

            return output;
        }

        private void LogQuaternionArray(string logName, Quaternion[] qs)
        {
            string output = logName + ": \n";
            output += "Q1;Q2;Q3;Q4;\n";
            foreach (Quaternion q in qs)
            {
                for (int i = 0; i < 4; i++)
                {
                    output += q[i].ToString("F6") + ";";
                }
                output += "\n";
            }
            output = output.Replace(".", ",");
        }

        private void LogQuaternion(string logName, Quaternion q)
        {
            Debug.Log(logName + ": Quaternion( " + q.x.ToString("F3") + " " + q.y.ToString("F3") + " " + q.z.ToString("F3") + " " + q.w.ToString("F3") + " )");
        }

        private void LogVector3(string logName, Vector3 v)
        {
            Debug.Log(logName + ": Vector3( " + v.x.ToString("F3") + " " + v.y.ToString("F3") + " " + v.z.ToString("F3") + " )");
        }

        /*private IEnumerator SetEndOfFrameTime(double startTime)
        {
            yield return new WaitForEndOfFrame();
            double endTime = GetTiming();
            UpdateDeltaTimes(endTime - startTime);
        }

        private void UpdateDeltaTimes(double deltaTime)
        {
            double total = 0.0;
            for (int i = 0; i < deltaTimesCount - 1; i++)
            {
                deltaTimes[i] = deltaTimes[i + 1];
                total += deltaTimes[i];
            }
            deltaTimes[deltaTimesCount - 1] = deltaTime;
            positionPredictTime = total / deltaTimesCount;
        }*/

        private void UpdateArrays()
        {
            for (int i = 0; i < frameCount - 1; i++)
            {
                baseRotations[i] = baseRotations[i + 1];
                rotations[i] = rotations[i + 1];
                positions[i] = positions[i + 1];
            }
            baseRotations[frameCount - 1] = basePose.rotation;
            rotations[frameCount - 1] = GetAdjustedRotation();
            positions[frameCount - 1] = GetPosition();
        }

        private Quaternion GetAdjustedRotation()
        {
#if UNITY_ANDROID || UNITY_IOS
            Quaternion quaternion = deltaRotation * GyroRotation();
#else
            Quaternion quaternion = startObjectRotation * UnityGyro((Quaternion.Inverse(startGyroRotation) * GyroRotation()));
#endif
            return quaternion;
        }

        private Vector3 GetPosition()
        {
            return basePose.position;
        }

        private double GetTiming()
        {
            return DateTime.Now.Ticks / (double)(TimeSpan.TicksPerMillisecond);
        }

        private void ResetState()
        {
            startObjectRotation = Quaternion.identity;
            startGyroRotation = GyroRotation();
            currentDeltaRotation = Quaternion.identity;
            deltaRotation = Quaternion.identity;
            initialised = false;
            initializing = false;
            initializeFrame = 0;
        }


#if UNITY_2020_1_OR_NEWER
        static internal InputDevice? s_InputTrackingDevice = null;

        void OnInputDeviceConnected(InputDevice device) => CheckConnectedDevice(device);

        void CheckConnectedDevice(InputDevice device, bool displayWarning = true)
        {
            var positionSuccess = false;
            var rotationSuccess = false;
            if (!(positionSuccess = device.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 position)))
                positionSuccess = device.TryGetFeatureValue(CommonUsages.colorCameraPosition, out position);
            if (!(rotationSuccess = device.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion rotation)))
                rotationSuccess = device.TryGetFeatureValue(CommonUsages.colorCameraRotation, out rotation);

            if (positionSuccess && rotationSuccess)
            {
                if (s_InputTrackingDevice == null)
                {
                    s_InputTrackingDevice = device;
                }
                else
                {
                    Debug.LogWarning($"An input device {device.name} with the TrackedDevice characteristic was registered but the AryzonPoseProvider is already consuming data from {s_InputTrackingDevice.Value.name}.");
                }
            }
        }

#else
        private List<UnityEngine.XR.XRNodeState> nodeStates = new List<UnityEngine.XR.XRNodeState>();
#endif
        private AryzonPoseDataFlags GetNodePoseData(XRNode node, out Pose resultPose)
        {
            AryzonPoseDataFlags retData = AryzonPoseDataFlags.NoData;

#if UNITY_2020_1_OR_NEWER
            if (s_InputTrackingDevice != null)
            {
                var pose = Pose.identity;
                var positionSuccess = false;
                var rotationSuccess = false;

                if (!(positionSuccess = s_InputTrackingDevice.Value.TryGetFeatureValue(CommonUsages.centerEyePosition, out pose.position)))
                    positionSuccess = s_InputTrackingDevice.Value.TryGetFeatureValue(CommonUsages.colorCameraPosition, out pose.position);
                if (!(rotationSuccess = s_InputTrackingDevice.Value.TryGetFeatureValue(CommonUsages.centerEyeRotation, out pose.rotation)))
                    rotationSuccess = s_InputTrackingDevice.Value.TryGetFeatureValue(CommonUsages.colorCameraRotation, out pose.rotation);

                if (positionSuccess) {
                    resultPose.position = pose.position;
                    retData |= AryzonPoseDataFlags.Position;
                }
                if (rotationSuccess) {
                    resultPose.rotation = pose.rotation;
                    retData |= AryzonPoseDataFlags.Rotation;
                }
            }
#else


            InputTracking.GetNodeStates(nodeStates);

            foreach (XRNodeState nodeState in nodeStates)
            {
                if (nodeState.nodeType == node)
                {
                    if (nodeState.TryGetPosition(out resultPose.position))
                    {
                        retData |= AryzonPoseDataFlags.Position;
                    }
                    if (nodeState.TryGetRotation(out resultPose.rotation))
                    {
                        retData |= AryzonPoseDataFlags.Rotation;
                    }
                    return retData;
                }
            }
            resultPose = Pose.identity;
            return retData;
#endif
        }

        private static Quaternion UnityGyro(Quaternion q)
        {
            //Right to left handed
            q = new Quaternion(q.x, q.y, -q.z, -q.w);
            return q;
        }

    }
    [Flags]
    public enum AryzonPoseDataFlags
    {
        /// <summary>
        /// No data was actually set on the pose
        /// </summary>
        NoData = 0,
        /// <summary>
        /// If this flag is set, position data was updated on the associated pose struct
        /// </summary>
        Position = 1 << 0,
        /// <summary>
        /// If this flag is set, rotation data was updated on the associated pose struct
        /// </summary>
        Rotation = 1 << 1,
    }

}
