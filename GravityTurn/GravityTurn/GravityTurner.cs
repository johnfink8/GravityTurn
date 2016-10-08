using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;
using UnityEngine;
using KSP.IO;
using System.IO;
using System.Diagnostics;
using KSP.UI.Screens;
#if DEBUG
using KramaxReloadExtensions;
#endif

namespace GravityTurn
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class GravityTurner : ReloadableMonoBehaviour
    {
        public static Vessel getVessel { get { return FlightGlobals.ActiveVessel; } }

        #region GUI Variables

        [Persistent]
        public EditableValue StartSpeed = new EditableValue(100);
        [Persistent]
        public EditableValue HoldAPTime = new EditableValue(50);
        [Persistent]
        public EditableValue APTimeStart = new EditableValue(50);
        [Persistent]
        public EditableValue APTimeFinish = new EditableValue(50);
        [Persistent]
        public EditableValue TurnAngle = new EditableValue(10);
        [Persistent]
        public EditableValue Sensitivity = new EditableValue(0.5);
        [Persistent]
        public EditableValue Roll = new EditableValue(0);
        [Persistent]
        public EditableValue DestinationHeight = new EditableValue(80);
        [Persistent]
        public EditableValue PressureCutoff = new EditableValue(1200);
        [Persistent]
        public EditableValue Inclination = new EditableValue(0);
        [Persistent]
        public bool EnableStageManager = true;
        [Persistent]
        public EditableValue FairingPressure = new EditableValue(10000, "{0:0}");
        [Persistent]
        public bool EnableStats = false;


        #endregion

        #region Misc. Public Variables
        public double HorizontalDistance = 0;
        public double MaxThrust = 0;
        public MovingAverage Throttle = new MovingAverage(10, 1);
        public float lastTimeMeasured = 0.0f;
        public VesselState vesselState = null;
        public double TimeSpeed = 0;
        public double PrevTime = 0;
        public MovingAverage PitchAdjustment = new MovingAverage(4, 0);
        public bool Launching = false;
        public string LaunchName = "";
        public CelestialBody LaunchBody = null;

        #endregion

        #region Window Stuff

        public ApplicationLauncherButton button;
        Window.MainWindow mainWindow = null;
        public Window.WindowManager windowManager = new Window.WindowManager();
        public Window.FlightMapWindow flightMapWindow;
        public Window.StatsWindow statsWindow;
        public string Message = "";

        #endregion

        #region Loss and related variables

        public float NeutralThrottle = 0.5f;
        public double TotalLoss = 0;
        public double MaxHeat = 0;
        double VelocityLost = 0;
        double DragLoss = 0;
        double GravityDragLoss = 0;
        double FlyTimeInterval = 0;
        double VectorLoss = 0;
        double TotalBurn = 0;
        bool InPitchProgram = false;
        bool PitchSet = false;
        MovingAverage DragRatio = new MovingAverage();

        #endregion

        #region Controllers and such

        AttitudeController attitude = null;
        public StageController stage;
        StageStats stagestats = null;
        MechjebWrapper mucore = new MechjebWrapper();
        LaunchDB launchdb = null;

        #endregion

        private int lineno { get { StackFrame callStack = new StackFrame(1, true); return callStack.GetFileLineNumber(); } }
        public static void Log(
            string format,
            params object[] args
            )
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
            return LaunchDB.GetBaseFilePath(this.GetType(), string.Format("gt_vessel_{0}_{1}.cfg",vessel.id.ToString(),vessel.mainBody.name));
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.Repaint || Event.current.isMouse)
            {
                //myPreDrawQueue(); // Your current on preDrawQueue code
            }
            windowManager.DrawGuis(); // Your current on postDrawQueue code
        }

        /*
         * Called after the scene is loaded.
         */
        public void Awake()
        {
            Log("GravityTurn: Awake {0}", this.GetInstanceID());
        }

        void Start()
        {
            Log("Starting");
            try
            {
                mucore.init();
                vesselState = new VesselState();
                attitude = new AttitudeController(this);
                stage = new StageController(this);
                attitude.OnStart();
                stagestats = new StageStats(stage);
                stagestats.editorBody = getVessel.mainBody;
                stagestats.OnModuleEnabled();
                stagestats.OnFixedUpdate();
                stagestats.RequestUpdate(this);
                stagestats.OnFixedUpdate();
                CreateButtonIcon();
                LaunchName = new string(getVessel.vesselName.ToCharArray());
                LaunchBody = getVessel.mainBody;
                launchdb = new LaunchDB(this);
                launchdb.Load();

                mainWindow = new Window.MainWindow(this, 6378070);
                flightMapWindow = new Window.FlightMapWindow(this, 548302);
                statsWindow = new Window.StatsWindow(this, 6378070 + 4);
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
        }

        private void SetWindowOpen()
        {
            mainWindow.WindowVisible = true;
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
            Message = "";
            VectorLoss = 0;
            HorizontalDistance = 0;
            MaxThrust = GetMaxThrust(vessel);
            bool openFlightmap = false;
            openFlightmap = flightMapWindow.WindowVisible;
            flightMapWindow.flightMap = new FlightMap(this);
            flightMapWindow.WindowVisible = openFlightmap;
        }

        private void CreateButtonIcon()
        {
            button = ApplicationLauncher.Instance.AddModApplication(
                SetWindowOpen,
                () => mainWindow.WindowVisible = false,
                null,
                null,
                null,
                null,
                ApplicationLauncher.AppScenes.ALWAYS,
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

        public void CalculateSettings(Vessel vessel,bool UseBest=false)
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

            double guessTurn, guessSpeed;
            if (UseBest && launchdb.BestSettings(out guessTurn,out guessSpeed))
            {
                StartSpeed = guessSpeed;
                TurnAngle = guessTurn;
            }
            else if (launchdb.GuessSettings(out guessTurn, out guessSpeed))
            {
                StartSpeed = guessSpeed;
                TurnAngle = guessTurn;
            }
            
            APTimeFinish = 50;
            APTimeStart = 50;
            Sensitivity = 0.5;
            if (vessel.mainBody.atmosphereDepth > 0)
                DestinationHeight = vessel.mainBody.atmosphereDepth + 10000;
            else
                DestinationHeight = vessel.mainBody.timeWarpAltitudeLimits[1] + 10000;
            DestinationHeight /= 1000;
            Roll = 0;
            Inclination = 0;
            PressureCutoff = 1200;
            SaveParameters();
        }

        private void DebugGUI(int windowID)
        {
            GUILayout.Box(PreflightInfo(getVessel));
            GUI.DragWindow(new Rect(0, 0, 10000, 2000));
        }

        public void Launch()
        {
            if (StageManager.CurrentStage == StageManager.StageCount)
                StageManager.ActivateNextStage();
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
            if (Launching)
            {
                stagestats.editorBody = getVessel.mainBody;
                vesselState.Update(getVessel);
                attitude.OnFixedUpdate();
                stagestats.OnFixedUpdate();
                stagestats.RequestUpdate(this);
                if (flightMapWindow.flightMap != null && Launching)
                    flightMapWindow.flightMap.UpdateMap(getVessel);
            }
            else if (EnableStats && !getVessel.Landed)
            {
                CalculateLosses(getVessel);
                stagestats.editorBody = getVessel.mainBody;
                vesselState.Update(getVessel);
                attitude.OnFixedUpdate();
                stagestats.OnFixedUpdate();
                stagestats.RequestUpdate(this);
            }
        }

        void Update()
        {
            if (Launching)
            {
                attitude.OnUpdate();
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


        public string GetFlightMapFilename()
        {
            return LaunchDB.GetBaseFilePath(this.GetType(), string.Format("gt_vessel_{0}_{1}.png", LaunchName, LaunchBody.name));
        }

        public void Kill()
        {
            if (flightMapWindow.flightMap != null)
            {
                flightMapWindow.flightMap.WriteParameters(TurnAngle, StartSpeed);
                flightMapWindow.flightMap.WriteResults(DragLoss, GravityDragLoss, VectorLoss);
                Log("Flightmap with {0:0.00} loss", flightMapWindow.flightMap.TotalLoss());
                FlightMap previousLaunch = FlightMap.Load(GetFlightMapFilename(), this);
                if (getVessel.vesselName != "Untitled Space Craft" // Don't save the default vessel name
                    && getVessel.altitude > getVessel.mainBody.atmosphereDepth
                    && (previousLaunch == null
                    || previousLaunch.BetterResults(DragLoss, GravityDragLoss, VectorLoss))) // Only save the best result
                    flightMapWindow.flightMap.Save(GetFlightMapFilename());
            }
            Launching = false;
            launchdb.Save();
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
            else if (vessel.orbit.ApA > DestinationHeight * 1000 && vessel.altitude > vessel.mainBody.atmosphereDepth)
            {
                if (TimeWarp.CurrentRateIndex > 0)
                    TimeWarp.SetRate(0, true);
                launchdb.RecordLaunch();
                if (mucore.Initialized)
                    mucore.CircularizeAtAP();
                Kill();
            }
            else
            {
                if (EnableStageManager)
                    stage.Update();
                if (vessel.orbit.ApA < DestinationHeight * 1000)
                    s.mainThrottle = Calculations.APThrottle(vessel.orbit.timeToAp, this);
                else
                    s.mainThrottle = 0;
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
                else if (vesselState.dynamicPressure > vesselState.maxQ * 0.5 || vesselState.dynamicPressure > PressureCutoff)
                { // Still ascending, or not yet below the cutoff pressure
                    attitude.attitudeTo(Quaternion.Euler(vessel.ProgradePitch() - PitchAdjustment, LaunchHeading(vessel), 0) * RollRotation(), AttitudeReference.SURFACE_NORTH, this);
                }
                else
                {
                    //attitude.attitudeTo(Quaternion.Euler(ProgradePitch(false) - PitchAdjustment, LaunchHeading(), 0) * RollRotation(), AttitudeReference.SURFACE_NORTH, this);
                    attitude.attitudeTo(Quaternion.Euler(0 - PitchAdjustment, 0, Roll), AttitudeReference.ORBIT, this);
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
            info += string.Format("\nTimeToDesiredAP {0:0.00}", Calculations.TimeToReachAP(vesselState, vesselState.speedVertical, HoldAPTime));
            return info;
        }

        void CalculateLosses(Vessel vessel)
        {
            if (vesselState.mass == 0)
                return;

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
            TotalLoss = DragLoss + GravityDragLossAtAp + VectorLoss;
            if (vessel.CriticalHeatPart().CriticalHeat() > MaxHeat)
                MaxHeat = vessel.CriticalHeatPart().CriticalHeat();
            launchdb.RecordLaunch();
            Message = string.Format(
                "Air Drag:\t\t{0:0.00}m/s²\n"+
                "GravityDrag:\t{1:0.00}m/s²\n" +
                "Thrust Vector Drag:\t{5:0.00}m/s²\n" +
                "Air Drag Loss:\t{2:0.00}m/s\n" +
                "Gravity Drag Loss:\t{3:0.00} -> {4:0.00}m/s (at AP)\n\n" +
                "Total Vector Loss:\t{6:0.00}m/s\n" +
                "Total Loss:\t{7:0.00}m/s\n"+
                "Total Burn:\t\t{8:0.0}",
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

        public void SaveParameters()
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
            windowManager.OnDestroy();
            ApplicationLauncher.Instance.RemoveModApplication(button);
            //SaveParameters();
        }

    }
}
