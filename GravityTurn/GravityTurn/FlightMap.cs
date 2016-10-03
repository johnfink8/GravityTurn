using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GravityTurn
{
    public class FlightMap
    {
        public Texture2D texture;
        GravityTurner turner;

        public FlightMap(GravityTurner turner,int width=800,int height=400)
        {
            this.turner = turner; 
            texture = new Texture2D(width, height);
            for (int x = 0; x < texture.width; x++)
                for (int y = 0; y < texture.height; y++)
                    texture.SetPixel(x, y, Color.black);
            texture.Apply();
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


        public void WriteParameters(float TurnAngle, float StartSpeed)
        {
            texture.SetPixel(texture.width, texture.height, new Color(TurnAngle / 90, StartSpeed / 200, 0));
            texture.Apply();
        }

        public void WriteResults(double DragLoss, double GravityDragLoss, double VectorLoss)
        {
            float r = (float)(DragLoss / 2000);
            float g = (float)(GravityDragLoss / 5000);
            float b = (float)(VectorLoss / 5000);
            texture.SetPixel(texture.width-1, texture.height, new Color(r,g,b));
            texture.Apply();
        }

        public float TotalLoss()
        {
            Color color = texture.GetPixel(texture.width - 1, texture.height);
            float OldDrag = color.r * 2000;
            float OldGrav = color.g * 5000;
            float OldVec = color.b * 5000;
            return OldDrag + OldGrav + OldVec;
        }

        public void  GetSettings(out float TurnAngle, out float StartSpeed)
        {
            Color color = texture.GetPixel(texture.width, texture.height);
            TurnAngle = color.r * 90;
            StartSpeed = color.g * 200;
        }

        public bool SameSettings(float inTurnAngle, float inStartSpeed)
        {
            float TurnAngle, StartSpeed;
            GetSettings(out TurnAngle, out StartSpeed);
            return TurnAngle == inTurnAngle && StartSpeed == inStartSpeed;
        }

        public bool BetterResults(double DragLoss, double GravityDragLoss, double VectorLoss)
        {
            if (TotalLoss() == 0)
                return true;
            return (MaxHeat() > 0.95 || TotalLoss() > DragLoss + GravityDragLoss + VectorLoss);
        }

        public float TurnAngle()
        {
            return texture.GetPixel(texture.width, texture.height).r * 90;
        }

        public float StartSpeed()
        {
            return texture.GetPixel(texture.width, texture.height).g * 200;
        }

        public void Save(string filename)
        {
            System.IO.File.WriteAllBytes(filename, texture.EncodeToPNG());
        }

        public static FlightMap Load(string filename, GravityTurner turner)
        {
            try
            {
                Texture2D texture = new Texture2D(800, 400);
                texture.LoadImage(System.IO.File.ReadAllBytes(filename));
                FlightMap flightmap = new FlightMap(turner, texture.width, texture.height);
                flightmap.texture = texture;
                GravityTurner.Log("FlightMap loaded with {0:0.00} loss", args:flightmap.TotalLoss());
                return flightmap;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public float MaxHeat()
        {
            float max = 0;
            for (int x = 0; x < texture.width; x++)
            {
                for (int y = 0; y < texture.height; y++)
                {
                    if (texture.GetPixel(x, y).r > max)
                        max = texture.GetPixel(x, y).r;
                }
            }
            GravityTurner.Log("Previous max heat {0:0.000}", max);
            return max;
        }

    }
}
