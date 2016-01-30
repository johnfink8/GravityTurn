using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP.IO;
using System.IO;
using UnityEngine;

namespace GravityTurn
{
    class DBEntry : IEquatable<DBEntry>, IComparable<DBEntry>
    {
        [Persistent]
        public double StartSpeed;
        [Persistent]
        public double APTimeStart;
        [Persistent]
        public double APTimeFinish;
        [Persistent]
        public double TurnAngle;
        [Persistent]
        public double Sensitivity;
        [Persistent]
        public double Roll;
        [Persistent]
        public double DestinationHeight;
        [Persistent]
        public double PressureCutoff;
        [Persistent]
        public double TotalLoss;
        [Persistent]
        public double MaxHeat;
        [Persistent]
        public bool LaunchSuccess;

        public int BetterThan(DBEntry other)
        {
            if (other.MaxHeat > 0.95 && other.MaxHeat > MaxHeat) // other overheated and we didn't
                return 1;
            if (!other.LaunchSuccess && LaunchSuccess) // other failed and we didn't
                return 1;
            return (other.TotalLoss > TotalLoss)?1:0;
        }

        public int CompareTo(DBEntry other)
        {
            if (other == null)
                return 1;
            else
                return this.BetterThan(other);
        }

        public bool Equals(DBEntry other)
        {
            return other.StartSpeed == StartSpeed && other.TurnAngle == TurnAngle;
        }

        public bool Equals(GravityTurner turner)
        {
            return StartSpeed == turner.StartSpeed && TurnAngle == turner.TurnAngle && MaxHeat == turner.MaxHeat && TotalLoss == turner.TotalLoss;
        }
    }

    class LaunchDB
    {
        GravityTurner turner;
        ConfigNode root = null;
        [Persistent]
        List<DBEntry> DB = new List<DBEntry>();

        public LaunchDB(GravityTurner inTurner)
        {
            turner = inTurner;
        }

        public bool GuessSettings(out double TurnAngle, out double StartSpeed)
        {
            // sort by most aggressive
            DB.Sort();
            TurnAngle = 0;
            StartSpeed = 0;
            if (DB.Count() == 0)
                return false;
            if (DB.Count() == 1)
            {
                if (DB[0].MaxHeat < 0.90)
                {
                    float Adjust = Mathf.Clamp((float)DB[0].MaxHeat + (float)(1 - DB[0].MaxHeat) / 2, 0.8f, 0.95f);
                    TurnAngle = DB[0].TurnAngle / Adjust;
                    StartSpeed = DB[0].StartSpeed * Adjust;
                }
                else if (DB[0].MaxHeat > 0.95)
                {
                    TurnAngle = DB[0].TurnAngle * 0.95;
                    StartSpeed = DB[0].StartSpeed * 1.05;
                }
                else
                {
                    TurnAngle = DB[0].TurnAngle;
                    StartSpeed = DB[0].StartSpeed;
                }
                return true;
            }

            // more than one result, now we can do real work

            // Simple linear progression 2nd best -> best -> next

            TurnAngle = DB[0].TurnAngle + DB[0].TurnAngle - DB[1].TurnAngle;
            StartSpeed = DB[0].StartSpeed + DB[0].StartSpeed - DB[1].StartSpeed;
            return true;
        }

        ///<summary>
        ///Get or create a new DBEntry based on angle and speed, so we don't have duplicates.
        ///</summary>
        DBEntry GetEntry()
        {
            foreach (DBEntry entry in DB)
            {
                if (entry.TurnAngle == turner.TurnAngle && entry.StartSpeed == turner.StartSpeed && entry.DestinationHeight==turner.DestinationHeight)
                    return entry;
            }
            DBEntry newentry = new DBEntry();
            DB.Add(newentry);
            return newentry;
        }

        ///<summary>
        ///Update or create a new entry in the DB for the current launch.
        ///</summary>
        public void RecordLaunch()
        {
            foreach (DBEntry entry in DB)
            {
                if (entry.Equals(turner))
                    return;
            }

            DBEntry newentry = GetEntry();
            newentry.TurnAngle = turner.TurnAngle;
            newentry.StartSpeed = turner.StartSpeed;
            newentry.TotalLoss = turner.TotalLoss;
            newentry.MaxHeat = turner.MaxHeat;
            newentry.DestinationHeight = turner.DestinationHeight;
            newentry.LaunchSuccess = GravityTurner.getVessel.orbit.ApA >= newentry.DestinationHeight * 1000;
        }

        public string GetFilename()
        {
            return IOUtils.GetFilePathFor(turner.GetType(), string.Format("gt_launchdb_{0}_{1}.cfg", turner.LaunchName, turner.LaunchBody.name));
        }

        public void Load()
        {
            try
            {
                root = ConfigNode.Load(GetFilename());
                if (root != null)
                {
                    if (ConfigNode.LoadObjectFromConfig(this, root))
                        GravityTurner.Log("Vessel DB loaded from {0}", GetFilename());
                    else
                        GravityTurner.Log("Vessel DB NOT loaded from {0}", GetFilename());
                }
            }
            catch (Exception ex)
            {
                GravityTurner.Log("Vessel DB Load error {0}" ,ex.ToString());
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GetFilename()));
            root = ConfigNode.CreateConfigFromObject(this);
            root.Save(GetFilename());
            GravityTurner.Log("Vessel DB saved to {0}", GetFilename());
        }
    }
}
