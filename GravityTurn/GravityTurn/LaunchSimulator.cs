using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP.IO;

namespace GravityTurn
{
    class LaunchSimulator
    {
        FuelFlowSimulation.Stats[] stats;
        int CurrentStage;
        double TimeStep = 0.2;

        public LaunchSimulator(FuelFlowSimulation.Stats[] inStats)
        {
            stats = inStats;
            CurrentStage = stats.Length;
        }

        public void Update()
        {

        }
    }
}
