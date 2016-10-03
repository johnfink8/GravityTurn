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
            stagesettings = new StageSettings(inTurner, inWindowID + 2,helpWindow);
        }

        public override void WindowGUI(int windowID)
        {
            base.WindowGUI(windowID);
            if (!WindowVisible)
                turner.button.SetFalse(false);
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            ItemLabel("Start m/s");
            turner.StartSpeed.setValue(GUILayout.TextField(string.Format("{0:0.0}", turner.StartSpeed), GUILayout.Width(60)));
            helpWindow.Button("At this speed, pitch to Turn Angle to begin the gravity turn.  Stronger rockets and extremely aerodynamically stable rockets should do this earlier.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Turn Angle");
            turner.TurnAngle.setValue(GUILayout.TextField(string.Format("{0:0.0}", turner.TurnAngle), GUILayout.Width(60)));
            helpWindow.Button("Angle to start turn at Start Speed.  Higher values may cause aerodynamic stress.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Hold AP Time Start");
            turner.APTimeStart.setValue(GUILayout.TextField(turner.APTimeStart.ToString(), GUILayout.Width(60)));
            helpWindow.Button("Starting value for Time To Prograde.  Higher values will make a steeper climb.  Steeper climbs are usually worse.  Lower values may cause overheating or death.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Hold AP Time Finish");
            turner.APTimeFinish.setValue(GUILayout.TextField(turner.APTimeFinish.ToString(), GUILayout.Width(60)));
            helpWindow.Button("AP Time will fade to this value, to vary the steepness of the ascent during the ascent.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Sensitivity");
            turner.Sensitivity.setValue(GUILayout.TextField(turner.Sensitivity.ToString(), GUILayout.Width(60)));
            helpWindow.Button("Will not throttle below this value.  Mostly a factor at the end of ascent.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Destination Height (km)");
            turner.DestinationHeight.setValue(GUILayout.TextField(turner.DestinationHeight.ToString(), GUILayout.Width(60)));
            helpWindow.Button("Desired Apoapsis.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Roll");
            turner.Roll.setValue(GUILayout.TextField(turner.Roll.ToString(), GUILayout.Width(60)));
            helpWindow.Button("If you want a particular side of your ship to face downwards.  Shouldn't matter for most ships.  May cause mild nausea.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Inclination");
            turner.Inclination.setValue(GUILayout.TextField(turner.Inclination.ToString(), GUILayout.Width(60)));
            helpWindow.Button("Desired orbit inclination.  Any non-zero value WILL make your launch less efficient. Final inclination will also not be perfect.  Sorry about that, predicting coriolis is hard.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Pressure Cutoff");
            turner.PressureCutoff.setValue(GUILayout.TextField(turner.PressureCutoff.ToString(), GUILayout.Width(60)));
            helpWindow.Button("Dynamic pressure where we change from Surface to Orbital velocity tracking\nThis will be a balance point between aerodynamic drag in the upper atmosphere vs. thrust vector loss.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Setup", GUILayout.ExpandWidth(false)))
                stagesettings.WindowVisible = !stagesettings.WindowVisible;
            turner.EnableStageManager = GUILayout.Toggle(turner.EnableStageManager, "Auto-StageManager");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            turner.flightMapWindow.WindowVisible = GUILayout.Toggle(turner.flightMapWindow.WindowVisible, "Show Launch Map");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("Time to match: {0:0.0}", turner.HoldAPTime), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GravityTurner.getVessel.Landed && !turner.Launching)
            {
                if (GUILayout.Button("Improve Guess", GUILayout.ExpandWidth(false)))
                    turner.CalculateSettings(GravityTurner.getVessel);
                if (GUILayout.Button("Previous Best Settings", GUILayout.ExpandWidth(false)))
                    turner.CalculateSettings(GravityTurner.getVessel, true);
                helpWindow.Button("Improve Guess will try to extrapolate the best settings based on previous launches.  This may end in fiery death, but it won't happen the same way twice.  Be warned, sometimes launches get worse before they get better.  But they do get better.");
            }
            GUILayout.EndHorizontal();
            if (GravityTurner.getVessel.Landed && !turner.Launching && GUILayout.Button("Launch!"))
            {
                turner.Launch();
            }
            if (turner.Launching && GUILayout.Button("Abort!"))
            {
                turner.Kill();
            }
            GUILayout.Label(turner.Message);
            GUILayout.EndVertical();
            double StopHeight = GravityTurner.getVessel.mainBody.atmosphereDepth;
            if (StopHeight <= 0)
                StopHeight = turner.DestinationHeight * 1000;
            turner.HoldAPTime = turner.APTimeStart + ((float)GravityTurner.getVessel.altitude / (float)StopHeight * (turner.APTimeFinish - turner.APTimeStart));
            if (turner.HoldAPTime > Math.Max(turner.APTimeFinish, turner.APTimeStart))
                turner.HoldAPTime = Math.Max(turner.APTimeFinish, turner.APTimeStart);
            if (turner.HoldAPTime < Math.Min(turner.APTimeFinish, turner.APTimeStart))
                turner.HoldAPTime = Math.Min(turner.APTimeFinish, turner.APTimeStart);
        }
    }
}
