using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using AngerRoom.Shimmer;
using System.Globalization;

[DefaultExecutionOrder(-300)]
public class EyeStateLogger : MonoBehaviour
{
    public static EyeStateLogger Instance;

    [Header("Eye Tracking")]
    public Transform head;
    public OVREyeGaze leftEyeGaze;
    public OVREyeGaze rightEyeGaze;

    [Header("Raycast")]
    public float maxDistance = 100f;
    //public LayerMask hitMask = ~0;
    [SerializeField] private LayerMask hitMask;

    [Header("Startup")]
    public float initialDelaySeconds = 4f;
    public float readyAfterSeconds = 1f;

    [Header("Debug")]
    public bool debugHitPoint = true;
    public float debugSphereSize = 0.04f;
    public Color debugSphereColor = Color.red;

    public GameObject _debugSphere;

    [Header("Debug Ray")]
    public bool debugRay = true;
    public float debugRayWidth = 0.01f;
    public Color debugRayColor = Color.red;

    LineRenderer _debugRay;

    [Header("Logging")]
    public float flushIntervalSeconds = 1f;

    public class EyePacket
    {
        public byte version;
        public string t;
        public string hp;
        public string hr;
        public string gaze;
        public string hit;
        public string obj;
        public bool isValid;
        public bool getSucceeded;
        public bool eyeStarted;
        public bool xrInitialized;
        public bool hmdPresent;
        public bool vrFocus;
        public bool inputFocus;
        public bool userPresent;
        public bool eyeSupported;
        public bool leftValid;
        public bool rightValid;
        public double pluginTime;
        public bool didHit;
        public string reason;
    }

    string _logPath;
    StreamWriter _writer;
    float _lastFlushAt = -1f;

    public bool _eyeStarted = false;
    public bool HasStableData { get; private set; } = false;
    float _firstValidPacketAt = -1f;
    int _validPacketCount = 0;
    double _observationSessionSeconds;
    double _lastSdkSampleTime = double.NaN;

    private void Awake()
    {
        Instance = this;
    }

    IEnumerator Start()
    {
        InitLogFile();

        if (initialDelaySeconds > 0f)
            yield return new WaitForSeconds(initialDelaySeconds);

        while (!OVRPlugin.initialized ||
               !OVRPlugin.hmdPresent ||
               !OVRPlugin.hasVrFocus ||
               !OVRPlugin.hasInputFocus ||
               !OVRPlugin.userPresent)
        {
            yield return null;
        }

        Debug.Log($"OVRPlugin.initialized: {OVRPlugin.initialized}");
        Debug.Log($"nativeXrApi: {OVRPlugin.nativeXrApi}");
        Debug.Log($"SystemHeadset: {OVRPlugin.GetSystemHeadsetType()}");
        Debug.Log($"eyeTrackingSupported: {OVRPlugin.eyeTrackingSupported}");

        _eyeStarted = OVRPlugin.StartEyeTracking();
        Debug.Log($"StartEyeTracking() => {_eyeStarted}");
        if (!_eyeStarted) yield break;

        if (debugHitPoint && _debugSphere == null)
        {
            _debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _debugSphere.name = "EyeTracking_DebugHitPoint";
            _debugSphere.transform.localScale = Vector3.one * debugSphereSize;

            Renderer renderer = _debugSphere.GetComponent<Renderer>();
            Shader markerShader = Shader.Find("Universal Render Pipeline/Lit");
            if (markerShader != null)
            {
                renderer.material = new Material(markerShader);
                renderer.material.color = debugSphereColor;
            }

            Collider collider = _debugSphere.GetComponent<Collider>();
            Destroy(collider);
        }

        if (_debugSphere != null) { _debugSphere.SetActive(debugHitPoint); }

        if (debugRay)
        {
            GameObject rayObj = new GameObject("EyeTracking_DebugRay");
            _debugRay = rayObj.AddComponent<LineRenderer>();

            _debugRay.positionCount = 2;
            _debugRay.startWidth = debugRayWidth;
            _debugRay.endWidth = debugRayWidth;
            _debugRay.useWorldSpace = true;

            Shader rayShader = Shader.Find("Sprites/Default");
            if (rayShader != null)
            {
                Material mat = new Material(rayShader);
                mat.color = debugRayColor;
                _debugRay.material = mat;
            }

            _debugRay.startColor = debugRayColor;
            _debugRay.endColor = debugRayColor;

            _debugRay.enabled = false;
        }

        yield return new WaitForSeconds(0.2f);
    }

    void InitLogFile()
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _logPath = RecordingSession.Instance != null ? Path.Combine(RecordingSession.Instance.Folder, "eyegaze.csv") :
            Application.persistentDataPath + "/eyegaze_" + timestamp + ".csv";
        Debug.Log("Eye gaze log path: " + _logPath);

        _writer = RecordingCsv.Create(_logPath,
            "session_id,session_time_s,observation_session_s,sample_session_s,time_source,clock_mapping_id,clock_bracket_half_width_s,gaze_age_s,sdk_time_repeated_or_backward,unity_frame,version,t,hp,hr,gaze,hit,obj,isValid,getSucceeded,eyeStarted,xrInitialized," +
            "hmdPresent,vrFocus,inputFocus,userPresent,eyeSupported,leftValid,rightValid," +
            "pluginTime,didHit,reason"
        );
        _writer.Flush();
        _lastFlushAt = Time.realtimeSinceStartup;
    }

    void Update()
    {
        _observationSessionSeconds = RecordingSession.Instance != null ? RecordingSession.Instance.Now : double.NaN;
        if (!_eyeStarted)
        {
            ResetStableData();
            SendEyeStatusPacket(false, false, false, false, 0d, false, "eye tracking not started");
            MaybeFlush();
            return;
        }

        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame && _debugSphere != null)
        {
            debugHitPoint = !debugHitPoint;

            _debugSphere.SetActive(debugHitPoint);

            Debug.Log("Debug Hit Point: " +
                      (debugHitPoint ? "ON" : "OFF"));
        }

        if (leftEyeGaze == null || rightEyeGaze == null)
        {
            OVREyeGaze[] gazes = FindObjectsByType<OVREyeGaze>(FindObjectsSortMode.None);

            foreach (OVREyeGaze gaze in gazes)
            {
                if (gaze.Eye == OVREyeGaze.EyeId.Left)
                    leftEyeGaze = gaze;

                if (gaze.Eye == OVREyeGaze.EyeId.Right)
                    rightEyeGaze = gaze;
            }

            ResetStableData();
            SendEyeStatusPacket(false, false, false, false, 0d, false, "OVREyeGaze references missing");
            MaybeFlush();
            return;
        }

        if (head == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                head = mainCamera.transform;

            ResetStableData();
            SendEyeStatusPacket(false, false, false, false, 0d, false, "head reference missing");
            MaybeFlush();
            return;
        }

        if (OVRPlugin.userPresent)
        {
            SendEyeState();
        }
        else
        {
            ResetStableData();
            SendEyeStatusPacket(false, false, false, false, 0d, false, "user not present");
        }

        MaybeFlush();
    }

    void SendEyeState()
    {
        var eyeGazesState = new OVRPlugin.EyeGazesState();
        bool getSucceeded = OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref eyeGazesState);
        _observationSessionSeconds = RecordingSession.Instance != null ? RecordingSession.Instance.Now : double.NaN;
        bool leftValid = getSucceeded &&
                         eyeGazesState.EyeGazes != null &&
                         eyeGazesState.EyeGazes.Length > 0 &&
                         eyeGazesState.EyeGazes[0].IsValid;
        bool rightValid = getSucceeded &&
                          eyeGazesState.EyeGazes != null &&
                          eyeGazesState.EyeGazes.Length > 1 &&
                          eyeGazesState.EyeGazes[1].IsValid;

        if (!leftValid || !rightValid)
        {
            ResetStableData();
            SendEyeStatusPacket(false, getSucceeded, leftValid, rightValid, eyeGazesState.Time, false, "EyeGazeState invalid");
            return;
        }

        Vector3 averageHeadRotation = head.forward.normalized;

        // Use the poses from the same SDK snapshot as pluginTime, rather than
        // OVREyeGaze transforms that may have been updated in a different frame.
        Camera gazeCamera = Camera.main;
        if (gazeCamera == null)
        {
            ResetStableData();
            SendEyeStatusPacket(false, getSucceeded, leftValid, rightValid, eyeGazesState.Time, false, "main camera missing");
            return;
        }
        var leftPose = eyeGazesState.EyeGazes[0].Pose.ToOVRPose().ToWorldSpacePose(gazeCamera);
        var rightPose = eyeGazesState.EyeGazes[1].Pose.ToOVRPose().ToWorldSpacePose(gazeCamera);
        Vector3 averageGazeDirection = ((leftPose.orientation * Vector3.forward + rightPose.orientation * Vector3.forward) * 0.5f).normalized;
        Vector3 averageGazePosition = (leftPose.position + rightPose.position) * 0.5f;

        bool didHit = Physics.Raycast(
        averageGazePosition,
        averageGazeDirection,
        out RaycastHit hit,
        maxDistance,
        hitMask,
        QueryTriggerInteraction.Ignore);

        Vector3 rayEnd = didHit ? hit.point : averageGazePosition + averageGazeDirection * maxDistance;

        if (_debugRay != null)
        {
            _debugRay.enabled = true;
            _debugRay.SetPosition(0, averageGazePosition);
            _debugRay.SetPosition(1, rayEnd);
        }

        if (!didHit)
        {
            SendEyePacket(
                true,
                getSucceeded,
                leftValid,
                rightValid,
                eyeGazesState.Time,
                false,
                "valid eye gaze; raycast did not hit",
                averageGazePosition,
                averageHeadRotation,
                averageGazeDirection,
                rayEnd,
                ""
            );
            MarkValidPacket();
            return;
        }

        if (debugHitPoint && _debugSphere != null)
        {
            _debugSphere.transform.position = hit.point;
        }

        SendEyePacket(
            true,
            getSucceeded,
            leftValid,
            rightValid,
            eyeGazesState.Time,
            true,
            "valid",
            averageGazePosition,
            averageHeadRotation,
            averageGazeDirection,
            hit.point,
            hit.collider.gameObject.name
        );

        MarkValidPacket();
    }

    void SendEyePacket(
        bool isValid,
        bool getSucceeded,
        bool leftValid,
        bool rightValid,
        double pluginTime,
        bool didHit,
        string reason,
        Vector3 averageGazePosition,
        Vector3 averageHeadRotation,
        Vector3 averageGazeDirection,
        Vector3 hitPoint,
        string hitObject)
    {
        var packet = new EyePacket
        {
            version = 1,
            t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
            hp = RoundData(averageGazePosition),
            hr = RoundData(averageHeadRotation),
            gaze = RoundData(averageGazeDirection),
            hit = RoundData(hitPoint),
            obj = hitObject,
            isValid = isValid,
            getSucceeded = getSucceeded,
            eyeStarted = _eyeStarted,
            xrInitialized = OVRPlugin.initialized,
            hmdPresent = OVRPlugin.hmdPresent,
            vrFocus = OVRPlugin.hasVrFocus,
            inputFocus = OVRPlugin.hasInputFocus,
            userPresent = OVRPlugin.userPresent,
            eyeSupported = OVRPlugin.eyeTrackingSupported,
            leftValid = leftValid,
            rightValid = rightValid,
            pluginTime = pluginTime,
            didHit = didHit,
            reason = reason
        };

        WriteRow(packet);
    }

    void SendEyeStatusPacket(bool isValid, bool getSucceeded, bool leftValid, bool rightValid, double pluginTime, bool didHit, string reason)
    {
        var packet = new EyePacket
        {
            version = 1,
            t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
            hp = "",
            hr = "",
            gaze = "",
            hit = "",
            obj = "",
            isValid = isValid,
            getSucceeded = getSucceeded,
            eyeStarted = _eyeStarted,
            xrInitialized = OVRPlugin.initialized,
            hmdPresent = OVRPlugin.hmdPresent,
            vrFocus = OVRPlugin.hasVrFocus,
            inputFocus = OVRPlugin.hasInputFocus,
            userPresent = OVRPlugin.userPresent,
            eyeSupported = OVRPlugin.eyeTrackingSupported,
            leftValid = leftValid,
            rightValid = rightValid,
            pluginTime = pluginTime,
            didHit = didHit,
            reason = reason
        };

        WriteRow(packet);
    }

    void WriteRow(EyePacket p)
    {
        if (_writer == null) return;

        var sb = new StringBuilder();
        var session = RecordingSession.Instance;
        double mapped = 0;
        bool hasMappedTime = session != null && p.getSucceeded &&
            session.EyeClock.TryMap(p.pluginTime, _observationSessionSeconds, out mapped);
        bool repeated = p.getSucceeded && p.pluginTime > 0 && p.pluginTime <= _lastSdkSampleTime;
        if (p.getSucceeded && p.pluginTime > 0) _lastSdkSampleTime = p.pluginTime;
        sb.Append(Csv(session?.SessionId)).Append(';');
        sb.Append(session != null ? RecordingCsv.Number(hasMappedTime ? mapped : _observationSessionSeconds) : "").Append(';');
        sb.Append(session != null ? RecordingCsv.Number(_observationSessionSeconds) : "").Append(';');
        sb.Append(hasMappedTime ? RecordingCsv.Number(mapped) : "").Append(';');
        sb.Append(hasMappedTime ? "sdk_mapped" : "observation_only").Append(';');
        sb.Append(hasMappedTime ? session.EyeClock.Revision.ToString(CultureInfo.InvariantCulture) : "").Append(';');
        sb.Append(hasMappedTime ? RecordingCsv.Number(session.EyeClock.HalfWidth) : "").Append(';');
        sb.Append(hasMappedTime ? RecordingCsv.Number(_observationSessionSeconds - mapped) : "").Append(';');
        sb.Append(repeated).Append(';');
        sb.Append(Time.frameCount).Append(';');
        sb.Append(p.version).Append(';');
        sb.Append(p.t).Append(';');
        sb.Append(Csv(p.hp)).Append(';');
        sb.Append(Csv(p.hr)).Append(';');
        sb.Append(Csv(p.gaze)).Append(';');
        sb.Append(Csv(p.hit)).Append(';');
        sb.Append(Csv(p.obj)).Append(';');
        sb.Append(p.isValid).Append(';');
        sb.Append(p.getSucceeded).Append(';');
        sb.Append(p.eyeStarted).Append(';');
        sb.Append(p.xrInitialized).Append(';');
        sb.Append(p.hmdPresent).Append(';');
        sb.Append(p.vrFocus).Append(';');
        sb.Append(p.inputFocus).Append(';');
        sb.Append(p.userPresent).Append(';');
        sb.Append(p.eyeSupported).Append(';');
        sb.Append(p.leftValid).Append(';');
        sb.Append(p.rightValid).Append(';');
        sb.Append(RecordingCsv.Number(p.pluginTime)).Append(';');
        sb.Append(p.didHit).Append(';');
        sb.Append(Csv(p.reason));

        _writer.WriteLine(sb.ToString());
    }

    // Wraps a field in quotes and escapes any embedded quotes, since obj/reason
    // are free text and could in principle contain commas or quotes.
    string Csv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    void MaybeFlush()
    {
        if (_writer == null) return;

        if (_lastFlushAt < 0f || Time.realtimeSinceStartup - _lastFlushAt >= flushIntervalSeconds)
        {
            _writer.Flush();
            _lastFlushAt = Time.realtimeSinceStartup;
        }
    }

    void ResetStableData()
    {
        _firstValidPacketAt = -1f;
        _validPacketCount = 0;
        HasStableData = false;
    }

    void MarkValidPacket()
    {
        _validPacketCount++;

        if (_firstValidPacketAt < 0f)
        {
            _firstValidPacketAt = Time.realtimeSinceStartup;
            Debug.Log("EYE first valid packet sent");
            return;
        }

        if (!HasStableData && Time.realtimeSinceStartup - _firstValidPacketAt >= readyAfterSeconds)
        {
            HasStableData = true;
            Debug.Log($"EYE OK | sent {readyAfterSeconds:0.0}s of valid data ({_validPacketCount} packets)");
        }
    }

    string RoundData(Vector3 value)
    {
        return "[" +
               OptimizeValue(value.x) + ";" +
               OptimizeValue(value.y) + ";" +
               OptimizeValue(value.z) + "]";
    }

    string OptimizeValue(float value)
    {
        if (Mathf.Abs(value) < 0.000005f)
            return "0";

        return value.ToString("F5", CultureInfo.InvariantCulture);
    }

    void CloseLogFile()
    {
        if (_writer == null) return;

        try
        {
            _writer.Flush();
            _writer.Close();
        }
        catch { }
        finally
        {
            _writer = null;
        }
    }

    void OnApplicationPause(bool pause)
    {
        _lastSdkSampleTime = double.NaN;
        // Quest apps can be suspended without OnDestroy ever firing,
        // so flush whenever the app loses focus.
        if (pause && _writer != null)
        {
            _writer.Flush();
        }
    }

    void OnApplicationQuit()
    {
        CloseLogFile();
    }

    void OnDestroy()
    {
        CloseLogFile();
        OVRPlugin.StopEyeTracking();
    }

    void OnGUI()
    {
        if (_eyeStarted) return;

        int size = 16;
        GUI.color = Color.red;
        GUI.DrawTexture(new Rect(32, 10, size, size), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
