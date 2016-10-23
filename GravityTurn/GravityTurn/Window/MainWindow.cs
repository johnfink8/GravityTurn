using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GravityTurn.Window
{
    class MainWindow :  BaseWindow
    {

        HelpWindow helpWindow = null;
        StageSettings stagesettings = null;

        public MainWindow(GravityTurner inTurner, int inWindowID)
            : base(inTurner,inWindowID)
        {
            turner = inTurner;
            helpWindow = new HelpWindow(inTurner,inWindowID+1);
            stagesettings = new StageSettings(inTurner, inWindowID + 2, helpWindow);
            windowPos.width = 300;
            windowPos.height = 100;
        }

        private void UiStartSpeed()
        {
            GUILayout.BeginHorizontal();
            ItemLabel("Start m/s");
            turner.StartSpeed.setValue(GUILayout.TextField(string.Format("{0:0.0}", turner.StartSpeed), GUILayout.Width(60)));
            turner.StartSpeed.locked = GuiUtils.LockToggle(turner.StartSpeed.locked);
            helpWindow.Button("At this speed, pitch to Turn Angle to begin the gravity turn.  Stronger rockets and extremely aerodynamically stable rockets should do this earlier.");
            GUILayout.EndHorizontal();

        }
        private void UiTurnAngle()
        {
            GUILayout.BeginHorizontal();
            ItemLabel("Turn Angle");
            turner.TurnAngle.setValue(GUILayout.TextField(string.Format("{0:0.0}", turner.TurnAngle), GUILayout.Width(60)));
            turner.TurnAngle.locked = GuiUtils.LockToggle(turner.TurnAngle.locked);
            helpWindow.Button("Angle to start turn at Start Speed.  Higher values may cause aerodynamic stress.");
            GUILayout.EndHorizontal();
        }
        private void UiAPTimeStart()
        {
            GUILayout.BeginHorizontal();
            ItemLabel("Hold AP Time Start");
            turner.APTimeStart.setValue(GUILayout.TextField(turner.APTimeStart.ToString(), GUILayout.Width(60)));
            turner.APTimeStart.locked = GuiUtils.LockToggle(turner.APTimeStart.locked);
            helpWindow.Button("Starting value for Time To Prograde.  Higher values will make a steeper climb.  Steeper climbs are usually worse.  Lower values may cause overheating or death.");
            GUILayout.EndHorizontal();
        }
        private void UiAPTimeFinish()
        {
            GUILayout.BeginHorizontal();
            ItemLabel("Hold AP Time Finish");
            turner.APTimeFinish.setValue(GUILayout.TextField(turner.APTimeFinish.ToString(), GUILayout.Width(60)));
            turner.APTimeFinish.locked = GuiUtils.LockToggle(turner.APTimeFinish.locked);
            helpWindow.Button("AP Time will fade to this value, to vary the steepness of the ascent during the ascent.");
            GUILayout.EndHorizontal();
        }
        private void UiSensitivity()
        {
            GUILayout.BeginHorizontal();
            ItemLabel("Sensitivity");
            turner.Sensitivity.setValue(GUILayout.TextField(turner.Sensitivity.ToString(), GUILayout.Width(60)));
            turner.Sensitivity.locked = GuiUtils.LockToggle(turner.Sensitivity.locked);
            helpWindow.Button("Will not throttle below this value.  Mostly a factor at the end of ascent.");
            GUILayout.EndHorizontal();
        }
        private void UiDestinationHeight()
        {
            GUILayout.BeginHorizontal();
            ItemLabel("Destination Height (km)");
            turner.DestinationHeight.setValue(GUILayout.TextField(turner.DestinationHeight.ToString(), GUILayout.Width(60)));
            turner.DestinationHeight.locked = GuiUtils.LockToggle(turner.DestinationHeight.locked);
            helpWindow.Button("Desired Apoapsis.");
            GUILayout.EndHorizontal();
        }
        private void UiRoll()
        {
            GUILayout.BeginHorizontal();
            ItemLabel("Roll");
            turner.Roll.setValue(GUILayout.TextField(turner.Roll.ToString(), GUILayout.Width(60)));
            turner.Roll.locked = GuiUtils.LockToggle(turner.Roll.locked);
            helpWindow.Button("If you want a particular side of your ship to face downwards.  Shouldn't matter for most ships.  May cause mild nausea.");
            GUILayout.EndHorizontal();
        }
        private void UiInclination()
        {
            GUILayout.BeginHorizontal();
            ItemLabel("Inclination");
            turner.Inclination.setValue(GUILayout.TextField(turner.Inclination.ToString(), GUILayout.Width(60)));
            turner.Inclination.locked = GuiUtils.LockToggle(turner.Inclination.locked);
            helpWindow.Button("Desired orbit inclination.  Any non-zero value WILL make your launch less efficient. Final inclination will also not be perfect.  Sorry about that, predicting coriolis is hard.");
            GUILayout.EndHorizontal();
        }
        private void UiPressureCutoff()
        {
            GUILayout.BeginHorizontal();
            ItemLabel("Pressure Cutoff");
            turner.PressureCutoff.setValue(GUILayout.TextField(turner.PressureCutoff.ToString(), GUILayout.Width(60)));
            turner.PressureCutoff.locked = GuiUtils.LockToggle(turner.PressureCutoff.locked);
            helpWindow.Button("Dynamic pressure where we change from Surface to Orbital velocity tracking\nThis will be a balance point between aerodynamic drag in the upper atmosphere vs. thrust vector loss.");
            GUILayout.EndHorizontal();
        }

        public override void WindowGUI(int windowID)
        {
            base.WindowGUI(windowID);
            if (!WindowVisible)
            {
                turner.button.SetFalse(false);
                turner.SaveParameters();
            }
            GUILayout.BeginVertical();
            UiStartSpeed();
            UiTurnAngle();
            UiAPTimeStart();
            UiAPTimeFinish();
            UiSensitivity();
            UiDestinationHeight();
            UiRoll();
            UiInclination();
            UiPressureCutoff();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Setup", GUILayout.ExpandWidth(false)))
                stagesettings.WindowVisible = !stagesettings.WindowVisible;
            turner.EnableStageManager = GUILayout.Toggle(turner.EnableStageManager, "Auto-StageManager");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            turner.flightMapWindow.WindowVisible = GUILayout.Toggle(turner.flightMapWindow.WindowVisible, "Show Launch Map", GUILayout.ExpandWidth(false));
            turner.EnableStats = GUILayout.Toggle(turner.EnableStats, "Show Stats", GUILayout.ExpandWidth(false));
            if (turner.statsWindow.WindowVisible != turner.EnableStats)
            {
                turner.statsWindow.WindowVisible = turner.EnableStats;
                turner.statsWindow.Save();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("Time to match: {0:0.0}", turner.HoldAPTime), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GravityTurner.getVessel.Landed && !turner.Launching)
            {
                string guess = turner.IsLaunchDBEmpty() ? "First Guess" : "Improve Guess";
                if (GUILayout.Button(guess, GUILayout.ExpandWidth(false)))
                    turner.CalculateSettings(GravityTurner.getVessel);
                if (GUILayout.Button("Previous Best Settings", GUILayout.ExpandWidth(false)))
                    turner.CalculateSettings(GravityTurner.getVessel, true);
                helpWindow.Button("Improve Guess will try to extrapolate the best settings based on previous launches.  This may end in fiery death, but it won't happen the same way twice.  Be warned, sometimes launches get worse before they get better.  But they do get better.");
            }
            GUILayout.EndHorizontal();
            if (GravityTurner.getVessel.Landed && !turner.Launching && GUILayout.Button("Launch!", GUILayout.ExpandWidth(true), GUILayout.MinHeight(30)))
            {
                turner.Launch();
            }
            if (turner.Launching && GUILayout.Button("Abort!"))
            {
                turner.Kill();
                turner.RecordAbortedLaunch();
            }
#if DEBUG
           // GUILayout.Label(GravityTurner.DebugMessage, GUILayout.ExpandWidth(true), GUILayout.MinHeight(200));
#endif

            GUILayout.EndVertical();
            double StopHeight = GravityTurner.getVessel.mainBody.atmosphereDepth;
            if (StopHeight <= 0)
                StopHeight = turner.DestinationHeight * 1000;
            turner.HoldAPTime = turner.APTimeStart + ((float)GravityTurner.getVessel.altitude / (float)StopHeight * (turner.APTimeFinish - turner.APTimeStart));
            if (turner.HoldAPTime > Math.Max(turner.APTimeFinish, turner.APTimeStart))
                turner.HoldAPTime = Math.Max(turner.APTimeFinish, turner.APTimeStart);
            if (turner.HoldAPTime < Math.Min(turner.APTimeFinish, turner.APTimeStart))
                turner.HoldAPTime = Math.Min(turner.APTimeFinish, turner.APTimeStart);
            Rect r = GUILayoutUtility.GetLastRect();
            float minHeight = r.height + r.yMin + 10;
            if (windowPos.height != minHeight && minHeight>20)
            {
                windowPos.height = minHeight;
                Save();
            }
        }
    }
}
