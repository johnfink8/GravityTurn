using System;
using System.Collections.Generic;
using System.Text;
//using System.Threading.Tasks;
using UnityEngine;

namespace GravityTurn
{
    static class PartExtensions
    {
        public static bool HasModule<T>(this Part part) where T : PartModule
        {
            for (int i = 0; i < part.Modules.Count; i++)
            {
                if (part.Modules[i] is T)
                    return true;
            }
            return false;
        }
        public static T GetModule<T>(this Part part) where T : PartModule
        {
            for (int i = 0; i < part.Modules.Count; i++)
            {
                PartModule pm = part.Modules[i];
                T module = pm as T;
                if (module != null)
                    return module;
            }
            return null;
        }
        // An allocation free version of GetModuleMass
        public static float GetModuleMassNoAlloc(this Part p, float defaultMass, ModifierStagingSituation sit)
        {
            float mass = 0f;

            for (int i = 0; i < p.Modules.Count; i++)
            {
                IPartMassModifier m = p.Modules[i] as IPartMassModifier;
                if (m != null)
                {
                    mass += m.GetModuleMass(defaultMass, sit);
                }
            }
            return mass;
        }
        public static bool IsUnfiredDecoupler(this Part p)
        {
            bool isFairing = false;
            for (int i = 0; i < p.Modules.Count; i++)
            {
                PartModule m = p.Modules[i];

                ModuleDecouple mDecouple = m as ModuleDecouple;
                if (mDecouple != null)
                {
                    if (!mDecouple.isDecoupled && mDecouple.stagingEnabled && p.stagingOn) return true;
                    break;
                }

                ModuleAnchoredDecoupler mAnchoredDecoupler = m as ModuleAnchoredDecoupler;
                if (mAnchoredDecoupler != null)
                {
                    if (!mAnchoredDecoupler.isDecoupled && mAnchoredDecoupler.stagingEnabled && p.stagingOn) return true;
                    break;
                }

                ModuleDockingNode mDockingNode = m as ModuleDockingNode;
                if (mDockingNode != null)
                {
                    if (mDockingNode.staged && mDockingNode.stagingEnabled && p.stagingOn) return true;
                    break;
                }

                if (m is ModuleProceduralFairing)
                    isFairing = true;

                if (m is ModuleCargoBay && isFairing)
                {
                    ModuleCargoBay fairing = m as ModuleCargoBay;
                    if (fairing.ClosedAndLocked() && m.stagingEnabled && p.stagingOn) return true;
                        break;
                }

                if (VesselState.isLoadedProceduralFairing && m.moduleName == "ProceduralFairingDecoupler")
                {
                    if (!m.Fields["decoupled"].GetValue<bool>(m) && m.stagingEnabled &&  p.stagingOn) return true;
                    break;
                }
            }
            return false;
        }
        public static bool IsSepratron(this Part p)
        {
            return p.ActivatesEvenIfDisconnected
                && p.IsEngine()
                && p.IsDecoupledInStage(p.inverseStage)
                && p.isControlSource == Vessel.ControlLevel.NONE;
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

        public static bool IsFuelTank(this Part p)
        {
            for (int i = 0; i < p.Resources.Count; i++)
            {
                PartResource r = p.Resources[i];
                if (r.resourceName == "LiquidFuel" || r.resourceName == "SolidFuel")
                    return true;
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

        public static bool IsFairing(this Part p)
        {
            for (int i = 0; i < p.Modules.Count; i++)
            {
                if (p.Modules[i] is ModuleProceduralFairing) return true;
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
        public static bool FuelTankHasFuel(this Part p)
        {
            for (int i = 0; i < p.Resources.Count; i++)
            {
                PartResource r = p.Resources[i];
                if (r.resourceName == "LiquidFuel" || r.resourceName == "SolidFuel")
                    return r.amount > 0;
            }
            return false;
        }
        public static void DeployFairing(this Part p)
        {
            for (int i = 0; i < p.Modules.Count; i++)
            {
                PartModule m = p.Modules[i];
                ModuleProceduralFairing fairing = m as ModuleProceduralFairing;
                if (fairing != null)
                    fairing.DeployFairing();

            }
        }

        public static double CriticalHeat(this Part p)
        {
            double skin = p.skinTemperature / p.skinMaxTemp;
            double intern = p.temperature / p.maxTemp;
            if (skin > intern)
                return skin;
            return intern;
        }

        public static Rigidbody rigidbody(this Part p)
        {
            return p.GetComponent<Rigidbody>();
        }

    }
}
