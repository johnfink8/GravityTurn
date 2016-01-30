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

        public static float APThrottle(double timeToAP, GravityTurner turner)
        {
            Vessel vessel = GravityTurner.getVessel;
            if (vessel.speed < turner.StartSpeed)
                turner.Throttle.value = 1.0f;
            else
            {
                if (timeToAP > vessel.orbit.timeToPe) // We're falling
                    timeToAP = 0;
                float diff = 0.1f * (float)Math.Abs(turner.HoldAPTime - timeToAP) * turner.Sensitivity;
                turner.TimeSpeed = (turner.PrevTime - timeToAP) / (Time.time - turner.lastTimeMeasured);
                if (Math.Abs(turner.TimeSpeed) < 0.02 && turner.PitchAdjustment == 0)
                    turner.NeutralThrottle = (float)turner.Throttle.value;
                if (Math.Abs(timeToAP - turner.HoldAPTime) < 0.1)
                {
                    if (turner.PitchAdjustment > 0)
                        turner.PitchAdjustment.value -= 0.1f;
                    else
                        turner.Throttle.force(turner.NeutralThrottle);
                }
                else if (timeToAP < turner.HoldAPTime)
                {
                    if (turner.Throttle.value >= 1 && (timeToAP < turner.PrevTime || (timeToAP - turner.HoldAPTime) / turner.TimeSpeed > 20))
                    {
                        turner.NeutralThrottle = 1;
                        turner.PitchAdjustment.value += 0.1f;
                    }
                    turner.Throttle.value += diff;

                    if (0 < (timeToAP - turner.HoldAPTime) / turner.TimeSpeed && (timeToAP - turner.HoldAPTime) / turner.TimeSpeed < 20)  // We will reach desired AP time in <20 second
                    {
                        turner.PitchAdjustment.value -= 0.1f;
                    }
                }
                else if (timeToAP > turner.HoldAPTime)
                {
                    if (turner.PitchAdjustment > 0)
                        turner.PitchAdjustment.value -= 0.1f;
                    else
                        turner.Throttle.value -= diff;
                }
            }
            if (turner.PitchAdjustment < 0)
                turner.PitchAdjustment.value = 0;
            if (turner.PitchAdjustment > MaxAngle(vessel,turner))
                turner.PitchAdjustment.value = MaxAngle(vessel,turner);

            // We don't want to do any pitch correction during the initial lift
            if (vessel.ProgradePitch(true) < -45)
                turner.PitchAdjustment.force(0);
            turner.PrevTime = vessel.orbit.timeToAp;
            turner.lastTimeMeasured = Time.time;
            if (turner.Throttle.value < turner.Sensitivity)
                turner.Throttle.force(turner.Sensitivity);
            if (turner.Throttle.value > 1)
                turner.Throttle.force(1);
            return (float)turner.Throttle.value;
        }

        public static float MaxAngle(Vessel vessel, GravityTurner turner)
        {
            float angle = 100000 / (float)turner.vesselState.dynamicPressure;
            float vertical = 90 + vessel.Pitch();
            angle = Mathf.Clamp(angle, 0, 35);
            if (angle > vertical)
                return vertical;
            return angle;
        }


    }
}
