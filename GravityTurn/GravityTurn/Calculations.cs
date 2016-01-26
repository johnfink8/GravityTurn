using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GravityTurn
{
    public class Calculations
    {
        public static double TimeToReachAP(VesselState vesselstate, double StartSpeed, double TargetAPTime)
        {
            // gravityForce isn't force, it's acceleration
            double targetSpeed = vesselstate.gravityForce.magnitude * TargetAPTime;
            double timeToSpeed = (targetSpeed - StartSpeed) / vesselstate.maxVertAccel;
            return timeToSpeed;
        }
    }
}
