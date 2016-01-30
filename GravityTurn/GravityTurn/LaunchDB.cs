using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP.IO;
using System.IO;

namespace GravityTurn
{
    class DBEntry
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

        public DBEntry BestLaunch()
        {
            DBEntry best = null;
            foreach (DBEntry entry in DB)
            {
                if (best == null || (entry.MaxHeat < 0.95 && entry.TotalLoss < best.TotalLoss))
                    best = entry;
            }
            return best;
        }

        public void RecordLaunch()
        {
            foreach (DBEntry entry in DB)
            {
                if (entry.StartSpeed == turner.StartSpeed && entry.TurnAngle == turner.TurnAngle)
                    return;
            }

            DBEntry newentry = new DBEntry();
            newentry.TurnAngle = turner.TurnAngle;
            newentry.StartSpeed = turner.StartSpeed;
            newentry.TotalLoss = turner.TotalLoss;
            newentry.MaxHeat = turner.MaxHeat;
            DB.Add(newentry);
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
