using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

[UpdateAfter(typeof(AntMovementSystem))]
public class PheromoneUpdateSystem : JobComponentSystem
{
	[BurstCompile]
	public struct PheromoneUpdateJob : IJobForEach<AntTransform, MoveSpeed, HoldingResource>
	{
		public NativeArray<float> pheromones;
		public int mapSize;
		public float trailAddSpeed;
		public float defaultAntSpeed;
		public float deltaTime;

		public void Execute([ReadOnly] ref AntTransform transform, [ReadOnly] ref MoveSpeed speed, [ReadOnly] ref HoldingResource holding)
		{
			float excitement = holding.Value ? 1f : .3f;
			excitement *= speed.Value / defaultAntSpeed;

			DropPheromones(transform.position, excitement);
		}

		void DropPheromones(Vector2 position, float strength)
		{
			int x = Mathf.FloorToInt(position.x);
			int y = Mathf.FloorToInt(position.y);
			if (x < 0 || y < 0 || x >= mapSize || y >= mapSize)
			{
				return;
			}

			int index = x + y * mapSize;
			pheromones[index] += (trailAddSpeed * strength * deltaTime) * (1f - pheromones[index]);
			if (pheromones[index] > 1f)
			{
				pheromones[index] = 1f;
			}
		}
	}

	[BurstCompile]
	public struct DecayJob : IJob
	{
		public NativeArray<float> pheromones;
		public int mapSize;
		public float trailDecay;

		public void Execute()
		{
			for (int x = 0; x < mapSize; x++)
			{
				for (int y = 0; y < mapSize; y++)
				{
					int index = x + y * mapSize;
					pheromones[index] *= trailDecay;
				}
			}
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        PheromoneUpdateJob updateJob = new PheromoneUpdateJob
        {
            pheromones = LevelManager.Pheromones,
            mapSize = LevelManager.MapSize,
            //Hack for now, need values
            trailAddSpeed = LevelManager.TrailAddSpeed,
            defaultAntSpeed = .2f,
			deltaTime = Time.deltaTime
		};

        DecayJob decayJob = new DecayJob
        {
            pheromones = LevelManager.Pheromones,
            mapSize = LevelManager.MapSize,
            trailDecay = LevelManager.TrailDecay
        };

		JobHandle updateHandle = updateJob.ScheduleSingle(this, inputDeps);
		return decayJob.Schedule(updateHandle);
	}
}
