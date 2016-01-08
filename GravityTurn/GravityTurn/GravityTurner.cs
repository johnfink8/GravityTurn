using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GravityTurn
{
    [KSPAddon(KSPAddon.Startup.Flight,false)]
    public class GravityTurner : MonoBehaviour
    {
        public static Vessel vessel { get { return FlightGlobals.ActiveVessel; } }
        protected Rect windowPos = new Rect(50, 100, 300, 200);
        ApplicationLauncherButton button;
        bool WindowVisible = false;
        float StartSpeed = 100f;
        float HoldAPTime = 50f;
        float APTimeStart = 40f;
        float APTimeFinish = 40f;
        float TurnAngle = 10f;
        float NeutralThrottle = 0.5f;
        double PrevTime = 0;
        MovingAverage Throttle = new MovingAverage(10, 1);
        float Sensitivity = 0.1f;
        float Roll = -90f;
        double DestinationHeight = 80;
        private float lastTimeMeasured = 0.0f;
        public VesselState vesselState = null;
        double TimeSpeed = 0;
        AttitudeController attitude = null;
        StageController stage;

        public static void Log(string message, [CallerMemberName] string callingMethod = "",
         [CallerFilePath] string callingFilePath = "",
         [CallerLineNumber] int callingFileLineNumber = 0)
        {
#if DEBUG            
            Debug.Log(string.Format("{0} - {1} - {2}: {3}", callingMethod, callingFilePath, callingFileLineNumber, message));
#endif
        }


        void Start()
        {
            vesselState = new VesselState();
            attitude = new AttitudeController(this);
            stage = new StageController(this);
            attitude.OnStart();
            RenderingManager.AddToPostDrawQueue(3, new Callback(drawGUI));//start the GUI
            CreateButtonIcon();
        }
        private void SetWindowOpen()
        {
            vessel.OnFlyByWire += new FlightInputCallback(fly);
            WindowVisible = true;
            vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
        }

        private void CreateButtonIcon()
        {
            button = ApplicationLauncher.Instance.AddModApplication(
                SetWindowOpen,
                Kill,
                null,
                null,
                null,
                null,
                ApplicationLauncher.AppScenes.FLIGHT,
                GameDatabase.Instance.GetTexture("GravityTurn/Textures/icon", false)
                );
        }
        private void WindowGUI(int windowID)
        {
            GUIStyle mySty = new GUIStyle(GUI.skin.button);
            mySty.normal.textColor = mySty.focused.textColor = Color.white;
            mySty.hover.textColor = mySty.active.textColor = Color.yellow;
            mySty.onNormal.textColor = mySty.onFocused.textColor = mySty.onHover.textColor = mySty.onActive.textColor = Color.green;
            mySty.padding = new RectOffset(8, 8, 8, 8);
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Start m/s",GUILayout.ExpandWidth(false));
            StartSpeed = float.Parse(GUILayout.TextField(StartSpeed.ToString(),GUILayout.Width(40)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Hold AP Time Start", GUILayout.ExpandWidth(false));
            APTimeStart = float.Parse(GUILayout.TextField(APTimeStart.ToString(), GUILayout.Width(40)));
            GUILayout.Label("seconds", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Hold AP Time Finish", GUILayout.ExpandWidth(false));
            APTimeFinish = float.Parse(GUILayout.TextField(APTimeFinish.ToString(), GUILayout.Width(40)));
            GUILayout.Label("seconds", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Sensitivity", GUILayout.ExpandWidth(false));
            Sensitivity = float.Parse(GUILayout.TextField(Sensitivity.ToString(), GUILayout.Width(40)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Turn Angle", GUILayout.ExpandWidth(false));
            TurnAngle = float.Parse(GUILayout.TextField(TurnAngle.ToString(), GUILayout.Width(40)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Destination Height", GUILayout.ExpandWidth(false));
            DestinationHeight = double.Parse(GUILayout.TextField(DestinationHeight.ToString(), GUILayout.Width(40)));
            GUILayout.Label("km", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Roll", GUILayout.ExpandWidth(false));
            Roll = float.Parse(GUILayout.TextField(Roll.ToString(), GUILayout.Width(40)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("Time to match: {0:0.0}",HoldAPTime), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            if (Staging.CurrentStage == Staging.StageCount && GUILayout.Button("Launch!"))
                Staging.ActivateNextStage();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
            HoldAPTime = APTimeStart + ((float)vessel.altitude / (float)vessel.mainBody.atmosphereDepth * (APTimeFinish - APTimeStart));
            if (HoldAPTime > Math.Max(APTimeFinish, APTimeStart))
                HoldAPTime = Math.Max(APTimeFinish, APTimeStart);
            if (HoldAPTime < Math.Min(APTimeFinish, APTimeStart))
                HoldAPTime = Math.Min(APTimeFinish, APTimeStart);
        }

        void FixedUpdate()
        {
            vesselState.Update(vessel);
            attitude.OnFixedUpdate();
        }

        void Update()
        {
            attitude.OnUpdate();
        }

        private void drawGUI()
        {
            if (WindowVisible)
            {
                GUI.skin = HighLogic.Skin;
                windowPos = GUILayout.Window(1, windowPos, WindowGUI, "GravityTurn", GUILayout.MinWidth(300));
            }
        }

        private float APThrottle(double timeToAP)
        {
            if (vessel.speed < StartSpeed)
                Throttle.value = 1.0f;
            else
            {
                float diff = 0.1f * (float)Math.Abs(HoldAPTime - timeToAP) * Sensitivity;
                TimeSpeed = (PrevTime - timeToAP) / (Time.time - lastTimeMeasured);
                if (Math.Abs(TimeSpeed) < 0.02)
                    NeutralThrottle = (float)Throttle.value;
                if (Math.Abs(timeToAP - HoldAPTime) < 0.1)
                    Throttle.force( NeutralThrottle);
                else if (timeToAP < HoldAPTime)
                    Throttle.value += diff;
                else if (timeToAP > HoldAPTime)
                    Throttle.value -= diff;
            }
            PrevTime = vessel.orbit.timeToAp;
            lastTimeMeasured = Time.time;
            if (Throttle.value < Sensitivity)
                Throttle.force(Sensitivity);
            if (Throttle.value > 1)
                Throttle.force(1);
            return (float)Throttle.value;
        }

        private void Kill()
        {
            vessel.OnFlyByWire -= new FlightInputCallback(fly);
            WindowVisible = false;
            FlightInputHandler.state.mainThrottle = 0;
            attitude.enabled = false;
            vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
        }

        private void fly(FlightCtrlState s)
        {
            if (!WindowVisible || vessel.orbit.ApA >= DestinationHeight * 1000)
                Kill();
            else
            {
                stage.Update();
                s.mainThrottle = APThrottle(vessel.orbit.timeToAp);
                if (vessel.speed < StartSpeed)
                {
                    attitude.attitudeTo(Quaternion.Euler(-90, 90, Roll), AttitudeReference.SURFACE_NORTH, this);
                }
                else if (vessel.speed < StartSpeed + StartSpeed * 1.5)
                {
                    attitude.attitudeTo(Quaternion.Euler(-90+TurnAngle, 90, Roll), AttitudeReference.SURFACE_NORTH, this);
                }
                else if (vessel.altitude < vessel.mainBody.atmosphereDepth * 0.5)
                {
                    attitude.attitudeTo(Quaternion.Euler(0, 0, Roll), AttitudeReference.SURFACE_VELOCITY, this);
                }
                else
                {
                    attitude.attitudeTo(Quaternion.Euler(0, 0, Roll), AttitudeReference.ORBIT, this);
                }
                attitude.enabled = true;
                attitude.Drive(s);
            }
        }

        void OnDestroy()
        {
            Kill();
            ApplicationLauncher.Instance.RemoveModApplication(button);
        }

    }
}
