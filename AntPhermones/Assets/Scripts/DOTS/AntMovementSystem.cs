using UnityEngine;
using System.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Random = Unity.Mathematics.Random;
using Unity.Mathematics;

public class AntMovementSystem : JobComponentSystem
{
    [BurstCompile]
    public struct ComputeAntJob : IJobForEachWithEntity<AntTransform, MoveSpeed, HoldingResource>
    {
        [ReadOnly] public float currentFrameCount;

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


        public void Execute(Entity entity, int index, ref AntTransform ant, ref MoveSpeed speed, ref HoldingResource holdingResource)
        {
            float targetSpeed = antSpeed;

            var random = new Random((uint)(currentFrameCount * index + 1));

            ant.facingAngle += random.NextFloat(-randomSteering, randomSteering);
            
            float pheroSteering = PheromoneSteering(ref ant, 3f);
            int wallSteering = WallSteering(ref ant, 1.5f);
            ant.facingAngle += pheroSteering * pheromoneSteerStrength;
            ant.facingAngle += wallSteering * wallSteerStrength;

            targetSpeed *= 1f - (math.abs(pheroSteering) + math.abs(wallSteering)) / 3f;

            speed.Value += (targetSpeed - speed.Value) * antAccel;

            Vector2 targetPos;
            if (holdingResource.Value == false)
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
                holdingResource.Value = !holdingResource.Value;
                ant.facingAngle += math.PI;
            }

            // Displacement
            float vx = math.cos(ant.facingAngle) * speed.Value;
            float vy = math.sin(ant.facingAngle) * speed.Value;
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
            if (holdingResource.Value)
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
        }

        int PheromoneIndex(int x, int y)
        {
            return x + y * mapSize;
        }

        float PheromoneSteering(ref AntTransform ant, float distance)
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

        int WallSteering(ref AntTransform ant, float distance)
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
                    if (LevelManager.HasObstackeInBucket(ref obstacleData, mapSize, testX, testY))
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
                if (LevelManager.HasObstackeInBucket(ref obstacleData, mapSize, point1.x + dx * t, point1.y + dy * t))
                {
                    return true;
                }
            }

            return false;
        }
    }


	protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (LevelManager.main == null)
            return inputDeps;

		AntConfigData antData = LevelManager.AntData;
		LevelConfigData levelData = LevelManager.LevelData;


		ComputeAntJob job = new ComputeAntJob
        {
            currentFrameCount = Time.frameCount,
            antSpeed = antData.antSpeed,
            randomSteering = antData.randomSteering,
            pheromoneSteerStrength = antData.pheromoneSteerStrength,
            wallSteerStrength = antData.wallSteerStrength,
            antAccel = antData.antAccel,
            obstacleRadius = levelData.obstacleRadius,
            outwardStrength = antData.outwardStrength,
            inwardStrength = antData.inwardStrength,
            pheromones = LevelManager.Pheromones,
            mapSize = levelData.mapSize,
            obstacleData = LevelManager.GetObstacleData,
            resourcePosition = levelData.resourcePosition,
            colonyPosition = levelData.colonyPosition,
            goalSteerStrength = antData.goalSteerStrength,
        };

        return job.Schedule(this, inputDeps);
    }
}
