using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GravityTurn.Window
{
    public class FlightMapWindow : BaseWindow
    {
        public FlightMap flightMap;
        public FlightMapWindow(GravityTurner turner, int inWindowID, int width = 800, int height = 400)
            : base(turner, inWindowID)
        {
            windowPos = new Rect(Screen.width / 2 - width / 2, 100, width, height);
            flightMap = new FlightMap(turner, width, height);
            WindowTitle = "FlightMap";
        }

        public override void WindowGUI(int windowID)
        {
            base.WindowGUI(windowID);
            GUIStyle mySty = new GUIStyle();
            mySty.normal.textColor = mySty.focused.textColor = Color.white;
            mySty.fontSize = 20;
            mySty.fontStyle = FontStyle.Bold;
            GUILayout.Box(flightMap.texture);
            Vector2 pivotPoint = new Vector2(windowPos.width - 25, windowPos.height / 2 - 30);
            GUIUtility.RotateAroundPivot(-90, pivotPoint);
            GUI.Label(new Rect(windowPos.width - 80, windowPos.height / 2 - 40, 80, 20), "Altitude", mySty);
            GUIUtility.RotateAroundPivot(90, pivotPoint);
            GUI.Label(new Rect(windowPos.width / 2 - 80, windowPos.height - 25, 160, 20), "Horizontal Distance", mySty);
        }
    }
}
