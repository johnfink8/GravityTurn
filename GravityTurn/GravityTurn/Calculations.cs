using System;
using System.Collections.Generic;
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
        static float maxAoA = 0;
        public static float APThrottle(double timeToAP, GravityTurner turner)
        {
            Vessel vessel = GravityTurner.getVessel;
            GravityTurner.DebugMessage += "-\n";
            if (vessel.speed < turner.StartSpeed)
                turner.Throttle.value = 1.0f;
            else
            {
                if (timeToAP > vessel.orbit.timeToPe) // We're falling
                    timeToAP = 0;
                float diff = 0.1f * (float)Math.Abs(turner.HoldAPTime - timeToAP) * 0.5f;
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
                if (Math.Abs(maxAoA) < Math.Abs(turner.vesselState.AoA))
                    maxAoA = turner.vesselState.AoA;

                GravityTurner.DebugMessage += String.Format("max Angle of Attack: {0:0.00}\n", maxAoA);
                GravityTurner.DebugMessage += String.Format("cur Angle of Attack: {0:0.00}\n", turner.vesselState.AoA.value);
                GravityTurner.DebugMessage += String.Format("-\n");

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

            // calculate Yaw correction for inclination
            if (vessel.ProgradePitch(true) > -45 
                && Math.Abs(turner.Inclination) > 2
                && turner.program != GravityTurner.AscentProgram.InLaunch)
            {
                float heading = (Mathf.Sign(turner.Inclination) * (float)turner.vesselState.orbitInclination.value - turner.Inclination);
                GravityTurner.DebugMessage += String.Format("  Heading: {0:0.00}\n", heading);
                heading *= 1.2f;
                if (Math.Abs(heading) < 0.3)
                    heading = 0;
                else if (Mathf.Abs(turner.YawAdjustment) > 0.1)
                    heading = (turner.YawAdjustment*7.0f + heading)/8.0f;

                if (Mathf.Abs(turner.YawAdjustment) > Mathf.Abs(heading) || turner.YawAdjustment == 0.0)
                    turner.YawAdjustment = heading;
                GravityTurner.DebugMessage += String.Format("  YawCorrection: {0:0.00}\n", turner.YawAdjustment);
            }
            else
                turner.YawAdjustment = 0;

            // Inrease the AP time if needed for SRB lifter stages
            if (vessel.HasActiveSRB() && vessel.orbit.timeToAp > turner.HoldAPTime && turner.TimeSpeed < 0)
            {
                double StopHeight = GravityTurner.getVessel.mainBody.atmosphereDepth;
                if (StopHeight <= 0)
                    StopHeight = turner.DestinationHeight * 1000;
                turner.APTimeStart = (StopHeight * vessel.orbit.timeToAp - vessel.altitude * turner.APTimeFinish) / (StopHeight - vessel.altitude);
                turner.APTimeStart *= 0.99; // We want to be just a bit less than what we calculate, so we don't stay throttled up
            }

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

        public static double CircularOrbitSpeed(CelestialBody body, double radius)
        {
            return Math.Sqrt(body.gravParameter / radius);
        }

        //Computes the deltaV of the burn needed to circularize an orbit.
        public static Vector3d DeltaVToCircularize(Orbit o)
        {
            double UT = Planetarium.GetUniversalTime();
            UT += o.timeToAp;

            Vector3d desiredVelocity = CircularOrbitSpeed(o.referenceBody, o.Radius(UT)) * o.Horizontal(UT);
            Vector3d actualVelocity = o.SwappedOrbitalVelocityAtUT(UT);
            return desiredVelocity - actualVelocity;
        }
    }
}
