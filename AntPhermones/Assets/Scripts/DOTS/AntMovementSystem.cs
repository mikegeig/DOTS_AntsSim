using UnityEngine;
using System.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Random = Unity.Mathematics.Random;

public class AntMovementSystem : JobComponentSystem
{
    [BurstCompile]
    public struct ComputeAntJob : IJobForEachWithEntity<AntTransform, MoveSpeed, HoldingResource>
    {
        [ReadOnly] public float currentFrameCount;
        [ReadOnly] public float deltaTime;

        [ReadOnly] public float antSpeed;
        [ReadOnly] public float randomSteering;
        [ReadOnly] public float pheromoneSteerStrength;
        [ReadOnly] public float wallSteerStrength;
        [ReadOnly] public float antAccel;
        [ReadOnly] public int mapSize;

        [ReadOnly] public NativeArray<float> pheromones;

        [ReadOnly] public LevelManager.ObstacleData obstacleData;

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

            targetSpeed *= 1f - (Mathf.Abs(pheroSteering) + Mathf.Abs(wallSteering)) / 3f;

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
                float targetAngle = Mathf.Atan2(targetPos.y - ant.position.y, targetPos.x - ant.position.x);
                if (targetAngle - ant.facingAngle > Mathf.PI)
                {
                    ant.facingAngle += Mathf.PI * 2f;
                    color = Color.red;
                }
                else if (targetAngle - ant.facingAngle < -Mathf.PI)
                {
                    ant.facingAngle -= Mathf.PI * 2f;
                    color = Color.red;
                }
                else
                {
                    if (Mathf.Abs(targetAngle - ant.facingAngle) < Mathf.PI * .5f)
                        ant.facingAngle += (targetAngle - ant.facingAngle) * goalSteerStrength;
                }

                //Debug.DrawLine(ant.position/mapSize,targetPos/mapSize,color);
            }

            // Gather resource
            if ((ant.position - targetPos).sqrMagnitude < 4f * 4f)
            {
                holdingResource.Value = !holdingResource.Value;
                ant.facingAngle += Mathf.PI;
            }

            // Displacement
            float vx = Mathf.Cos(ant.facingAngle) * speed.Value;
            float vy = Mathf.Sin(ant.facingAngle) * speed.Value;
            float ovx = vx;
            float ovy = vy;

            if (ant.position.x + vx < 0f || ant.position.x + vx > mapSize)
            {
                vx = -vx;
            }
            else
            {
                ant.position.x += vx * deltaTime * 30.0f;
            }
            if (ant.position.y + vy < 0f || ant.position.y + vy > mapSize)
            {
                vy = -vy;
            }
            else
            {
                ant.position.y += vy * deltaTime * 30.0f;
            }


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
                    dist = Mathf.Sqrt(sqrDist);
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
            dist = Mathf.Sqrt(dx * dx + dy * dy);
            inwardOrOutward *= 1f - Mathf.Clamp01(dist / pushRadius);
            vx += dx / dist * inwardOrOutward;
            vy += dy / dist * inwardOrOutward;

            if (ovx != vx || ovy != vy)
            {
                ant.facingAngle = Mathf.Atan2(vy, vx);
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
                float angle = ant.facingAngle + i * Mathf.PI * .25f;
                float testX = ant.position.x + Mathf.Cos(angle) * distance;
                float testY = ant.position.y + Mathf.Sin(angle) * distance;

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
            return Mathf.Sign(output);
        }

        int WallSteering(ref AntTransform ant, float distance)
        {
            int output = 0;

            for (int i = -1; i <= 1; i += 2)
            {
                float angle = ant.facingAngle + i * Mathf.PI * .25f;
                float testX = ant.position.x + Mathf.Cos(angle) * distance;
                float testY = ant.position.y + Mathf.Sin(angle) * distance;

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
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            int stepCount = Mathf.CeilToInt(dist * .5f);
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

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Time.timeScale = 1f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Time.timeScale = 2f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            Time.timeScale = 3f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            Time.timeScale = 4f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            Time.timeScale = 5f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            Time.timeScale = 6f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            Time.timeScale = 7f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            Time.timeScale = 8f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            Time.timeScale = 9f;
        }


        ComputeAntJob job = new ComputeAntJob
        {
            currentFrameCount = Time.frameCount,
            antSpeed = LevelManager.AntSpeed,
            randomSteering = LevelManager.RandomSteering,
            pheromoneSteerStrength = LevelManager.PheromoneSteerStrength,
            wallSteerStrength = LevelManager.WallSteerStrength,
            antAccel = LevelManager.AntAccel,
            obstacleRadius = LevelManager.ObstacleRadius,
            outwardStrength = LevelManager.OutwardStrength,
            inwardStrength = LevelManager.InwardStrength,
            deltaTime = Time.deltaTime,
            pheromones = LevelManager.Pheromones,
            mapSize = LevelManager.MapSize,
            obstacleData = LevelManager.GetObstacleData,
            resourcePosition = LevelManager.ResourcePosition,
            colonyPosition = LevelManager.ColonyPosition,
            goalSteerStrength = LevelManager.GoalSteerStrength,
        };

        return job.Schedule(this, inputDeps);
    }
}
