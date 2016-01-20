using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GravityTurn
{
    class FlightMap
    {
        public Texture2D texture;
        GravityTurner turner;
        public Rect windowPos;
        public bool visible = false;

        public FlightMap(GravityTurner turner,int width=800,int height=400)
        {
            this.turner = turner;
            texture = new Texture2D(width, height);
            for (int x = 0; x < texture.width; x++)
                for (int y = 0; y < texture.height; y++)
                    texture.SetPixel(x, y, Color.black);
            texture.Apply();
            windowPos = new Rect(Screen.width/2-width/2, 100, width, height);
        }

        public void UpdateMap(Vessel vessel)
        {
            double yScale;
            if (vessel.mainBody.atmosphere)
                yScale = vessel.mainBody.atmosphereDepth;
            else
                yScale = turner.DestinationHeight * 1000;
            double MetersPerPixel = yScale / texture.height;
            double x = turner.HorizontalDistance / MetersPerPixel % texture.width;
            double y = vessel.altitude / MetersPerPixel % texture.height;
            float red = (float)(vessel.CriticalHeatPart().CriticalHeat());
            float green = (float)turner.vesselState.drag / 5;
            float blue = (float)(turner.vesselState.thrustCurrent / turner.MaxThrust);
            texture.SetPixel((int)x, (int)y, new Color(red, green, blue));
            texture.Apply();
        }

        public void WindowGUI(int windowID)
        {
            GUIStyle mySty = new GUIStyle();
            mySty.normal.textColor = mySty.focused.textColor = Color.white;
            mySty.fontSize = 20;
            mySty.fontStyle = FontStyle.Bold;
            if (GUI.Button(new Rect(windowPos.width - 18, 2, 16, 16), "X"))
                visible = false;
            GUILayout.Box(texture);
            Vector2 pivotPoint = new Vector2(windowPos.width-25,windowPos.height / 2 - 30);
            GUIUtility.RotateAroundPivot(-90, pivotPoint);
            GUI.Label(new Rect(windowPos.width-80,  windowPos.height / 2-40,80,20),"Altitude",mySty);
            GUIUtility.RotateAroundPivot(90, pivotPoint);
            GUI.Label(new Rect(windowPos.width/2-80, windowPos.height - 25, 160, 20), "Horizontal Distance",mySty);
            GUI.DragWindow(new Rect(0, 0, 10000, 2000));
        }

    }
}
