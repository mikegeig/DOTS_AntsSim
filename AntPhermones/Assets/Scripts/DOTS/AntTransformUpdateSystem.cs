using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public class AntTransformUpdateSystem : JobComponentSystem
{
	[BurstCompile]
	public struct TransformUpdateJob : IJobForEach<AntTransform, Translation, Rotation>
	{
		public int mapSize;

		public void Execute([ReadOnly] ref AntTransform ant, ref Translation pos, ref Rotation rot)
		{
			pos.Value = new float3(ant.position.x / mapSize, ant.position.y / mapSize, 0f);
			rot.Value = quaternion.Euler(0f, 0f, ant.facingAngle);
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		if (LevelManager.main == null)
			return inputDeps;

		TransformUpdateJob job = new TransformUpdateJob
		{
			mapSize = LevelManager.LevelData.mapSize
		};

		return job.Schedule(this, inputDeps);
	}
}
