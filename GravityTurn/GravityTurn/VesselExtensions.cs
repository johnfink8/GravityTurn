using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP.UI.Screens;

namespace GravityTurn
{
    public static class VesselExtensions
    {
        public static bool StageHasSolidEngine(this Vessel vessel, int inverseStage)
        {
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                if (part.inverseStage == inverseStage)
                {
                    for (int m = 0; m < part.Modules.Count; m++)
                    {
                        var engine = part.Modules[m] as ModuleEngines;
                        if (engine && engine.engineType == EngineType.SolidBooster)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static bool IsInStableOrbit(this Vessel v)
        {
            if (v.orbit.ApA > v.StableOrbitHeight() && v.orbit.PeA > v.StableOrbitHeight())
                return true;

            return false;
        }
        public static double StableOrbitHeight(this Vessel v)
        {
            if (v.mainBody.atmosphere)
                return v.mainBody.atmosphereDepth;
            else
                return v.mainBody.timeWarpAltitudeLimits[1];
        }

        public static Vector3d up(this Vessel vessel)
        {
            return (vessel.CoM - vessel.mainBody.position).normalized;
        }

        public static float DragCubeCoefForward(this Vessel vessel)
        {
            float coef = 0;
            foreach (Part p in vessel.parts)
                coef += p.DragCubes.GetCubeCoeffDir(vessel.up())*p.DragCubes.GetCubeAreaDir(vessel.up());
            return coef;
        }

        public static Vector3d horizontal(this Vessel vessel, bool surface = true)
        {
            if (surface)
                return Vector3d.Exclude(vessel.up(), vessel.srf_velocity.normalized);
            else
                return Vector3d.Exclude(vessel.up(), vessel.obt_velocity.normalized);
        }

        public static float ProgradePitch(this Vessel vessel, bool surface = true)
        {
            if (surface)
                return -(float)Vector3.Angle(vessel.horizontal(surface), vessel.srf_velocity.normalized);
            else
                return -(float)Vector3.Angle(vessel.horizontal(surface), vessel.obt_velocity.normalized);
        }

        public static Vector3 forward(this Vessel v)
        {
            return v.GetTransform().up;
        }

        public static float Pitch(this Vessel v)
        {
            return -Vector3.Angle(v.horizontal(true), v.forward());   
        }

        public static Part CriticalHeatPart(this Vessel v)
        {
            Part hottest = null;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (hottest == null || p.CriticalHeat() > hottest.CriticalHeat())
                    hottest = p;
            }
            return hottest;
        }

        public static bool LiftedOff(this Vessel v)
        {
            return !(v.Landed || v.Splashed);
        }

        public static Rigidbody rigidbody(this Vessel v)
        {
            return v.GetComponent<Rigidbody>();
        }

        public static ManeuverNode PlaceManeuverNode(this Vessel vessel, Orbit patch, Vector3d dV, double UT)
        {
            //placing a maneuver node with bad dV values can really mess up the game, so try to protect against that
            //and log an exception if we get a bad dV vector:
            for (int i = 0; i < 3; i++)
            {
                if (double.IsNaN(dV[i]) || double.IsInfinity(dV[i]))
                {
                    throw new Exception("VesselExtensions.PlaceManeuverNode: bad dV: " + dV);
                }
            }

            if (double.IsNaN(UT) || double.IsInfinity(UT))
            {
                throw new Exception("VesselExtensions.PlaceManeuverNode: bad UT: " + UT);
            }

            //It seems that sometimes the game can freak out if you place a maneuver node in the past, so this
            //protects against that.
            UT = Math.Max(UT, Planetarium.GetUniversalTime());

            //convert a dV in world coordinates into the coordinate system of the maneuver node,
            //which uses (x, y, z) = (radial+, normal-, prograde)
            Vector3d nodeDV = patch.DeltaVToManeuverNodeCoordinates(UT, dV);
            ManeuverNode mn = vessel.patchedConicSolver.AddManeuverNode(UT);
            mn.DeltaV = nodeDV;
            vessel.patchedConicSolver.UpdateFlightPlan();
            return mn;
        }

        // 0.90 added a building upgrade to unlock Orbit visualization and patched conics
        // Unfortunately when patchedConics are disabled vessel.patchedConicSolver is null
        // So we need to add a lot of sanity check and/or disable modules
        public static bool patchedConicsUnlocked(this Vessel vessel)
        {
            return GameVariables.Instance.GetOrbitDisplayMode(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation)) == GameVariables.OrbitDisplayMode.PatchedConics;
        }

        public static bool HasActiveSRB(this Vessel vessel)
        {
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                if (part.isActiveAndEnabled)
                {
                    for (int m = 0; m < part.Modules.Count; m++)
                    {
                        var engine = part.Modules[m] as ModuleEngines;
                        if (engine != null && engine.engineType == EngineType.SolidBooster && engine.propellantReqMet>0)
                            return true;
                    }
                }
            }
            return false;
        }
    }
}
