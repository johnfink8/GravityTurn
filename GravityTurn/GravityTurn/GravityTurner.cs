using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using UnityEngine;
using KSP.IO;
using System.IO;
using System.Diagnostics;

namespace GravityTurn
{
    [KSPAddon(KSPAddon.Startup.Flight,false)]
    public class GravityTurner : MonoBehaviour
    {
        public static Vessel getVessel { get { return FlightGlobals.ActiveVessel; } }
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
        MovingAverage PitchAdjustment = new MovingAverage(4, 0);
        string Message = "";
        MechjebWrapper mucore = new MechjebWrapper();
        bool Launching = false;
        double maxQ = 0;
        Vector3d RelativeVelocity;
        MovingAverage DragRatio = new MovingAverage();
        FlightMap flightmap = null;
        string LaunchName = "";
        CelestialBody LaunchBody = null;
        //LaunchSimulator simulator = new LaunchSimulator();

        public static void Log(string format, params object[] args)
        {
            string method = "";
#if DEBUG
            StackFrame stackFrame = new StackFrame(1, true);
            method = string.Format(" [{0}]|{1}",stackFrame.GetMethod().ToString(),stackFrame.GetFileLineNumber());
#endif
            string incomingMessage;
            if (args == null)
                incomingMessage = format;
            else
                incomingMessage = string.Format(format,args);
            UnityEngine.Debug.Log(string.Format("GravityTurn{0} : {1}",method,incomingMessage));
        }


        string ConfigFilename(Vessel vessel)
        {
            return IOUtils.GetFilePathFor(this.GetType(), string.Format("gt_vessel_{0}_{1}.cfg",vessel.id.ToString(),vessel.mainBody.name));
        }


        void Start()
        {
            try
            {
                mucore.init();
                vesselState = new VesselState();
                attitude = new AttitudeController(this);
                stage = new StageController(this);
                attitude.OnStart();
                RenderingManager.AddToPostDrawQueue(3, new Callback(drawGUI));//start the GUI
                helpWindowPos = new Rect(windowPos.x + windowPos.width, windowPos.y, 0, 0);
                stagestats = new StageStats();
                stagestats.editorBody = getVessel.mainBody;
                stagestats.OnModuleEnabled();
                stagestats.OnFixedUpdate();
                stagestats.RequestUpdate(this);
                stagestats.OnFixedUpdate();
                CreateButtonIcon();
                LaunchName = new string(getVessel.vesselName.ToCharArray());
                LaunchBody = getVessel.mainBody;
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
        }
        private void SetWindowOpen()
        {
            WindowVisible = true;
            if (!Launching)
            {
                LoadParameters();
                InitializeNumbers(getVessel);
            }
        }

        void InitializeNumbers(Vessel vessel)
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
            MaxThrust = GetMaxThrust(vessel);
            bool openFlightmap = false;
            if (flightmap != null)
                openFlightmap = flightmap.visible;
            flightmap = new FlightMap(this);
            flightmap.visible = openFlightmap;
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


        double TWRWeightedAverage(double MinimumDeltaV, Vessel vessel)
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

        void CalculateSettings(Vessel vessel)
        {
            stagestats.RequestUpdate(this);
            double TWR = 0;
            for (int i = stagestats.atmoStats.Length - 1; i >= 0; i--)
            {
                double stagetwr = stagestats.atmoStats[i].StartTWR(vessel.mainBody.GeeASL);
                if (stagetwr > 0)
                {
                    if (vessel.StageHasSolidEngine(i))
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
            FlightMap previousLaunch = FlightMap.Load(GetFlightMapFilename(), this);
            if (previousLaunch != null)
            {

                if (previousLaunch.MaxHeat() > 0.95)
                {
                    Log("Previous launch had high heat, reducing aggressiveness");
                    TurnAngle = previousLaunch.TurnAngle() * 0.95;
                    StartSpeed = previousLaunch.StartSpeed()* 1.05 ;
                }
                else if (previousLaunch.MaxHeat() < 0.9)
                {
                    float Adjust = Mathf.Clamp(previousLaunch.MaxHeat() + (1-previousLaunch.MaxHeat())/2, 0.8f, 0.95f);
                    Log("Previous launch seems high, increasing aggressiveness by {0:0.0}%",(1-Adjust)*100);
                    TurnAngle = previousLaunch.TurnAngle() / Adjust;
                    StartSpeed = previousLaunch.StartSpeed() * Adjust;
                }
                else
                {
                    Log("Previous launch seems about right");
                    TurnAngle = previousLaunch.TurnAngle();
                    StartSpeed = previousLaunch.StartSpeed();
                }
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
            GUILayout.Box(PreflightInfo(getVessel));
            GUI.DragWindow(new Rect(0, 0, 10000, 2000));
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
            if (getVessel.Landed && !Launching && GUILayout.Button("Best Guess Settings", GUILayout.ExpandWidth(false)))
                CalculateSettings(getVessel);
            if (getVessel.Landed && !Launching && GUILayout.Button("Launch!"))
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
            double StopHeight = getVessel.mainBody.atmosphereDepth;
            if (StopHeight <= 0)
                StopHeight = DestinationHeight*1000;
            HoldAPTime = APTimeStart + ((float)getVessel.altitude / (float)StopHeight * (APTimeFinish - APTimeStart));
            if (HoldAPTime > Math.Max(APTimeFinish, APTimeStart))
                HoldAPTime = Math.Max(APTimeFinish, APTimeStart);
            if (HoldAPTime < Math.Min(APTimeFinish, APTimeStart))
                HoldAPTime = Math.Min(APTimeFinish, APTimeStart);
        }

        void Launch()
        {
            if (Staging.CurrentStage == Staging.StageCount)
                Staging.ActivateNextStage();
            InitializeNumbers(getVessel);
            getVessel.OnFlyByWire += new FlightInputCallback(fly);
            Launching = true;
            SaveParameters();
            LaunchName = new string(getVessel.vesselName.ToCharArray());
            LaunchBody = getVessel.mainBody;
        }

        double GetMaxThrust(Vessel vessel)
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
            stagestats.editorBody = getVessel.mainBody;
            vesselState.Update(getVessel);
            attitude.OnFixedUpdate();
            stagestats.OnFixedUpdate();
            stagestats.RequestUpdate(this);
            if (flightmap != null && Launching)
                flightmap.UpdateMap(getVessel);
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
                windowPos = GUILayout.Window(763246, windowPos, WindowGUI, "GravityTurn", GUILayout.MinWidth(300));
                if (helpWindowOpen)
                    helpWindowPos = GUILayout.Window(1353634, helpWindowPos, HelpWindowGUI, "GravityTurn Help", GUILayout.MinWidth(300));
#if DEBUG
                DebugWindowPos = GUILayout.Window(875743, DebugWindowPos, DebugGUI, "Debug");
#endif
                if (flightmap != null && flightmap.visible)
                    flightmap.windowPos = GUILayout.Window(9064452, flightmap.windowPos, flightmap.WindowGUI, "FlightMap");
            }

        }

        private float MaxAngle(Vessel vessel)
        {
            float angle = 100000 / (float)vesselState.dynamicPressure;
            float vertical = 90 + vessel.Pitch();
            angle = Mathf.Clamp(angle, 0, 35);
            if (angle > vertical)
                return vertical;
            return angle;
        }

        private float APThrottle(double timeToAP, Vessel vessel)
        {
            if (vessel.speed < StartSpeed)
                Throttle.value = 1.0f;
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
                        PitchAdjustment.value -= 0.1f;
                    else
                        Throttle.force(NeutralThrottle);
                }
                else if (timeToAP < HoldAPTime)
                {
                    if (Throttle.value >= 1 && (timeToAP < PrevTime || (timeToAP - HoldAPTime) / TimeSpeed > 20))
                    {
                        NeutralThrottle = 1;
                        PitchAdjustment.value += 0.1f;
                    }
                    Throttle.value += diff;

                    if (0 < (timeToAP - HoldAPTime) / TimeSpeed && (timeToAP - HoldAPTime) / TimeSpeed < 20)  // We will reach desired AP time in <20 second
                    {
                        PitchAdjustment.value -= 0.1f;
                    }
                }
                else if (timeToAP > HoldAPTime)
                {
                    if (PitchAdjustment > 0)
                        PitchAdjustment.value -= 0.1f;
                    else
                        Throttle.value -= diff;
                }
            }
            if (PitchAdjustment < 0)
                PitchAdjustment.value = 0;
            if (PitchAdjustment > MaxAngle(vessel))
                PitchAdjustment.value = MaxAngle(vessel);
            PrevTime = vessel.orbit.timeToAp;
            lastTimeMeasured = Time.time;
            if (Throttle.value < Sensitivity)
                Throttle.force(Sensitivity);
            if (Throttle.value > 1)
                Throttle.force(1);
            return (float)Throttle.value;
        }

        public string GetFlightMapFilename()
        {
            return IOUtils.GetFilePathFor(this.GetType(), string.Format("gt_vessel_{0}_{1}.png", LaunchName, LaunchBody.name));
        }

        private void Kill()
        {
            if (flightmap != null)
            {
                flightmap.WriteParameters(TurnAngle, StartSpeed);
                flightmap.WriteResults(DragLoss, GravityDragLoss, VectorLoss);
                Log("Flightmap with {0:0.00} loss", flightmap.TotalLoss());
                FlightMap previousLaunch = FlightMap.Load(GetFlightMapFilename(), this);
                if (getVessel.vesselName != "Untitled Space Craft" // Don't save the default vessel name
                    && getVessel.altitude > getVessel.mainBody.atmosphereDepth
                    && (previousLaunch == null
                    || previousLaunch.BetterResults(DragLoss, GravityDragLoss, VectorLoss))) // Only save the best result
                    flightmap.Save(GetFlightMapFilename());
            }
            Launching = false;
            getVessel.OnFlyByWire -= new FlightInputCallback(fly);
            FlightInputHandler.state.mainThrottle = 0;
            attitude.enabled = false;
            getVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
        }

        private float LaunchHeading(Vessel vessel)
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
            Vessel vessel = getVessel;
            if (!Launching)
                Kill();
            //if (!WindowVisible) 
            //    Kill();
            else if (vessel.orbit.ApA > DestinationHeight * 1000 && vessel.altitude > vessel.mainBody.atmosphereDepth)
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
                    s.mainThrottle = APThrottle(vessel.orbit.timeToAp, vessel);
                else
                    s.mainThrottle = 0;
                RelativeVelocity = vesselState.surfaceVelocity;
                if (InPitchProgram && PitchSet)
                {
                    if (vessel.ProgradePitch() + 90 >= TurnAngle)
                        InPitchProgram = false;
                }
                if (vessel.speed < StartSpeed)
                {
                    InPitchProgram = true;
                    attitude.attitudeTo(Quaternion.Euler(-90, LaunchHeading(vessel), 0) * RollRotation(), AttitudeReference.SURFACE_NORTH, this);
                }
                else if (InPitchProgram)
                {
                    attitude.attitudeTo(Quaternion.Euler(-90 + TurnAngle, LaunchHeading(vessel), 0) * RollRotation(), AttitudeReference.SURFACE_NORTH, this);
                    PitchSet = true;
                }
                else if (vesselState.dynamicPressure > maxQ * 0.5 || vesselState.dynamicPressure > PressureCutoff)
                { // Still ascending, or not yet below the cutoff pressure
                    attitude.attitudeTo(Quaternion.Euler(vessel.ProgradePitch() - PitchAdjustment, LaunchHeading(vessel), 0) * RollRotation(), AttitudeReference.SURFACE_NORTH, this);
                }
                else
                {
                    //attitude.attitudeTo(Quaternion.Euler(ProgradePitch(false) - PitchAdjustment, LaunchHeading(), 0) * RollRotation(), AttitudeReference.SURFACE_NORTH, this);
                    attitude.attitudeTo(Quaternion.Euler(0 - PitchAdjustment, 0, Roll), AttitudeReference.ORBIT, this);
                    RelativeVelocity = vesselState.orbitalVelocity;
                }
                attitude.enabled = true;
                attitude.Drive(s);
                CalculateLosses(getVessel);
            }
        }

        string PreflightInfo(Vessel vessel)
        {
            string info;
            info = string.Format("Drag area {0:0.00}", vesselState.areaDrag);
            info += string.Format("\nDrag coefficient {0:0.00}", vesselState.dragCoef);
            info += string.Format("\nDrag coefficient fwd {0:0.00}", vessel.DragCubeCoefForward());
            info += string.Format("\nMass {0:0.00}", vesselState.mass);
            DragRatio.value = vesselState.areaDrag/vesselState.mass;
            info += string.Format("\narea/mass {0:0.00}", DragRatio.value);
            info += string.Format("\nGuess TWR {0:0.00}", TWRWeightedAverage(2 * vessel.mainBody.GeeASL * DestinationHeight,vessel));
            info += string.Format("\nPitch {0:0.00}", vessel.Pitch());
            return info;
        }

        void CalculateLosses(Vessel vessel)
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
            if (getVessel.mainBody.atmosphereDepth > 0)
                DestinationHeight = getVessel.mainBody.atmosphereDepth + 10000;
            else
                DestinationHeight = getVessel.mainBody.timeWarpAltitudeLimits[1] + 10000;
            DestinationHeight /= 1000;
            try
            {
                savenode = ConfigNode.Load(ConfigFilename(getVessel));
                if (savenode != null)
                {
                    if (ConfigNode.LoadObjectFromConfig(this, savenode))
                        Log("Vessel loaded from " + ConfigFilename(getVessel));
                    else
                        Log("Vessel NOT loaded from " + ConfigFilename(getVessel));
                }
            }
            catch (Exception ex)
            {
                Log("Vessel Load error " + ex.GetType());
            }
        }

        void SaveParameters()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilename(getVessel)));
            ConfigNode savenode = ConfigNode.CreateConfigFromObject(this);
            savenode.Save(ConfigFilename(getVessel));
            Log("Vessel saved to " + ConfigFilename(getVessel));
        }

        void OnDestroy()
        {
            try
            {
                Kill();
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
            ApplicationLauncher.Instance.RemoveModApplication(button);
            //SaveParameters();
        }

    }
}
