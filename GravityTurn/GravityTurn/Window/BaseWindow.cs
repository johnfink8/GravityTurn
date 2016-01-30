using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GravityTurn.Window
{
    public class BaseWindow
    {
        int WindowID;
        public bool WindowVisible = false;
        public Rect windowPos = new Rect();
        public string WindowTitle = "GravityTurn";

        protected void ItemLabel(string labelText)
        {
            GUILayout.Label(labelText, GUILayout.ExpandWidth(false), GUILayout.Width(windowPos.width / 2));
        }

        public BaseWindow(GravityTurner turner, int inWindowID)
        {
            turner.windowManager.Register(this);
            WindowID = inWindowID;
        }

        public virtual void WindowGUI(int windowID)
        {
            if (GUI.Button(new Rect(windowPos.width - 18, 2, 16, 16), "X"))
            {
                WindowVisible = false;
            }
            GUI.DragWindow(new Rect(0, 0, 10000, 20));

        }
        public void drawGUI()
        {
            if (WindowVisible)
            {
                GUI.skin = HighLogic.Skin;
                windowPos = GUILayout.Window(WindowID, windowPos, WindowGUI, WindowTitle, GUILayout.MinWidth(300));
            }
        }

    }
}
