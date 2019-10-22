using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class AntColorSystem : JobComponentSystem
{
    const int const_instancesPerBatch = 1023;

    [BurstCompile]
    public struct ComputeAntColorJob : IJobForEach<AntMaterial, HoldingResource>
    {
        [ReadOnly] public int instancesPerBatch;
        [ReadOnly] public Vector4 searchColor;
        [ReadOnly] public Vector4 carryColor;

        public void Execute([ReadOnly] ref AntMaterial antBrightness, ref HoldingResource antRessource)
        {
            /*
            antIndex doesn't exist, how to we pass the data to the materialblock later
            int index1 = antIndex / instancesPerBatch;
            int index2 = antIndex % instancesPerBatch;

            if (antRessource.Value == false)
            {
                antColors[index1][index2] += ((Vector4)searchColor * antBrightness.Value - antColors[index1][index2]) * .05f;
            }
            else
            {
                antColors[index1][index2] += ((Vector4)carryColor * antBrightness.Value - antColors[index1][index2]) * .05f;
            }*/
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        ComputeAntColorJob job = new ComputeAntColorJob
        {
            instancesPerBatch = const_instancesPerBatch,
            searchColor = LevelManager.SearchColor,
            carryColor = LevelManager.CarryColor
        };

        return job.Schedule(this, inputDeps);
    }
}
