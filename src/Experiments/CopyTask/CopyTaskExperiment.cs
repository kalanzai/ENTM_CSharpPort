﻿using SharpNeat.Core;
using SharpNeat.Phenomes;

namespace ENTM.Experiments.CopyTask
{
    public class CopyTaskExperiment : NeatExperiment<CopyTaskEvaluator, CopyTaskEnvironment>
    {
      
        // The read output from the controller. +1 for bias input!
        public override int InputCount => Evaluator.EnvironmentOutputCount + Evaluator.TuringMachineOutputCount + 1;

        // The input we give the controller in each iteration
        // Will be empty when we expect the controller to read back the sequence.
        // The two extra ones are START and DELIMITER bits.
        public override int OutputCount => Evaluator.EnvironmentInputCount + Evaluator.TuringMachineInputCount;
        
    }
}