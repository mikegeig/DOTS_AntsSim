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
    public struct ComputeAntJob : IJobForEach<AntTransform, MoveSpeed>
    {
        [ReadOnly] public float antSpeed;
        [ReadOnly] public float randomSteering;
        [ReadOnly] public float pheromoneSteerStrength;
        [ReadOnly] public float wallSteerStrength;
        [ReadOnly] public float antAccel;
        [ReadOnly] public int mapSize;

        [ReadOnly] public NativeArray<float> pheromones;

        [ReadOnly] public LevelManager.ObstacleData obstacleData;

        public float trailAddSpeed;


        public void Execute(ref AntTransform ant, ref MoveSpeed speed)
        {
            float targetSpeed = antSpeed;

            var random = new Random((uint)(ant.position.x * ant.position.y));

            ant.facingAngle += random.NextFloat(-randomSteering, randomSteering);

            float pheroSteering = PheromoneSteering(ref ant, 3f);
            int wallSteering = WallSteering(ref ant, 1.5f);
            ant.facingAngle += pheroSteering * pheromoneSteerStrength;
            ant.facingAngle += wallSteering * wallSteerStrength;

            targetSpeed *= 1f - (Mathf.Abs(pheroSteering) + Mathf.Abs(wallSteering)) / 3f;

            speed.Value += (targetSpeed - speed.Value) * antAccel;


            /*
            ANT COLOR moved to antcolorsystem

            TargetPos

            Vector2 targetPos;
            if (ant.holdingResource == false)
            {
                targetPos = resourcePosition;
            }
            else
            {
                targetPos = colonyPosition;
            }*/



            /*
            LINECAST 

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
            }*/

            // Gather resource
            /*if ((ant.position - targetPos).sqrMagnitude < 4f * 4f)
            {
                ant.holdingResource = !ant.holdingResource;
                ant.facingAngle += Mathf.PI;
            }*/



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
                ant.position.x += vx;
            }
            if (ant.position.y + vy < 0f || ant.position.y + vy > mapSize)
            {
                vy = -vy;
            }
            else
            {
                ant.position.y += vy;
            }


            /* OBSTACLE AVOIDANCE
             
            float dx, dy, dist;

            Obstacle[] nearbyObstacles = GetObstacleBucket(ant.position);
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
            if (ant.holdingResource)
            {
                inwardOrOutward = inwardStrength;
                pushRadius = mapSize;
            }
            */

            /* ???
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
            
             */

            /*
                         Pheromone dropping

                        //if (ant.holdingResource == false) {
                        //float excitement = 1f-Mathf.Clamp01((targetPos - ant.position).magnitude / (mapSize * 1.2f));
                        float excitement = .3f;
                        if (ant.holdingResource)
                        {
                            excitement = 1f;
                        }
                        excitement *= ant.speed / antSpeed;
                        DropPheromones(ant.position, excitement);
                        //}
            */

            /*
             * ANT UNITY MOVEMENT, ALREADY MOVED TO ANTTRANSFORMUPDATE
                        Matrix4x4 matrix = GetRotationMatrix(ant.facingAngle);
                        matrix.m03 = ant.position.x / mapSize;
                        matrix.m13 = ant.position.y / mapSize;
                        matrices[i / instancesPerBatch][i % instancesPerBatch] = matrix;
                        pos.Value = new float3(ant.position.x / mapSize, ant.position.y / mapSize, 0f);
                        rot.Value = quaternion.Euler(0f, 0f, ant.facingAngle);
            */
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
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        ComputeAntJob job = new ComputeAntJob
        {
            antSpeed = 0.2f,
            randomSteering = 0.14f,
            pheromoneSteerStrength = 0.015f,
            wallSteerStrength = 0.12f,
            antAccel = 0.07f,
            pheromones = LevelManager.Pheromones,
            trailAddSpeed = LevelManager.main.trailAddSpeed,
            mapSize = LevelManager.MapSize,
            obstacleData = LevelManager.GetObstacleData
        };

        return job.Schedule(this, inputDeps);
    }
}
