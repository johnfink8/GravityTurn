using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GravityTurn.Window
{
    public class StatsWindow: BaseWindow
    {
        public StatsWindow(GravityTurner turner, int WindowID)
            : base(turner, WindowID)
        {
            WindowTitle = "GravityTurn Statistics Window";
            windowPos.height = 200;
        }

        public override void WindowGUI(int windowID)
        {
            base.WindowGUI(windowID);
            
            GUILayout.BeginVertical();
            GUILayout.Label(turner.Message, GUILayout.Width(300), GUILayout.Height(200));
            GUILayout.EndVertical();
            if (GameSettings.MODIFIER_KEY.GetKeyDown() && !GravityTurner.DebugShow)
            {
                GravityTurner.DebugShow = true;
            }
            if (GravityTurner.DebugShow)
            {
                GUILayout.BeginVertical();
                GUILayout.Label(GravityTurner.DebugMessage, GUILayout.Width(300), GUILayout.Height(350));
                GUILayout.EndVertical();
            }
        }
    }
}
