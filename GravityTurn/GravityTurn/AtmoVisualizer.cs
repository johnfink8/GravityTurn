using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.IO;
using KSP.IO;


namespace GravityTurn
{
    class AtmoVisualizer
    {

        static void Dot(Texture2D texture, int size, int x, int y, Color color)
        {
            for (int i = x - size / 2; i < x + size / 2; i++)
            {
                if (i < 0 || i > texture.width)
                    continue;
                for (int j = y - size / 2; j < y + size / 2; j++)
                {
                    if (j < 0 || j > texture.height)
                        continue;
                    texture.SetPixel(i, j, color);
                }
            }
        }

        public static void GenerateAtmoMap(CelestialBody b, string filename)
        {
            Texture2D texture=new Texture2D(500,500);
            for (int i = 1; i < b.atmosphereDepth; i += (int)b.atmosphereDepth / 500)
            {
               Dot(texture,6,500*i/(int)b.atmosphereDepth, 500*(int)b.atmospherePressureCurve.Evaluate(i)/(int)b.atmospherePressureSeaLevel,new Color(0, 0, 1));
            }
            texture.Apply();
            System.IO.File.WriteAllBytes(filename,texture.EncodeToPNG());
        }

        public static void GenerateTempMap(CelestialBody b, string filename)
        {
            Texture2D texture = new Texture2D(360, 500);
            float maxtemp = (float)b.atmosphereTemperatureSeaLevel + b.atmosphereTemperatureSunMultCurve.Evaluate(0)
                              * (b.latitudeTemperatureBiasCurve.Evaluate(0)
                                 + b.latitudeTemperatureSunMultCurve.Evaluate(0)
                                 + b.axialTemperatureSunMultCurve.Evaluate(0));
            float mintemp = maxtemp;
            for (int i = 1; i < b.atmosphereDepth; i += (int)b.atmosphereDepth / 500)
            {
                for (int j = -180; j < 180; j++)
                {
                    // min/max is a little unpredictable, so we have to do this twice
                    float temp = b.atmosphereTemperatureCurve.Evaluate(i);
                    temp += b.atmosphereTemperatureSunMultCurve.Evaluate((float)i)
                              * (b.latitudeTemperatureBiasCurve.Evaluate((float)j)
                                 + b.latitudeTemperatureSunMultCurve.Evaluate((float)j)
                                 + b.axialTemperatureSunMultCurve.Evaluate(0));
                    if (temp < mintemp)
                        mintemp = temp;
                    else if (temp > maxtemp)
                        maxtemp = temp;
                }
            }
            for (int i = 1; i < b.atmosphereDepth; i += (int)b.atmosphereDepth / 500)
            {
                for (float j = -90; j < 90; j+=0.5f)
                {
                    float temp = b.atmosphereTemperatureCurve.Evaluate(i);
                    temp += b.atmosphereTemperatureSunMultCurve.Evaluate((float)i)
                              * (b.latitudeTemperatureBiasCurve.Evaluate(j)
                                 + b.latitudeTemperatureSunMultCurve.Evaluate(j)
                                 + b.axialTemperatureSunMultCurve.Evaluate(0));
                    float pc = (temp-mintemp) / (maxtemp - mintemp);
                    texture.SetPixel((int)(j*2 + 180), 500 * i / (int)b.atmosphereDepth, new Color(pc, 0, 1 - pc));
                }
            }
            texture.Apply();
            System.IO.File.WriteAllBytes(filename, texture.EncodeToPNG());
        }
    }
}
