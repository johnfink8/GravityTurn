﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;
using UnityEngine;
using KSP.IO;
using System.IO;
using System.Diagnostics;
using KSP.UI.Screens;
using KramaxReloadExtensions;
using KSP.UI.Screens.Flight;

namespace GravityTurn
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class GravityTurner : ReloadableMonoBehaviour
    {
        public enum AscentProgram
        {
            Landed,
            InLaunch,
            InTurn,
            InInitialPitch,
            InInsertion,
            InCoasting,
            InCircularisation
        }

        public AscentProgram program = AscentProgram.Landed;

        public static Vessel getVessel { get { return FlightGlobals.ActiveVessel; } }

        #region GUI Variables

        [Persistent]
        public EditableValue StartSpeed = new EditableValue(100, locked: false);
        [Persistent]
        public EditableValue HoldAPTime = new EditableValue(50, locked: false);
        [Persistent]
        public EditableValue APTimeStart = new EditableValue(50, locked: true);
        [Persistent]
        public EditableValue APTimeFinish = new EditableValue(50, locked: true);
        [Persistent]
        public EditableValue TurnAngle = new EditableValue(10, locked: false);
        [Persistent]
        public EditableValue Sensitivity = new EditableValue(0.3, locked: true);
        [Persistent]
        public EditableValue Roll = new EditableValue(0, locked: true);
        [Persistent]
        public EditableValue DestinationHeight = new EditableValue(80, locked: true);
        [Persistent]
        public EditableValue PressureCutoff = new EditableValue(1200, locked: false);
        [Persistent]
        public EditableValue Inclination = new EditableValue(0, locked: true);
        [Persistent]
        public bool EnableStageManager = true;
        [Persistent]
        public bool EnableSpeedup = false;
        [Persistent]
        public EditableValue FairingPressure = new EditableValue(1000, "{0:0}");
        [Persistent]
        public EditableValue autostagePostDelay = new EditableValue(0.3d, "{0:0.0}");
        [Persistent]
        public EditableValue autostagePreDelay = new EditableValue(0.7d, "{0:0.0}");
        [Persistent]
        public EditableValue autostageLimit = new EditableValue(0, "{0:0}");
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
        public float YawAdjustment = 0.0f;
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
        static public string DebugMessage = "";
        static public bool DebugShow = false;

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
        bool PitchSet = false;
        MovingAverage DragRatio = new MovingAverage();

        #endregion

        #region Controllers and such

        AttitudeController attitude = null;
        public StageController stage;
        StageStats stagestats = null;
        MechjebWrapper mucore = new MechjebWrapper();
        LaunchDB launchdb = null;
        static int previousTimeWarp = 0;
        static public double inclinationHeadingCorrectionSpeed = 0;

        public bool IsLaunchDBEmpty()
        {
            return launchdb.IsEmpty();
        }

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
            method = string.Format(" [{0}]|{1}", stackFrame.GetMethod().ToString(), stackFrame.GetFileLineNumber());
#endif
            string incomingMessage;
            if (args == null)
                incomingMessage = format;
            else
                incomingMessage = string.Format(format, args);
            UnityEngine.Debug.Log(string.Format("GravityTurn{0} : {1}", method, incomingMessage));
        }


        string DefaultConfigFilename(Vessel vessel)
        {
            return LaunchDB.GetBaseFilePath(this.GetType(), string.Format("gt_vessel_default_{0}.cfg", vessel.mainBody.name));
        }
        string ConfigFilename(Vessel vessel)
        {
            return LaunchDB.GetBaseFilePath(this.GetType(), string.Format("gt_vessel_{0}_{1}.cfg", vessel.id.ToString(), vessel.mainBody.name));
        }

        private void OnGUI()
        {
            // hide UI if F2 was pressed
            if (!Window.BaseWindow.ShowGUI)
                return;
            if (Event.current.type == EventType.Repaint || Event.current.isMouse)
            {
                //myPreDrawQueue(); // Your current on preDrawQueue code
            }
            windowManager.DrawGuis(); // Your current on postDrawQueue code
        }

        private void ShowGUI()
        {
            Window.BaseWindow.ShowGUI = true;
        }
        private void HideGUI()
        {
            Window.BaseWindow.ShowGUI = false;
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

                GameEvents.onShowUI.Add(ShowGUI);
                GameEvents.onHideUI.Add(HideGUI);
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
            inclinationHeadingCorrectionSpeed = Calculations.CircularOrbitSpeed(getVessel.mainBody, getVessel.mainBody.Radius + DestinationHeight * 1000);
            Log("Orbit velocity {0:0.0}", inclinationHeadingCorrectionSpeed);
            inclinationHeadingCorrectionSpeed /= 1.7;
            Log("inclination heading correction {0:0.0}", inclinationHeadingCorrectionSpeed);
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
                double stagetwr = (stagestats.atmoStats[i].StartTWR(vessel.mainBody.GeeASL) + stagestats.atmoStats[i].MaxTWR(vessel.mainBody.GeeASL)) / 2;
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

        public void CalculateSettings(Vessel vessel, bool UseBest = false)
        {
            float baseFactor = Mathf.Round((float)vessel.mainBody.GeeASL * 100.0f) / 10.0f;
            Log("Base turn speed factor {0:0.00}", baseFactor);

            // reset the settings to defaults
            if (GameSettings.MODIFIER_KEY.GetKey())
            {
                launchdb.Clear();
                TurnAngle = 10;
                StartSpeed = baseFactor * 10.0;
                DestinationHeight = (vessel.StableOrbitHeight() + 10000) / 1000;
                GravityTurner.Log("Reset results");
                return;
            }
            Log("Min orbit height: {0}", vessel.StableOrbitHeight());

            stagestats.ForceSimunlation();
            double TWR = 0;
            for (int i = stagestats.atmoStats.Length - 1; i >= 0; i--)
            {
                double stagetwr = stagestats.atmoStats[i].StartTWR(vessel.mainBody.GeeASL);
                if (stagetwr > 0)
                {
                    if (vessel.StageHasSolidEngine(i))
                        TWR = (stagetwr + stagestats.atmoStats[i].MaxTWR(vessel.mainBody.GeeASL)) / 2.3;
                    else
                        TWR = stagetwr;
                    break;
                }
            }
            if (TWR > 1.2)
            {
                Log("First guess for TWR > 1.2 {0:0.00}", TWR);
                TWR -= 1.2;
                if (!TurnAngle.locked)
                    TurnAngle = Mathf.Clamp((float)(10 + TWR * 5), 10, 80);
                if (!StartSpeed.locked)
                {
                    StartSpeed = Mathf.Clamp((float)(baseFactor * 10 - TWR * baseFactor * 3), baseFactor, baseFactor * 10);
                    if (StartSpeed < 10)
                        StartSpeed = 10;
                }
            }

            double guessTurn, guessSpeed;
            if (UseBest && launchdb.BestSettings(out guessTurn, out guessSpeed))
            {
                Log("UseBest && launchdb.BestSettings");
                if (!StartSpeed.locked)
                    StartSpeed = guessSpeed;
                if (!TurnAngle.locked)
                    TurnAngle = guessTurn;
            }
            else if (launchdb.GuessSettings(out guessTurn, out guessSpeed))
            {
                Log("GuessSettings");
                if (!StartSpeed.locked)
                    StartSpeed = guessSpeed;
                if (!TurnAngle.locked)
                    TurnAngle = guessTurn;
            }

            if (!APTimeStart.locked)
                APTimeStart = 50;
            if (!APTimeFinish.locked)
                APTimeFinish = 50;
            if (!Sensitivity.locked)
                Sensitivity = 0.3;
            if (!DestinationHeight.locked)
            {
                DestinationHeight = vessel.StableOrbitHeight() + 10000;
                DestinationHeight /= 1000;
            }
            if (!Roll.locked)
                Roll = 0;
            if (!Inclination.locked)
                Inclination = 0;
            if (!PressureCutoff.locked)
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
            StageController.topFairingDeployed = false;
            if (StageManager.CurrentStage == StageManager.StageCount)
                StageManager.ActivateNextStage();
            InitializeNumbers(getVessel);
            getVessel.OnFlyByWire += new FlightInputCallback(fly);
            Launching = true;
            PitchSet = false;
            DebugShow = false;
            program = AscentProgram.Landed;
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
            else if (EnableStats && !getVessel.Landed && !getVessel.IsInStableOrbit())
            {
                CalculateLosses(getVessel);
                stagestats.editorBody = getVessel.mainBody;
                vesselState.Update(getVessel);
                attitude.OnFixedUpdate();
                stagestats.OnFixedUpdate();
                stagestats.RequestUpdate(this);
            }
            else if (EnableStats && !getVessel.Landed && getVessel.IsInStableOrbit())
            {
                if (VectorLoss > 0.01)
                {
                    Message = string.Format(
                        "Total Vector Loss:\t{0:0.00} m/s\n" +
                        "Total Loss:\t{1:0.00} m/s\n" +
                        "Total Burn:\t\t{2:0.0}\n\n",
                        VectorLoss,
                        TotalLoss,
                        TotalBurn
                        );
                }
                else
                    Message = "";

                Message += string.Format(
                    "Apoapsis:\t\t{0}\n" +
                    "Periapsis:\t\t{1}\n" +
                    "Inclination:\t\t{2:0.0} °\n",
                    OrbitExtensions.FormatOrbitInfo(getVessel.orbit.ApA, getVessel.orbit.timeToAp),
                    OrbitExtensions.FormatOrbitInfo(getVessel.orbit.PeA, getVessel.orbit.timeToPe),
                    getVessel.orbit.inclination
                    );

            }
            else if (EnableStats && getVessel.Landed)
            {
                double diffUT = Planetarium.GetUniversalTime() - delayUT;
                if (diffUT > 1 || Double.IsNaN(delayUT))
                {
                    vesselState.Update(getVessel);
                    stagestats.OnFixedUpdate();
                    stagestats.RequestUpdate(this);
                    Message = PreflightInfo(getVessel);
                    delayUT = Planetarium.GetUniversalTime();
                }
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
            getVessel.OnFlyByWire -= new FlightInputCallback(fly);
            FlightInputHandler.state.mainThrottle = 0;
            attitude.enabled = false;
            getVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
        }

        // this records an aborted launch as not sucessful
        public void RecordAbortedLaunch()
        {
            launchdb.RecordLaunch();
            launchdb.Save();
        }

        private float LaunchHeading(Vessel vessel)
        {
            return (float)MuUtils.HeadingForLaunchInclination(vessel.mainBody, Inclination, vessel.latitude, inclinationHeadingCorrectionSpeed);
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

        static public void StoreTimeWarp()
        {
            previousTimeWarp = TimeWarp.CurrentRateIndex;
        }

        static public void RestoreTimeWarp()
        {
            if (previousTimeWarp != 0)
            {
                TimeWarp.fetch.Mode = TimeWarp.Modes.LOW;
                TimeWarp.SetRate(previousTimeWarp, false);
            }
            previousTimeWarp = 0;
        }

        public void ApplySpeedup(int rate)
        {
            if (EnableSpeedup)
            {
                TimeWarp.fetch.Mode = TimeWarp.Modes.LOW;
                TimeWarp.SetRate(previousTimeWarp < rate ? rate : previousTimeWarp, false);
            }
        }

        static public void StopSpeedup()
        {
            TimeWarp.SetRate(0, false);
        }

        static double delayUT = double.NaN;

        private void fly(FlightCtrlState s)
        {
            if (!Launching)
            {
                Kill();
                return;
            }
            DebugMessage = "";
            Vessel vessel = getVessel;
            if (program != AscentProgram.InCoasting && vessel.orbit.ApA > DestinationHeight * 1000 && vessel.altitude < vessel.StableOrbitHeight())
            {
                CalculateLosses(getVessel);
                // save launch, ignoring losses due to coasting losses, but so we get results earlier
                launchdb.RecordLaunch();
                launchdb.Save();
                program = AscentProgram.InCoasting;
                DebugMessage += "In Coasting program\n";
                Throttle.force(0);
                Log("minorbit {0}, {1}", vessel.mainBody.minOrbitalDistance, vessel.StableOrbitHeight());
                // time warp to speed up things (if enabled)
                ApplySpeedup(2);
            }
            else if (vessel.orbit.ApA > DestinationHeight * 1000 && vessel.altitude > vessel.StableOrbitHeight())
            {
                Log("minorbit {0}, {1}", vessel.mainBody.minOrbitalDistance, vessel.StableOrbitHeight());
                program = AscentProgram.InCircularisation;
                StopSpeedup();
                GravityTurner.Log("Saving launchDB");
                launchdb.RecordLaunch();
                launchdb.Save();
                Kill();
                DebugMessage += "In Circularisation program\n";
                if (mucore.Initialized)
                {
                    program = AscentProgram.InCircularisation;
                    mucore.CircularizeAtAP();
                }

                button.SetFalse();
            }
            else
            {
                double minInsertionHeight = vessel.mainBody.atmosphere ? vessel.StableOrbitHeight() / 4 : Math.Max(DestinationHeight*667, vessel.StableOrbitHeight()*0.667);

                if (EnableStageManager && stage != null)
                    stage.Update();

                if (vessel.orbit.ApA < DestinationHeight * 1000)
                    s.mainThrottle = Calculations.APThrottle(vessel.orbit.timeToAp, this);
                else
                    s.mainThrottle = 0;
                if (program == AscentProgram.InInitialPitch && PitchSet)
                {
                    if (vessel.ProgradePitch() + 90 >= TurnAngle - 0.1)
                    {
                        delayUT = double.NaN;
                        // continue any previous timewarp
                        RestoreTimeWarp();
                        ApplySpeedup(1);
                        program = AscentProgram.InTurn;
                        DebugMessage += "Turning now\n";
                    }
                }
                if (vessel.speed < StartSpeed)
                {
                    DebugMessage += "In Launch program\n";
                    program = AscentProgram.InLaunch;
                    if (vesselState.altitudeBottom > vesselState.vesselHeight)
                        attitude.attitudeTo(Quaternion.Euler(-90, LaunchHeading(vessel), 0) * RollRotation(), AttitudeReference.SURFACE_NORTH, this);
                    else
                        attitude.attitudeTo(Quaternion.Euler(-90, 0, 0), AttitudeReference.SURFACE_NORTH, this);
                }
                else if (program == AscentProgram.InLaunch || program == AscentProgram.InInitialPitch)
                {
                    if (!PitchSet)
                    {
                        // remember and stop timewarp for pitching
                        StoreTimeWarp();
                        StopSpeedup();
                        PitchSet = true;
                        program = AscentProgram.InInitialPitch;
                        delayUT = Planetarium.GetUniversalTime();
                    }
                    DebugMessage += "In Pitch program\n";
                    double diffUT = Planetarium.GetUniversalTime() - delayUT;
                    float newPitch = Mathf.Min((float)(((double)TurnAngle * diffUT) / 5.0d + 2.0d), TurnAngle);
                    double pitch = (90d - vesselState.vesselPitch + vessel.ProgradePitch() + 90)/2;
                    attitude.attitudeTo(Quaternion.Euler(-90 + newPitch, LaunchHeading(vessel), 0) * RollRotation(), AttitudeReference.SURFACE_NORTH, this);
                    DebugMessage += String.Format("TurnAngle: {0:0.00}\n", TurnAngle.value);
                    DebugMessage += String.Format("Target pitch: {0:0.00}\n", newPitch);
                    DebugMessage += String.Format("Current pitch: {0:0.00}\n", pitch);
                    DebugMessage += String.Format("Prograde pitch: {0:0.00}\n", vessel.ProgradePitch()+90);
                }
                else if (vesselState.dynamicPressure > vesselState.maxQ * 0.5 || vesselState.dynamicPressure > PressureCutoff || vessel.altitude < minInsertionHeight)
                { // Still ascending, or not yet below the cutoff pressure or below min insertion heigt
                    DebugMessage += "In Turn program\n";
                    attitude.attitudeTo(Quaternion.Euler(vessel.ProgradePitch() - PitchAdjustment, LaunchHeading(vessel), 0) * RollRotation(), AttitudeReference.SURFACE_NORTH, this);
                }
                else
                {
                    // did we reach the desired inclination?
                    DebugMessage += String.Format("Insertion program\n");
                    Quaternion q = Quaternion.Euler(0 - PitchAdjustment, YawAdjustment, Roll);
                    // smooth out change from surface to orbital prograde
                    if (program != AscentProgram.InInsertion && program != AscentProgram.InCoasting)
                    {
                        // start timer
                        if (Double.IsNaN(delayUT))
                        {
                            // slow down timewarp
                            delayUT = Planetarium.GetUniversalTime();
                            StoreTimeWarp();
                            StopSpeedup();
                            // switch NavBall UI
                            FlightGlobals.SetSpeedMode(FlightGlobals.SpeedDisplayModes.Orbit);
                        }
                        double diffUT = Planetarium.GetUniversalTime() - delayUT;
                        //attitude.attitudeTo(q, AttitudeReference.ORBIT, this);
                        q.x = (attitude.lastAct.x * 8.0f + q.x) / 9.0f;
                        if (diffUT > 10 || (attitude.lastAct.x > 0.02 && diffUT > 2.0))
                        {
                            program = AscentProgram.InInsertion;
                            delayUT = double.NaN;
                            RestoreTimeWarp();
                            ApplySpeedup(2);
                        }
                    }
                    attitude.attitudeTo(q, AttitudeReference.ORBIT, this);
                }
                attitude.enabled = true;
                attitude.Drive(s);
                CalculateLosses(getVessel);
                DebugMessage += "-";
            }
        }

        string PreflightInfo(Vessel vessel)
        {
            string info = "";
            info += string.Format("Surface TWR:\t{0:0.00}\n", TWRWeightedAverage(2 * vessel.mainBody.GeeASL * DestinationHeight, vessel));
            info += string.Format("Mass:\t\t{0:0.00} t\n", vesselState.mass);
            info += string.Format("Height:\t\t{0:0.0} m\n", vesselState.vesselHeight);
            info += "\n";
            info += string.Format("Drag area:\t\t{0:0.00}\n", vesselState.areaDrag);
            info += string.Format("Drag coefficient:\t{0:0.00}\n", vesselState.dragCoef);
            info += string.Format("Drag coefficient fwd:\t{0:0.00}\n", vessel.DragCubeCoefForward());
            DragRatio.value = vesselState.areaDrag / vesselState.mass;
            info += string.Format("area/mass:\t{0:0.00}\n", DragRatio.value);
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

            Message = string.Format(
                "Air Drag:\t\t{0:0.00} m/s²\n" +
                "GravityDrag:\t{1:0.00} m/s²\n" +
                "Thrust Vector Drag:\t{5:0.00} m/s²\n" +
                "Air Drag Loss:\t{2:0.00} m/s\n" +
                "Gravity Drag Loss:\t{3:0.00} -> {4:0.00}m/s @AP\n\n" +
                "Total Vector Loss:\t{6:0.00} m/s\n" +
                "Total Loss:\t{7:0.00} m/s\n" +
                "Total Burn:\t\t{8:0.0}\n\n" +
                "Apoapsis:\t\t{9}\n" +
                "Periapsis:\t\t{10}\n" +
                "Inclination:\t\t{11:0.0} °\n",
                vesselState.drag,
                GravityDrag,
                DragLoss,
                GravityDragLoss, GravityDragLossAtAp,
                VectorDrag,
                VectorLoss,
                TotalLoss,
                TotalBurn,
                OrbitExtensions.FormatOrbitInfo(vessel.orbit.ApA, vessel.orbit.timeToAp),
                OrbitExtensions.FormatOrbitInfo(vessel.orbit.PeA, vessel.orbit.timeToPe),
                vessel.orbit.inclination
                );
        }

        void LoadParameters()
        {
            ConfigNode savenode;
            try
            {
                savenode = ConfigNode.Load(ConfigFilename(getVessel));
                if (savenode != null)
                {
                    ConfigNode.LoadObjectFromConfig(this, savenode);
                }
                else
                {
                    // now try to get defaults
                    savenode = ConfigNode.Load(DefaultConfigFilename(getVessel));
                    if (savenode != null)
                    {
                        if (ConfigNode.LoadObjectFromConfig(this, savenode))
                        {
                            CalculateSettings(getVessel);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Vessel Load error " + ex.GetType());
            }
        }

        public void SaveParameters()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilename(getVessel)));
                ConfigNode savenode = ConfigNode.CreateConfigFromObject(this);
                // save this vehicle
                savenode.Save(ConfigFilename(getVessel));
            }
            catch (Exception)
            {
                Log("Exception, vessel NOT saved!");
            }
        }
        public void SaveDefaultParameters()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilename(getVessel)));
            ConfigNode savenode = ConfigNode.CreateConfigFromObject(this);
            // save defaults for new vehicles
            savenode.Save(DefaultConfigFilename(getVessel));

            Log("Defaults saved to " + DefaultConfigFilename(getVessel));
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
            DebugShow = false;
            windowManager.OnDestroy();
            ApplicationLauncher.Instance.RemoveModApplication(button);
        }

    }
}
