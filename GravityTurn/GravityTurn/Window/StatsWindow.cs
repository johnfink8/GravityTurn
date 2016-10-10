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
            windowPos.height = 150;
        }

        public override void WindowGUI(int windowID)
        {
            base.WindowGUI(windowID);

            GUILayout.Label(turner.Message, GUILayout.Width(300), GUILayout.Height(150));
        }
    }
}
