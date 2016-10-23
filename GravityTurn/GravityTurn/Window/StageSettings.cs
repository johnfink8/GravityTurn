using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GravityTurn.Window
{
    public class StageSettings : BaseWindow
    {
        HelpWindow helpWindow;
        public StageSettings(GravityTurner turner, int WindowID, HelpWindow inhelpWindow)
            : base(turner, WindowID)
        {
            helpWindow = inhelpWindow;
            WindowTitle = "GravityTurn Stage Settings";
        }

        public override void WindowGUI(int windowID)
        {
            base.WindowGUI(windowID);
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            ItemLabel("Fairing Pressure");
            turner.FairingPressure.setValue(GUILayout.TextField(string.Format("{0:0}", turner.FairingPressure), GUILayout.Width(60)));
            turner.FairingPressure.locked = GuiUtils.LockToggle(turner.FairingPressure.locked);
            helpWindow.Button("Dynamic pressure where we pop the procedural fairings.  Higher values will pop lower in the atmosphere, which saves weight, but can cause overheating.  Fairings are heavy, so it's definitely a good idea to pop them as soon as possible.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Stage Post Delay");
            turner.autostagePostDelay.setValue(GUILayout.TextField(string.Format("{0:0}", turner.autostagePostDelay), GUILayout.Width(60)));
            turner.autostagePostDelay.locked = GuiUtils.LockToggle(turner.autostagePostDelay.locked);
            helpWindow.Button("Delay after a stage event before we consider the next stage.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Stage Pre Delay");
            turner.autostagePreDelay.setValue(GUILayout.TextField(string.Format("{0:0}", turner.autostagePreDelay), GUILayout.Width(60)));
            turner.autostagePreDelay.locked = GuiUtils.LockToggle(turner.autostagePreDelay.locked);
            helpWindow.Button("Delay after running out of fuel before we activate the next stage.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ItemLabel("Stage Limit");
            turner.autostageLimit.setValue(GUILayout.TextField(string.Format("{0:0}", turner.autostageLimit), GUILayout.Width(60)));
            turner.autostageLimit.locked = GuiUtils.LockToggle(turner.autostageLimit.locked);
            helpWindow.Button("Stop at this stage number");
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}
