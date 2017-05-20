﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace GravityTurn
{
    //Other modules can request that the stage stats be computed by calling RequestUpdate
    //This module will then run the stage stats computation in a separate thread, update
    //the publicly available atmoStats and vacStats. Then it will disable itself unless
    //it got another RequestUpdate in the meantime.
    public class StageStats
    {
        private StageController Stage;
        public StageStats(StageController stage) : base() { Stage = stage;  }

        public static Vessel vessel { get { return FlightGlobals.ActiveVessel; } }
        public bool dVLinearThrust = true;

        public FuelFlowSimulation.Stats[] atmoStats = { };
        public FuelFlowSimulation.Stats[] vacStats = { };

        private FuelFlowSimulation[] sims = { new FuelFlowSimulation(), new FuelFlowSimulation() };

        public void RequestUpdate(object controller)
        {
            updateRequested = true;

            if (HighLogic.LoadedSceneIsEditor && editorBody != null)
            {
                TryStartSimulation();
            }
        }

        public CelestialBody editorBody;
        public bool liveSLT = true;


        protected bool updateRequested = false;
        protected bool simulationRunning = false;
        protected System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

        long millisecondsBetweenSimulations;

        public void OnModuleEnabled()
        {
            millisecondsBetweenSimulations = 0;
            stopwatch.Start();
        }

        public void OnModuleDisabled()
        {
            stopwatch.Stop();
            stopwatch.Reset();
        }

        public void OnFixedUpdate()
        {
            TryStartSimulation();
        }
        public void ForceSimunlation()
        {
            try
            {
                simulationRunning = true;
                stopwatch.Start(); //starts a timer that times how long the simulation takes

                //Create two FuelFlowSimulations, one for vacuum and one for atmosphere
                List<Part> parts = (HighLogic.LoadedSceneIsEditor ? EditorLogic.fetch.ship.parts : vessel.parts);

                sims[0].Init(parts, dVLinearThrust);
                sims[1].Init(parts, dVLinearThrust);

                RunSimulation(sims);
            }
            catch (Exception e)
            {
                GravityTurner.Log("Exception in StageStats.ForceSimunlation(): {0}", e.ToString());
                // Stop timing the simulation
                stopwatch.Stop();
                millisecondsBetweenSimulations = 500;
                stopwatch.Reset();

                // Start counting down the time to the next simulation
                stopwatch.Start();
                simulationRunning = false;
            }
        }

        public void TryStartSimulation()
        {
            if ((HighLogic.LoadedSceneIsEditor || vessel.isActiveVessel) && !simulationRunning)
            {
                //We should be running simulations periodically, but one is not running right now.
                //Check if enough time has passed since the last one to start a new one:
                if (stopwatch.ElapsedMilliseconds > millisecondsBetweenSimulations)
                {
                    if (updateRequested)
                    {
                        updateRequested = false;

                        stopwatch.Stop();
                        stopwatch.Reset();

                        StartSimulation();
                    }
                }
            }
        }

        protected void StartSimulation()
        {
            try
            {
                simulationRunning = true;
                stopwatch.Start(); //starts a timer that times how long the simulation takes

                //Create two FuelFlowSimulations, one for vacuum and one for atmosphere
                List<Part> parts = (HighLogic.LoadedSceneIsEditor ? EditorLogic.fetch.ship.parts : vessel.parts);

                sims[0].Init(parts, dVLinearThrust);
                sims[1].Init(parts, dVLinearThrust);

                //Run the simulation in a separate thread
                ThreadPool.QueueUserWorkItem(RunSimulation, sims);
                //RunSimulation(sims);
            }
            catch (Exception)
            {

                // Stop timing the simulation
                stopwatch.Stop();
                millisecondsBetweenSimulations = 500;
                stopwatch.Reset();

                // Start counting down the time to the next simulation
                stopwatch.Start();
                simulationRunning = false;
            }
        }

        protected void RunSimulation(object o)
        {
            try
            {
                CelestialBody simBody = HighLogic.LoadedSceneIsEditor ? editorBody : vessel.mainBody;
                double staticPressureKpa = (HighLogic.LoadedSceneIsEditor || !liveSLT ? (simBody.atmosphere ? simBody.GetPressure(0) : 0) : vessel.staticPressurekPa);
                double atmDensity = (HighLogic.LoadedSceneIsEditor || !liveSLT ? simBody.GetDensity(simBody.GetPressure(0), simBody.GetTemperature(0)) : vessel.atmDensity) / 1.225;
                double mach = HighLogic.LoadedSceneIsEditor ? 1 : vessel.mach;

                //Run the simulation
                FuelFlowSimulation[] sims = (FuelFlowSimulation[])o;
                FuelFlowSimulation.Stats[] newAtmoStats = sims[0].SimulateAllStages(1.0f, staticPressureKpa, atmDensity, mach);
                FuelFlowSimulation.Stats[] newVacStats = sims[1].SimulateAllStages(1.0f, 0.0, 0.0, mach);
                atmoStats = newAtmoStats;
                vacStats = newVacStats;
            }
            catch (Exception e)
            {
                GravityTurner.Log("Exception in StageStats.RunSimulation(): {0}", e.ToString());
            }

            //see how long the simulation took
            stopwatch.Stop();
            long millisecondsToCompletion = stopwatch.ElapsedMilliseconds;
            stopwatch.Reset();

            //set the delay before the next simulation
            millisecondsBetweenSimulations = 2 * millisecondsToCompletion;

            //start the stopwatch that will count off this delay
            stopwatch.Start();
            simulationRunning = false;
        }
    }
}
