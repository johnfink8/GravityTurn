using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace GravityTurn
{
    static class PartExtensions
    {
        public static bool IsUnfiredDecoupler(this Part p)
        {
            for (int i = 0; i < p.Modules.Count; i++)
            {
                PartModule m = p.Modules[i];
                ModuleDecouple mDecouple = m as ModuleDecouple;
                if (mDecouple != null)
                {
                    if (!mDecouple.isDecoupled && p.stagingOn) return true;
                    break;
                }

                ModuleAnchoredDecoupler mAnchoredDecoupler = m as ModuleAnchoredDecoupler;
                if (mAnchoredDecoupler != null)
                {
                    if (!mAnchoredDecoupler.isDecoupled && p.stagingOn) return true;
                    break;
                }

                if (m.ClassName == "ProceduralFairingDecoupler")
                {
                    return p.stagingOn;
                }
            }
            return false;
        }
        public static bool IsSepratron(this Part p)
        {
            return p.ActivatesEvenIfDisconnected
                && p.IsEngine()
                && p.IsDecoupledInStage(p.inverseStage)
                && !p.isControlSource;
        }
        public static bool IsEngine(this Part p)
        {
            for (int i = 0; i < p.Modules.Count; i++)
            {
                PartModule m = p.Modules[i];
                if (m is ModuleEngines) return true;
            }
            return false;
        }

        public static bool IsParachute(this Part p)
        {
            for (int i = 0; i < p.Modules.Count; i++)
            {
                if (p.Modules[i] is ModuleParachute) return true;
            }
            return false;
        }

        public static bool IsLaunchClamp(this Part p)
        {
            for (int i = 0; i < p.Modules.Count; i++)
            {
                if (p.Modules[i] is LaunchClamp) return true;
            }
            return false;
        }

        public static bool IsDecoupledInStage(this Part p, int stage)
        {
            if ((p.IsUnfiredDecoupler() || p.IsLaunchClamp()) && p.inverseStage == stage) return true;
            if (p.parent == null) return false;
            return p.parent.IsDecoupledInStage(stage);
        }
        public static bool EngineHasFuel(this Part p)
        {
            for (int i = 0; i < p.Modules.Count; i++)
            {
                PartModule m = p.Modules[i];
                ModuleEngines eng = m as ModuleEngines;
                if (eng != null) return !eng.getFlameoutState;

            }
            return false;
        }
    }
}
