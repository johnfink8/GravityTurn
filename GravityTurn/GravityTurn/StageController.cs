using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;
using Smooth.Slinq;

namespace GravityTurn
{
    public class StageController
    {
        public StageController(GravityTurner turner)

        {
            this.turner = turner;
        }

        GravityTurner turner = null;
        public static Vessel vessel { get { return FlightGlobals.ActiveVessel; } }
        private VesselState vesselState { get { return turner.vesselState; } }
        //adjustable parameters:

        static public bool topFairingDeployed = false;

        //internal state:
        double lastStageTime = 0;

        bool countingDown = false;
        double stageCountdownStart = 0;

        public void Update()
        {
            if (!vessel.isActiveVessel)
                return;

            GravityTurner.DebugMessage = "StageController is active\n";

            //if autostage enabled, and if we are not waiting on the pad, and if there are stages left,
            //and if we are allowed to continue staging, and if we didn't just fire the previous stage
            if (!vessel.LiftedOff() || StageManager.CurrentStage <= 0 || StageManager.CurrentStage <= turner.autostageLimit
               || Math.Abs(vesselState.time - lastStageTime) < turner.autostagePostDelay)
                return;

            GravityTurner.DebugMessage += "  Lifted off\n";
             
            //only decouple fairings if the dynamic pressure and altitude conditions are respected
            if (!topFairingDeployed)
            {
                Part fairing = GetTopmostFairing(vessel);
                if (fairing == null)
                    GravityTurner.DebugMessage += "  no top fairing\n";
                
                if (fairing != null && fairing.IsUnfiredDecoupler() && (vesselState.dynamicPressure < turner.FairingPressure && Math.Abs(vesselState.dynamicPressure - vesselState.maxQ) > 0.1) && (vesselState.maxQ > vessel.mainBody.atmospherePressureSeaLevel/2))
                {
                    topFairingDeployed = true;
                    fairing.DeployFairing();
                    GravityTurner.Log("Top Fairing deployed.");
                    GravityTurner.Log("  fairing pressure: {0:0.0}", turner.FairingPressure);
                    GravityTurner.Log("  dynamic pressure: {0:0.0}", vesselState.dynamicPressure);
                    GravityTurner.Log("  vessel maxQ: {0:0.0}", vesselState.maxQ);
                    GravityTurner.DebugMessage += "  Deploying top Fairing!!!\n";
                    return;
                }
            }

            //don't decouple active or idle engines or tanks
            List<int> burnedResources = FindBurnedResources();
            if (InverseStageDecouplesActiveOrIdleEngineOrTank(StageManager.CurrentStage - 1, vessel, burnedResources))
                return;

            GravityTurner.DebugMessage += "  active/idle Engine\n";

            //Don't fire a stage that will activate a parachute, unless that parachute gets decoupled:
            if (HasStayingChutes(StageManager.CurrentStage - 1, vessel))
                return;

            GravityTurner.DebugMessage += "  HasStayingChute\n";

            //only fire decouplers to drop deactivated engines or tanks
            bool firesDecoupler = InverseStageFiresDecoupler(StageManager.CurrentStage - 1, vessel);
            if (firesDecoupler && !InverseStageDecouplesDeactivatedEngineOrTank(StageManager.CurrentStage - 1, vessel))
                return;

            GravityTurner.DebugMessage += "  deactivated Engine/Tank\n";

            //When we find that we're allowed to stage, start a countdown (with a
            //length given by autostagePreDelay) and only stage once that countdown finishes,
            if (countingDown)
            {
                GravityTurner.DebugMessage += "  Counting down\n";
                if (Math.Abs(vesselState.time - stageCountdownStart) > turner.autostagePreDelay)
                {
                    GravityTurner.DebugMessage += "    Countdown finished\n";
                    if (firesDecoupler)
                    {
                        //if we decouple things, delay the next stage a bit to avoid exploding the debris
                        lastStageTime = vesselState.time;
                    }
                    GravityTurner.DebugMessage += "    ActivateNextStage\n";
                    GravityTurner.Log("Activate next stage.");
                    StageManager.ActivateNextStage();
                    countingDown = false;
                    GravityTurner.RestoreTimeWarp();
                }
            }
            else
            {
                GravityTurner.DebugMessage += "  Stage Countdown\n";
                GravityTurner.StoreTimeWarp();
                GravityTurner.StopSpeedup();
                stageCountdownStart = vesselState.time;
                countingDown = true;
            }
        }

        //determine whether it's safe to activate inverseStage
        public static bool InverseStageDecouplesActiveOrIdleEngineOrTank(int inverseStage, Vessel v, List<int> tankResources)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p.inverseStage == inverseStage && p.IsUnfiredDecoupler() && HasActiveOrIdleEngineOrTankDescendant(p, tankResources))
                {
                    return true;
                }
            }
            return false;
        }

        // Find resources burned by engines that will remain after StageManager (so we wait until tanks are empty before releasing drop tanks)
        bool PartIsEngine(Part p)
        {
            return p.inverseStage >= StageManager.CurrentStage && p.IsEngine() && !p.IsSepratron() &&
                !p.IsDecoupledInStage(StageManager.CurrentStage - 1);
        }
        bool IsEnabledEngine(ModuleEngines e)
        {
            return e.isEnabled;
        }
        ModuleEngines EnabledEngine(Part p)
        {
            for (int i = 0; i < p.Modules.Count; i++)
            {
                PartModule m = p.Modules[i];
                if (m is ModuleEngines
                    && IsEnabledEngine(m as ModuleEngines))
                {
                    return m as ModuleEngines;
                }
            }
            return null;
        }
        List<Part> GetEnginesOfVessel(Vessel v)
        {
            var engines = new List<Part>();

            for (int i = 0; i < v.Parts.Count; i++)
            {
                if (PartIsEngine(v.Parts[i]))
                    engines.Add(v.Parts[i]);
            }
            return engines;
        }
        List<ModuleEngines> GetEnabledEnginesOfVessel(Vessel v)
        {
            var engineModules = new List<ModuleEngines>();
            for (int i = 0; i < v.Parts.Count; i++)
            {
                Part p = v.Parts[i];
                if (PartIsEngine(p) && EnabledEngine(p))
                    engineModules.Add(EnabledEngine(p));
            }
            return engineModules;
        }


        public List<int> FindBurnedResources()
        {
            var engineModules = GetEnabledEnginesOfVessel(vessel);

            var propellantIDs = new List<int>();
            for (int eng = 0; eng < engineModules.Count; eng++)
            {
                var e = engineModules[eng];
                for (int i = 0; i < e.propellants.Count; i++)
                {
                    propellantIDs.Add(e.propellants[i].id);
                }
            }

            return propellantIDs;
        }

        //detect if a part is above an active or idle engine in the part tree
        public static bool HasActiveOrIdleEngineOrTankDescendant(Part p, List<int> tankResources)
        {
            if ((p.State == PartStates.ACTIVE || p.State == PartStates.IDLE)
                && p.IsEngine() && !p.IsSepratron() && p.EngineHasFuel())
            {
                return true; // TODO: properly check if ModuleEngines is active
            }

            if (!p.IsSepratron())
            {
                for (int i = 0; i < p.Resources.Count; i++)
                {
                    PartResource r = p.Resources[i];
                    if (r.amount > 0 && r.info.name != "ElectricCharge" && tankResources.Contains(r.info.id))
                    {
                        return true;
                    }
                }
            }
            for (int i = 0; i < p.children.Count; i++)
            {
                if (HasActiveOrIdleEngineOrTankDescendant(p.children[i], tankResources))
                {
                    return true;
                }
            }
            return false;
        }

        //determine whether activating inverseStage will fire any sort of decoupler. This
        //is used to tell whether we should delay activating the next stage after activating inverseStage
        public static bool InverseStageFiresDecoupler(int inverseStage, Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p.inverseStage == inverseStage && p.IsUnfiredDecoupler())
                {
                    return true;
                }
            }
            return false;
        }

        //determine whether inverseStage sheds a dead engine
        public static bool InverseStageDecouplesDeactivatedEngineOrTank(int inverseStage, Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p.inverseStage == inverseStage && p.IsUnfiredDecoupler() && HasDeactivatedEngineOrTankDescendant(p))
                {
                    return true;
                }
            }
            return false;
        }

        //detect if a part is above a deactivated engine or fuel tank
        public static bool HasDeactivatedEngineOrTankDescendant(Part p)
        {
            if ((p.State == PartStates.DEACTIVATED) && (p.IsFuelTank() || p.IsEngine()) && !p.IsSepratron())
            {
                return true; // TODO: yet more ModuleEngine lazy checks
            }

            //check if this is a new-style fuel tank that's run out of resources:
            bool hadResources = false;
            bool hasResources = false;
            for (int i = 0; i < p.Resources.Count; i++)
            {
                PartResource r = p.Resources[i];
                if (r.resourceName == "ElectricCharge") continue;
                if (r.maxAmount > 0) hadResources = true;
                if (r.amount > 0) hasResources = true;
            }
            if (hadResources && !hasResources) return true;

            if (p.IsEngine() && !p.EngineHasFuel()) return true;

            for (int i = 0; i < p.children.Count; i++)
            {
                if (HasDeactivatedEngineOrTankDescendant(p.children[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool HasFairing(int inverseStage, Vessel v)
        {
            foreach (Part p in v.parts)
            {
                if (p.inverseStage == inverseStage &&
                    (p.HasModule<ModuleProceduralFairing>() || (p.FindModulesImplementing<ModuleProceduralFairing>().Count > 0 && p.Modules.Contains("ProceduralFairingDecoupler"))))
                    return true;
            }
            return false;
        }
        public static Part GetTopmostFairing(Vessel v)
        {
            foreach (Part p in v.parts.Slinq().OrderBy(o => o.inverseStage).ToList())
            {
                if (p.HasModule<ModuleProceduralFairing>() || (p.FindModulesImplementing<ModuleProceduralFairing>().Count > 0 && p.Modules.Contains("ProceduralFairingDecoupler")))
                    return p;
            }
            return null;
        }

        public static bool HasStayingFairing(int inverseStage, Vessel v)
        {
            foreach (Part p in v.parts)
            {
                if (p.inverseStage == inverseStage &&
                    !p.IsDecoupledInStage(inverseStage) &&
                    (p.HasModule<ModuleProceduralFairing>() || (p.FindModulesImplementing<ModuleProceduralFairing>().Count > 0 && p.Modules.Contains("ProceduralFairingDecoupler"))))
                    return true;
            }
            return false;
        }

        //determine if there are chutes being fired that wouldn't also get decoupled
        public static bool HasStayingChutes(int inverseStage, Vessel v)
        {
            var chutes = v.parts.FindAll(p => p.inverseStage == inverseStage && p.IsParachute());

            for (int i = 0; i < chutes.Count; i++)
            {
                Part p = chutes[i];
                if (!p.IsDecoupledInStage(inverseStage))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
