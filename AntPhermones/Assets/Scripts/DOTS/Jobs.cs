using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using Unity.Mathematics;

[BurstCompile]
public struct RenderDataBuilderJob : IJobParallelFor
{
	public int mapSize;
	public int rotationResolution;
	public NativeArray<Matrix4x4> matrices;
	public NativeArray<Vector4> colors;
	[ReadOnly] public NativeArray<Ant2> ants;
	[ReadOnly] public NativeArray<Matrix4x4> rotations;
	public Vector4 searchColor;
	public Vector4 carryColor;
	

	public void Execute(int index)
	{
		Matrix4x4 matrix = GetRotationMatrix(ants[index].facingAngle);
		matrix.m03 = ants[index].position.x / mapSize;
		matrix.m13 = ants[index].position.y / mapSize;
		matrices[index] = matrix;

		Ant2 ant = ants[index];

		Vector4 finalColor = ant.holdingResource ? carryColor : searchColor;
		finalColor += (finalColor * ant.brightness - ant.color) * .05f;
		colors[index] = finalColor;
	}

	Matrix4x4 GetRotationMatrix(float angle)
	{
		angle /= Mathf.PI * 2f;
		angle -= Mathf.Floor(angle);
		angle *= rotationResolution;
		return rotations[((int)angle) % rotationResolution];
	}
}

[BurstCompile]
public struct MoveAntJob : IJobParallelFor
{
	[ReadOnly] public float currentFrameCount;
	public NativeArray<Ant2> ants;

	[ReadOnly] public float antSpeed;
	[ReadOnly] public float randomSteering;
	[ReadOnly] public float pheromoneSteerStrength;
	[ReadOnly] public float wallSteerStrength;
	[ReadOnly] public float antAccel;
	[ReadOnly] public int mapSize;

	[ReadOnly] public NativeArray<float> pheromones;

	[ReadOnly] public ObstacleData obstacleData;

	[ReadOnly] public Vector2 resourcePosition;
	[ReadOnly] public Vector2 colonyPosition;

	[ReadOnly] public float goalSteerStrength;

	[ReadOnly] public float obstacleRadius;
	[ReadOnly] public float outwardStrength;
	[ReadOnly] public float inwardStrength;


	public void Execute(int index)
	{
		Ant2 ant = ants[index];

		float targetSpeed = antSpeed;

		var random = new Random((uint)(currentFrameCount * index + 1));

		ant.facingAngle += random.NextFloat(-randomSteering, randomSteering);

		float pheroSteering = PheromoneSteering(ref ant, 3f);
		int wallSteering = WallSteering(ref ant, 1.5f);
		ant.facingAngle += pheroSteering * pheromoneSteerStrength;
		ant.facingAngle += wallSteering * wallSteerStrength;

		targetSpeed *= 1f - (math.abs(pheroSteering) + math.abs(wallSteering)) / 3f;

		ant.speed += (targetSpeed - ant.speed) * antAccel;

		Vector2 targetPos;
		if (ant.holdingResource == false)
		{
			targetPos = resourcePosition;
		}
		else
		{
			targetPos = colonyPosition;
		}

		if (Linecast(ant.position, targetPos) == false)
		{
			Color color = Color.green;
			float targetAngle = math.atan2(targetPos.y - ant.position.y, targetPos.x - ant.position.x);
			if (targetAngle - ant.facingAngle > math.PI)
			{
				ant.facingAngle += math.PI * 2f;
				color = Color.red;
			}
			else if (targetAngle - ant.facingAngle < -math.PI)
			{
				ant.facingAngle -= math.PI * 2f;
				color = Color.red;
			}
			else
			{
				if (math.abs(targetAngle - ant.facingAngle) < math.PI * .5f)
					ant.facingAngle += (targetAngle - ant.facingAngle) * goalSteerStrength;
			}

			//Debug.DrawLine(ant.position/mapSize,targetPos/mapSize,color);
		}

		// Gather resource
		if ((ant.position - targetPos).sqrMagnitude < 4f * 4f)
		{
			ant.holdingResource = !ant.holdingResource;
			ant.facingAngle += math.PI;
		}

		// Displacement
		float vx = math.cos(ant.facingAngle) * ant.speed;
		float vy = math.sin(ant.facingAngle) * ant.speed;
		float ovx = vx;
		float ovy = vy;

		if (ant.position.x + vx < 0f || ant.position.x + vx > mapSize)
		{
			vx = -vx;
		}

		ant.position.x += vx;

		if (ant.position.y + vy < 0f || ant.position.y + vy > mapSize)
		{
			vy = -vy;
		}

		ant.position.y += vy;



		// Obstacle pushback
		float dx, dy, dist;
		NativeSlice<Obstacle> nearbyObstacles = LevelManager.GetObstacleBucket(ref obstacleData, mapSize, ant.position.x, ant.position.y);
		for (int j = 0; j < nearbyObstacles.Length; j++)
		{
			Obstacle obstacle = nearbyObstacles[j];
			dx = ant.position.x - obstacle.position.x;
			dy = ant.position.y - obstacle.position.y;
			float sqrDist = dx * dx + dy * dy;
			if (sqrDist < obstacleRadius * obstacleRadius)
			{
				dist = math.sqrt(sqrDist);
				dx /= dist;
				dy /= dist;
				ant.position.x = obstacle.position.x + dx * obstacleRadius;
				ant.position.y = obstacle.position.y + dy * obstacleRadius;

				vx -= dx * (dx * vx + dy * vy) * 1.5f;
				vy -= dy * (dx * vx + dy * vy) * 1.5f;
			}
		}

		float inwardOrOutward = -outwardStrength;
		float pushRadius = mapSize * .4f;
		if (ant.holdingResource)
		{
			inwardOrOutward = inwardStrength;
			pushRadius = mapSize;
		}

		// ?????
		dx = colonyPosition.x - ant.position.x;
		dy = colonyPosition.y - ant.position.y;
		dist = math.sqrt(dx * dx + dy * dy);
		inwardOrOutward *= 1f - math.clamp(dist / pushRadius, 0.0f, 1.0f);
		vx += dx / dist * inwardOrOutward;
		vy += dy / dist * inwardOrOutward;

		if (ovx != vx || ovy != vy)
		{
			ant.facingAngle = math.atan2(vy, vx);
		}

		ants[index] = ant;
	}

	int PheromoneIndex(int x, int y)
	{
		return x + y * mapSize;
	}

	float PheromoneSteering(ref Ant2 ant, float distance)
	{
		float output = 0;

		for (int i = -1; i <= 1; i += 2)
		{
			float angle = ant.facingAngle + i * math.PI * .25f;
			float testX = ant.position.x + math.cos(angle) * distance;
			float testY = ant.position.y + math.sin(angle) * distance;

			if (testX < 0 || testY < 0 || testX >= mapSize || testY >= mapSize)
			{

			}
			else
			{
				int index = PheromoneIndex((int)testX, (int)testY);
				float value = pheromones[index];
				output += value * i;
			}
		}
		return math.sign(output);
	}

	int WallSteering(ref Ant2 ant, float distance)
	{
		int output = 0;

		for (int i = -1; i <= 1; i += 2)
		{
			float angle = ant.facingAngle + i * math.PI * .25f;
			float testX = ant.position.x + math.cos(angle) * distance;
			float testY = ant.position.y + math.sin(angle) * distance;

			if (testX < 0 || testY < 0 || testX >= mapSize || testY >= mapSize)
			{
			}
			else
			{
				int value = LevelManager.GetObstacleBucket(ref obstacleData, mapSize, testX, testY).Length;
				if (value > 0)
				{
					output -= i;
				}
			}
		}
		return output;
	}

	bool Linecast(Vector2 point1, Vector2 point2)
	{
		float dx = point2.x - point1.x;
		float dy = point2.y - point1.y;
		float dist = math.sqrt(dx * dx + dy * dy);

		int stepCount = (int)math.ceil(dist * .5f);
		for (int i = 0; i < stepCount; i++)
		{
			float t = (float)i / stepCount;
			if (LevelManager.GetObstacleBucket(ref obstacleData, mapSize, point1.x + dx * t, point1.y + dy * t).Length > 0)
			{
				return true;
			}
		}

		return false;
	}
}

[BurstCompile]
public struct PheromoneUpdateJob : IJob
{
	public NativeArray<float> pheromones;
	[ReadOnly] public NativeArray<Ant2> ants;
	public int mapSize;
	public float trailAddSpeed;
	public float defaultAntSpeed;
	public float deltaTime;

	public void Execute()
	{
		for (int index = 0; index < ants.Length; index++)
		{
			Ant2 ant = ants[index];
			float excitement = ant.holdingResource ? 1f : .3f;
			excitement *= ant.speed / defaultAntSpeed;
			DropPheromones(ant.position, excitement);
		}
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
	public NativeArray<Color> pheromonesColor;
	[ReadOnly] public int mapSize;
	[ReadOnly] public float trailDecay;

	public void Execute()
	{
		for (int x = 0; x < mapSize; x++)
		{
			for (int y = 0; y < mapSize; y++)
			{
				int index = x + y * mapSize;
				pheromones[index] *= trailDecay;
				pheromonesColor[index] = new Color(pheromones[index], 0.0f, 0.0f);
			}
		}
	}
}
