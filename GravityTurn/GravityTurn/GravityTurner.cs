using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;
using KSP.IO;
using System.IO;

namespace GravityTurn
{
    [KSPAddon(KSPAddon.Startup.Flight,false)]
    public class GravityTurner : MonoBehaviour
    {
        public static Vessel vessel { get { return FlightGlobals.ActiveVessel; } }
        protected Rect windowPos = new Rect(50, 100, 300, 200);
        protected Rect helpWindowPos;
        protected bool helpWindowOpen = false;
        protected Rect DebugWindowPos = new Rect(500, 100, 300, 300);
        protected string helpWindowText = "";
        protected string DebugText = "";
        ApplicationLauncherButton button;
        bool WindowVisible = false;

        [Persistent]
        public EditableValue StartSpeed = new EditableValue(100);
        [Persistent]
        public EditableValue HoldAPTime = new EditableValue(50);
        [Persistent]
        public EditableValue APTimeStart = new EditableValue(40);
        [Persistent]
        public EditableValue APTimeFinish = new EditableValue(40);
        [Persistent]
        public EditableValue TurnAngle = new EditableValue(10);
        [Persistent]
        public EditableValue Sensitivity = new EditableValue(0.2);
        [Persistent]
        public EditableValue Roll = new EditableValue(-90);
        [Persistent]
        public EditableValue DestinationHeight = new EditableValue(80);
        [Persistent]
        public EditableValue PressureCutoff = new EditableValue(2500);
        [Persistent]
        public EditableValue Inclination = new EditableValue(0);
        [Persistent]
        bool EnableStaging = true;

        float NeutralThrottle = 0.5f;
        double PrevTime = 0;
        double VelocityLost = 0;
        double DragLoss = 0;
        double GravityDragLoss = 0;
        double FlyTimeInterval = 0;
        double VectorLoss = 0;
        double TotalBurn = 0;
        public double HorizontalDistance = 0;
        public double MaxThrust = 0;
        MovingAverage Throttle = new MovingAverage(10, 1);
        private float lastTimeMeasured = 0.0f;
        public VesselState vesselState = null;
        double TimeSpeed = 0;
        AttitudeController attitude = null;
        StageController stage;
        bool InPitchProgram = false;
        bool PitchSet = false;
        StageStats stagestats = null;
        float PitchAdjustment = 0;
        string Message = "";
        MechjebWrapper mucore = new MechjebWrapper();
        bool Launching = false;
        double maxQ = 0;
        Vector3d RelativeVelocity;
        MovingAverage DragRatio = new MovingAverage();
        FlightMap flightmap = null;

#if DEBUG            
        public static void Log(string message, [CallerMemberName] string callingMethod = "",
         [CallerFilePath] string callingFilePath = "",
         [CallerLineNumber] int callingFileLineNumber = 0)
        {
            Debug.Log(string.Format("{0} - {1} - {2}: {3}", callingMethod, callingFilePath, callingFileLineNumber, message));
#else
        public static void Log(string message)
        {

#endif
        }


        string ConfigFilename()
        {
            return IOUtils.GetFilePathFor(this.GetType(), string.Format("gt_vessel_{0}_{1}.cfg",vessel.id.ToString(),vessel.mainBody.name));
        }


        void Start()
        {
            mucore.init();
            vesselState = new VesselState();
            attitude = new AttitudeController(this);
            stage = new StageController(this);
            attitude.OnStart();
            RenderingManager.AddToPostDrawQueue(3, new Callback(drawGUI));//start the GUI
            helpWindowPos = new Rect(windowPos.x+windowPos.width, windowPos.y, 0, 0);
            stagestats = new StageStats();
            stagestats.editorBody = vessel.mainBody;
            stagestats.OnModuleEnabled();
            stagestats.OnFixedUpdate();
            stagestats.RequestUpdate(this);
            stagestats.OnFixedUpdate();
            CreateButtonIcon();
        }
        private void SetWindowOpen()
        {
            LoadParameters();
            vessel.OnFlyByWire += new FlightInputCallback(fly);
            WindowVisible = true;
            InitializeNumbers();
        }

        void InitializeNumbers()
        {
            NeutralThrottle = 0.5f;
            PrevTime = 0;
            VelocityLost = 0;
            DragLoss = 0;
            GravityDragLoss = 0;
            FlyTimeInterval = Time.time;
            maxQ = 0;
            Message = "";
            VectorLoss = 0;
            HorizontalDistance = 0;
            MaxThrust = GetMaxThrust();
            flightmap = new FlightMap(this);
        }

        private void CreateButtonIcon()
        {
            button = ApplicationLauncher.Instance.AddModApplication(
                SetWindowOpen,
                () => WindowVisible = false,
                null,
                null,
                null,
                null,
                ApplicationLauncher.AppScenes.FLIGHT,
                GameDatabase.Instance.GetTexture("GravityTurn/Textures/icon", false)
                );
        }

        bool StageHasSolidEngine(int inverseStage)
        {
            foreach (Part p in vessel.parts)
            {
                if (p.inverseStage == inverseStage)
                {
                    foreach (ModuleEngines e in p.FindModulesImplementing<ModuleEngines>())
                    {
                        if (e.engineType == EngineType.SolidBooster)
                            return true;
                    }
                }
            }
            return false;
        }

        double TWRWeightedAverage(double MinimumDeltaV)
        {
            stagestats.RequestUpdate(this);
            double TWR = 0;
            double deltav = 0;
            for (int i = stagestats.atmoStats.Length - 1; i >= 0; i--)
            {
                double stagetwr = (stagestats.atmoStats[i].StartTWR(vessel.mainBody.GeeASL) + stagestats.atmoStats[i].MaxTWR(vessel.mainBody.GeeASL))/2;
                if (stagetwr > 0)
                {
                    TWR += stagetwr * stagestats.atmoStats[i].deltaV;
                    deltav += stagestats.atmoStats[i].deltaV;
                    if (deltav >= MinimumDeltaV)
                        break;
                }
            }
            return TWR / deltav;
        }

        void CalculateSettings()
        {
            stagestats.RequestUpdate(this);
            double TWR = 0;
            for (int i = stagestats.atmoStats.Length - 1; i >= 0; i--)
            {
                double stagetwr = stagestats.atmoStats[i].StartTWR(vessel.mainBody.GeeASL);
                if (stagetwr > 0)
                {
                    if (StageHasSolidEngine(i))
                        TWR = (stagetwr + stagestats.atmoStats[i].MaxTWR(vessel.mainBody.GeeASL))/2.3;
                    else
                        TWR = stagetwr;
                    break;
                }
            }
            if (TWR > 1.2)
            {
                TWR -= 1.2;
                TurnAngle = Mathf.Clamp((float)(10 + TWR * 9), 10, 80);
                StartSpeed = Mathf.Clamp((float)(100 - TWR * 45), 10, 100);
            }
            APTimeFinish = 40;
            APTimeStart = 40;
            Sensitivity = 0.2;
            if (vessel.mainBody.atmosphereDepth > 0)
                DestinationHeight = vessel.mainBody.atmosphereDepth + 10000;
            else
                DestinationHeight = vessel.mainBody.timeWarpAltitudeLimits[1] + 10000;
            DestinationHeight /= 1000;
            Roll = -90;
            Inclination = 0;
            PressureCutoff = 2500;
            SaveParameters();
        }

        void helpButton(string helpMessage)
        {
            if (GUILayout.Button("?", GUILayout.ExpandWidth(false),GUILayout.ExpandHeight(false),GUILayout.Height(16)))
            {
                helpWindowOpen = true;
                helpWindowText = helpMessage;
            }
        }
        void HelpWindowGUI(int windowID)
        {
            GUIStyle mySty = new GUIStyle(GUI.skin.button);
            mySty.normal.textColor = mySty.focused.textColor = Color.white;
            mySty.hover.textColor = mySty.active.textColor = Color.yellow;
            mySty.onNormal.textColor = mySty.onFocused.textColor = mySty.onHover.textColor = mySty.onActive.textColor = Color.green;
            mySty.padding = new RectOffset(8, 8, 8, 8);
            if (GUI.Button(new Rect(helpWindowPos.width - 18, 2, 16, 16), "X"))
            {
                helpWindowOpen = false;
            }
            GUILayout.BeginVertical();
            GUILayout.TextArea(helpWindowText);
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));

        }

        void ItemLabel(string labelText)
        {
            GUILayout.Label(labelText, GUILayout.ExpandWidth(false), GUILayout.Width(windowPos.width / 2));
        }

        private void DebugGUI(int windowID)
        {
            GUILayout.Box(PreflightInfo());
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
            ItemLabel("Start m/s");
            StartSpeed.setValue(GUILayout.TextField(string.Format("{0:0.0}",StartSpeed),GUILayout.Width(60)));
            helpButton("At this speed, pitch to Turn Angle to begin the gravity turn.  Stronger rockets and extremely aerodynamically stable rockets should do this earlier.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Turn Angle");
            TurnAngle.setValue(GUILayout.TextField(string.Format("{0:0.0}", TurnAngle), GUILayout.Width(60)));
            helpButton("Angle to start turn at Start Speed.  Higher values may cause aerodynamic stress.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Hold AP Time Start");
            APTimeStart.setValue(GUILayout.TextField(APTimeStart.ToString(), GUILayout.Width(60)));
            helpButton("Starting value for Time To Prograde.  Higher values will make a steeper climb.  Steeper climbs are usually worse.  Lower values may cause overheating or death.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Hold AP Time Finish");
            APTimeFinish.setValue(GUILayout.TextField(APTimeFinish.ToString(), GUILayout.Width(60)));
            helpButton("AP Time will fade to this value, to vary the steepness of the ascent during the ascent.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Sensitivity");
            Sensitivity.setValue(GUILayout.TextField(Sensitivity.ToString(), GUILayout.Width(60)));
            helpButton("Will not throttle below this value.  Mostly a factor at the end of ascent.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Destination Height (km)");
            DestinationHeight.setValue(GUILayout.TextField(DestinationHeight.ToString(), GUILayout.Width(60)));
            helpButton("Desired Apoapsis.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Roll");
            Roll.setValue(GUILayout.TextField(Roll.ToString(), GUILayout.Width(60)));
            helpButton("If you want a particular side of your ship to face downwards.  Shouldn't matter for most ships.  May cause mild nausea.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Inclination");
            Inclination.setValue(GUILayout.TextField(Inclination.ToString(), GUILayout.Width(60)));
            helpButton("Desired orbit inclination.  Any non-zero value WILL make your launch less efficient. Final inclination will also not be perfect.  Sorry about that, predicting coriolis is hard.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Pressure Cutoff");
            PressureCutoff.setValue(GUILayout.TextField(PressureCutoff.ToString(), GUILayout.Width(60)));
            helpButton("Dynamic pressure where we change from Surface to Orbital velocity tracking\nThis will be a balance point between aerodynamic drag in the upper atmosphere vs. thrust vector loss.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            EnableStaging = GUILayout.Toggle(EnableStaging,"Auto-Staging");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            flightmap.visible = GUILayout.Toggle(flightmap.visible, "Show Launch Map");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("Time to match: {0:0.0}",HoldAPTime), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            if (vessel.Landed && !Launching && GUILayout.Button("Best Guess Settings", GUILayout.ExpandWidth(false)))
                CalculateSettings();
            if (vessel.Landed && !Launching && GUILayout.Button("Launch!"))
            {
                Launch();
            }
            if (Launching && GUILayout.Button("Abort!"))
            {
                Kill();
            }
            GUILayout.Label(Message);
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 2000));
            double StopHeight = vessel.mainBody.atmosphereDepth;
            if (StopHeight <= 0)
                StopHeight = DestinationHeight*1000;
            HoldAPTime = APTimeStart + ((float)vessel.altitude / (float)StopHeight * (APTimeFinish - APTimeStart));
            if (HoldAPTime > Math.Max(APTimeFinish, APTimeStart))
                HoldAPTime = Math.Max(APTimeFinish, APTimeStart);
            if (HoldAPTime < Math.Min(APTimeFinish, APTimeStart))
                HoldAPTime = Math.Min(APTimeFinish, APTimeStart);
        }

        void Launch()
        {
            if (Staging.CurrentStage == Staging.StageCount)
                Staging.ActivateNextStage();
            InitializeNumbers();
            Launching = true;
            SaveParameters();
        }

        double GetMaxThrust()
        {
            double thrust = 0;
            FuelFlowSimulation.Stats[] stats;
            if (vessel.mainBody.atmosphere && vessel.altitude < vessel.mainBody.atmosphereDepth)
                stats = stagestats.atmoStats;
            else
                stats = stagestats.vacStats;
            for (int i = stats.Length - 1; i >= 0; i--)
            {
                if (stats[i].startThrust > thrust)
                    thrust = stats[i].startThrust;
            }
            return thrust;
        }



        void FixedUpdate()
        {
            stagestats.editorBody = vessel.mainBody;
            vesselState.Update(vessel);
            attitude.OnFixedUpdate();
            stagestats.OnFixedUpdate();
            stagestats.RequestUpdate(this);
            if (flightmap != null && Launching)
                flightmap.UpdateMap(vessel);
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
                if (helpWindowOpen)
                    helpWindowPos = GUILayout.Window(2, helpWindowPos, HelpWindowGUI, "GravityTurn Help", GUILayout.MinWidth(300));
#if DEBUG
                DebugWindowPos = GUILayout.Window(3, DebugWindowPos, DebugGUI, "Debug");
#endif
                if (flightmap != null && flightmap.visible)
                    flightmap.windowPos = GUILayout.Window(4, flightmap.windowPos, flightmap.WindowGUI, "FlightMap");
            }

        }

        private float MaxAngle()
        {
            float angle = 100000 / (float)vesselState.dynamicPressure;
            if (angle > 35)
                return 35;
            return angle;
        }

        private float APThrottle(double timeToAP)
        {
            if (vessel.speed < StartSpeed)
                Throttle.value = 1.0f;
            else if (timeToAP > vessel.orbit.timeToPe) // We're falling
                Throttle.force(1.0f);
            else
            {
                if (timeToAP > vessel.orbit.timeToPe) // We're falling
                    timeToAP = 0;
                float diff = 0.1f * (float)Math.Abs(HoldAPTime - timeToAP) * Sensitivity;
                TimeSpeed = (PrevTime - timeToAP) / (Time.time - lastTimeMeasured);
                if (Math.Abs(TimeSpeed) < 0.02 && PitchAdjustment==0)
                    NeutralThrottle = (float)Throttle.value;
                if (Math.Abs(timeToAP - HoldAPTime) < 0.1)
                {
                    if (PitchAdjustment > 0)
                        PitchAdjustment -= 0.1f;
                    else
                        Throttle.force(NeutralThrottle);
                }
                else if (timeToAP < HoldAPTime)
                {
                    if (Throttle.value >= 1 && timeToAP < PrevTime)
                    {
                        NeutralThrottle = 1;
                        if (vessel.Pitch() - PitchAdjustment - 0.1f >= -89)
                            PitchAdjustment += 0.1f;
                    }
                    Throttle.value += diff;
                }
                else if (timeToAP > HoldAPTime)
                {
                    if (PitchAdjustment > 0)
                        PitchAdjustment -= 0.1f;
                    else
                        Throttle.value -= diff;
                }
            }
            if (PitchAdjustment < 0)
                PitchAdjustment = 0;
            if (PitchAdjustment > MaxAngle())
                PitchAdjustment = MaxAngle();
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
            Launching = false;
            vessel.OnFlyByWire -= new FlightInputCallback(fly);
            //WindowVisible = false;
            FlightInputHandler.state.mainThrottle = 0;
            attitude.enabled = false;
            vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
        }

        private float LaunchHeading()
        {
            return (float)MuUtils.HeadingForLaunchInclination(vessel.mainBody, Inclination, vessel.latitude, vesselState.orbitalVelocity.magnitude);
        }

        private float ProgradeHeading(bool surface = true)
        {
            Quaternion current;
            if (surface)
                current = Quaternion.LookRotation(vesselState.surfaceVelocity.normalized, vesselState.up) * Quaternion.Euler(0, 0, Roll);
            else
                current = Quaternion.LookRotation(vesselState.orbitalVelocity.normalized, vesselState.up) * Quaternion.Euler(0, 0, Roll);
            //current *= vesselState.rotationSurface.Inverse();
            return (float)Vector3d.Angle(Vector3d.Exclude(vesselState.up, vesselState.surfaceVelocity), vesselState.north);
        }


        Quaternion RollRotation()
        {
            return Quaternion.AngleAxis(Roll, Vector3.forward);
        }

        private void fly(FlightCtrlState s)
        {
            if (!Launching)
                return;
            if (!WindowVisible) 
                Kill();
            else if (vessel.orbit.ApA > DestinationHeight*1000 && vessel.altitude > vessel.mainBody.atmosphereDepth)
            {
                if (TimeWarp.CurrentRateIndex > 0)
                    TimeWarp.SetRate(0, true);
                if (mucore.Initialized)
                    mucore.CircularizeAtAP();
                Kill();
            }
            else
            {
                if (maxQ < vesselState.dynamicPressure)
                    maxQ = vesselState.dynamicPressure;
                if (EnableStaging)
                    stage.Update();
                if (vessel.orbit.ApA < DestinationHeight * 1000)
                    s.mainThrottle = APThrottle(vessel.orbit.timeToAp);
                else
                    s.mainThrottle = 0;
                RelativeVelocity = vesselState.surfaceVelocity;
                if (InPitchProgram && PitchSet)
                {
                    if (vessel.ProgradePitch()+90 >= TurnAngle)
                        InPitchProgram = false;
                }
                if (vessel.speed < StartSpeed)
                {
                    InPitchProgram = true;
                    attitude.attitudeTo(Quaternion.Euler(-90, LaunchHeading(), 0) * RollRotation(), AttitudeReference.SURFACE_NORTH, this);
                }
                else if (InPitchProgram)
                {
                    attitude.attitudeTo(Quaternion.Euler(-90 + TurnAngle, LaunchHeading(), 0) * RollRotation(), AttitudeReference.SURFACE_NORTH, this);
                    PitchSet = true;
                }
                else if (vesselState.dynamicPressure > maxQ * 0.5 || vesselState.dynamicPressure > PressureCutoff)
                { // Still ascending, or not yet below the cutoff pressure
                    attitude.attitudeTo(Quaternion.Euler(vessel.ProgradePitch() - PitchAdjustment, LaunchHeading(), 0) * RollRotation(), AttitudeReference.SURFACE_NORTH, this);
                }
                else
                {
                    //attitude.attitudeTo(Quaternion.Euler(ProgradePitch(false) - PitchAdjustment, LaunchHeading(), 0) * RollRotation(), AttitudeReference.SURFACE_NORTH, this);
                    attitude.attitudeTo(Quaternion.Euler(0 - PitchAdjustment, 0, Roll), AttitudeReference.ORBIT, this);
                    RelativeVelocity = vesselState.orbitalVelocity;
                }
                attitude.enabled = true;
                attitude.Drive(s);
                CalculateLosses();
            }
        }

        string PreflightInfo()
        {
            string info;
            info = string.Format("Drag area {0:0.00}", vesselState.areaDrag);
            info += string.Format("\nDrag coefficient {0:0.00}", vesselState.dragCoef);
            info += string.Format("\nMass {0:0.00}", vesselState.mass);
            DragRatio.value = vesselState.areaDrag/vesselState.mass;
            info += string.Format("\narea/mass {0:0.00}", DragRatio.value);
            info += string.Format("\nGuess TWR {0:0.00}", TWRWeightedAverage(2 * vessel.mainBody.GeeASL * DestinationHeight));
            return info;
        }

        void CalculateLosses()
        {
            double fwdAcceleration = Vector3d.Dot(vessel.acceleration, vesselState.forward.normalized);
            double GravityDrag = Vector3d.Dot(vesselState.gravityForce, -vessel.obt_velocity.normalized);
            double TimeInterval = Time.time - FlyTimeInterval;
            FlyTimeInterval = Time.time;
            HorizontalDistance += Vector3d.Exclude(vesselState.up, vesselState.orbitalVelocity).magnitude * TimeInterval;
            VelocityLost += ((vesselState.thrustCurrent / vesselState.mass) - fwdAcceleration) * TimeInterval;
            DragLoss += vesselState.drag * TimeInterval;
            GravityDragLoss += GravityDrag * TimeInterval;
            double VectorDrag = vesselState.thrustCurrent - Vector3d.Dot(vesselState.thrustVectorLastFrame, vessel.obt_velocity.normalized);
            VectorDrag = VectorDrag / vesselState.mass;
            VectorLoss += VectorDrag * TimeInterval;
            TotalBurn += vesselState.thrustCurrent / vesselState.mass * TimeInterval;
            double GravityDragLossAtAp = GravityDragLoss + vessel.obt_velocity.magnitude - vessel.orbit.getOrbitalVelocityAtUT(vessel.orbit.timeToAp + Planetarium.GetUniversalTime()).magnitude;
            Message = string.Format(
@"Air Drag: {0:0.00}m/s²
GravityDrag: {1:0.00}m/s²
Thrust Vector Drag: {5:0.00}m/s²
Air Drag Loss: {2:0.00}m/s
Gravity Drag Loss: {3:0.00} -> {4:0.00}m/s (at AP)
Total Vector Loss: {6:0.00}m/s
Total Loss: {7:0.00}m/s
Total Burn: {8:0.0}",
                vesselState.drag,
                GravityDrag,
                DragLoss,
                GravityDragLoss, GravityDragLossAtAp,
                VectorDrag,
                VectorLoss,
                DragLoss + GravityDragLossAtAp + VectorLoss,
                TotalBurn
                );
        }

        void LoadParameters()
        {
            ConfigNode savenode;
            if (vessel.mainBody.atmosphereDepth > 0)
                DestinationHeight = vessel.mainBody.atmosphereDepth + 10000;
            else
                DestinationHeight = vessel.mainBody.timeWarpAltitudeLimits[1] + 10000;
            DestinationHeight /= 1000;
            try
            {
                savenode = ConfigNode.Load(ConfigFilename());
                if (savenode != null)
                {
                    if (ConfigNode.LoadObjectFromConfig(this, savenode))
                        Debug.Log("GravityTurn loaded from " + ConfigFilename());
                    else
                        Debug.Log("GravityTurn NOT loaded from " + ConfigFilename());
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("GravityTurn Load error " + ex.GetType());
            }
        }

        void SaveParameters()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilename()));
            ConfigNode savenode = ConfigNode.CreateConfigFromObject(this);
            savenode.Save(ConfigFilename());
            Debug.Log("GravityTurn saved to " + ConfigFilename());
        }

        void OnDestroy()
        {
            Kill();
            ApplicationLauncher.Instance.RemoveModApplication(button);
            //SaveParameters();
        }

    }
}
