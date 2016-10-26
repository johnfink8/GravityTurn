using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Reflection;

namespace GravityTurn
{
    class MechjebWrapper
    {
        System.Type CoreType;

        public bool Initialized = false;
        public static Vessel vessel { get { return FlightGlobals.ActiveVessel; } }
        PartModule core = null;

        bool GetCore()
        {
            foreach (Part p in vessel.parts)
            {
                foreach (PartModule module in p.Modules)
                {
                    if (module.GetType() == CoreType)
                    {
                        core = module;
                        return true;
                    }
                }

            }
            return false;
        }

        System.Type FindMechJebModule(string module)
        {
            Type type = null;
            AssemblyLoader.loadedAssemblies.TypeOperation(t =>
            {
                if (t.FullName == module)
                {
                    type = t;
                }
            });

            return type;
        }

        public bool init()
        {
            if (Initialized)
                return true;
            CoreType = FindMechJebModule("MuMech.MechJebCore");

            if (CoreType == null)
            {
                GravityTurner.Log("MechJeb assembly not found");
                return false;
            }
            if (!GetCore())
            {
                GravityTurner.Log("MechJeb core not found");
                return false;
            }
            GravityTurner.Log("Found MechJeb core");
            Initialized = true;
            return true;
        }

        public void ExecuteNode()
        {
            var coreNodeInfo = CoreType.GetField("node");
            var coreNode = coreNodeInfo.GetValue(core);
            var NodeExecute = coreNode.GetType().GetMethod("ExecuteOneNode", BindingFlags.Public | BindingFlags.Instance);
            NodeExecute.Invoke(coreNode, new object[] { this });
        }

        public void CircularizeAtAP()
        {
            double UT = Planetarium.GetUniversalTime();
            UT += vessel.orbit.timeToAp;
            System.Type OrbitalManeuverCalculatorType = FindMechJebModule("MuMech.OrbitalManeuverCalculator");

            MethodInfo CircularizeMethod = OrbitalManeuverCalculatorType.GetMethod("DeltaVToCircularize", BindingFlags.Public | BindingFlags.Static);
            Vector3d deltav = (Vector3d)CircularizeMethod.Invoke(null, new object[] { vessel.orbit, UT });
            GravityTurner.Log(string.Format("Circularization burn {0:0.0} m/s", deltav.magnitude));
            vessel.PlaceManeuverNode(vessel.orbit, deltav, UT);
            ExecuteNode();
        }


    }
}
