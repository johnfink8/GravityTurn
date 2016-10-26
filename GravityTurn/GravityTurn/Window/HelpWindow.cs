using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GravityTurn.Window
{
    public class HelpWindow : BaseWindow
    {
        public string helpWindowText = "";

        public HelpWindow(GravityTurner inTurner, int inWindowID)
            : base(inTurner,inWindowID)
        {
            WindowTitle = "GravityTurn Help";
        }

        public void Button(string helpMessage)
        {
            if (GUILayout.Button("?", GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false), GUILayout.MaxWidth(18), GUILayout.MinHeight(18)))
            {
                if (helpWindowText == helpMessage && WindowVisible)
                    WindowVisible = false;
                else
                    WindowVisible = true;
                helpWindowText = helpMessage;
            }
        }

        public override void WindowGUI(int windowID)
        {
            base.WindowGUI(windowID);
            GUILayout.BeginVertical();
            GUILayout.TextArea(helpWindowText);
            GUILayout.EndVertical();
        }
    }
}
