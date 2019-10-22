using UnityEngine;
using System.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Jobs;
using Unity.Burst;

public class AntMovementSystem : JobComponentSystem
{
    [BurstCompile]
    public struct ComputeAntJob : IJobForEach<AntComponent, Translation, Rotation>
    {
        public float antSpeed;
        public float randomSteering;
        public float pheromoneSteerStrength;
        public float wallSteerStrength;
        public float antAccel;

        public void Execute(ref AntComponent ant, ref Translation pos, ref Rotation rot)
        {
            float targetSpeed = antSpeed;

            ant.facingAngle += 0.12f; //Random.Range(-randomSteering, randomSteering);

            float pheroSteering = 0.1f; //PheromoneSteering(ant, 3f);
            int wallSteering = 0; // WallSteering(ant, 1.5f);
            ant.facingAngle += pheroSteering * pheromoneSteerStrength;
            ant.facingAngle += wallSteering * wallSteerStrength;

            targetSpeed *= 1f - (Mathf.Abs(pheroSteering) + Mathf.Abs(wallSteering)) / 3f;

            ant.speed += (targetSpeed - ant.speed) * antAccel;

/*
            ANT COLOR

            Vector2 targetPos;

            int index1 = i / instancesPerBatch;
            int index2 = i % instancesPerBatch;
            if (ant.holdingResource == false)
            {
                targetPos = resourcePosition;

                antColors[index1][index2] += ((Vector4)searchColor * ant.brightness - antColors[index1][index2]) * .05f;
            }
            else
            {
                targetPos = colonyPosition;
                antColors[index1][index2] += ((Vector4)carryColor * ant.brightness - antColors[index1][index2]) * .05f;
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
            float vx = Mathf.Cos(ant.facingAngle) * ant.speed;
            float vy = Mathf.Sin(ant.facingAngle) * ant.speed;
            float ovx = vx;
            float ovy = vy;

            if (ant.position.x + vx < 0f || ant.position.x + vx > 128 /*level mapSize*/)
            {
                vx = -vx;
            }
            else
            {
                ant.position.x += vx;
            }
            if (ant.position.y + vy < 0f || ant.position.y + vy > 128 /*levelmapSize*/)
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
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        ComputeAntJob job = new ComputeAntJob
        {
            antSpeed = 0.2f,
            randomSteering = 0.14f,
            pheromoneSteerStrength = 0.015f,
            wallSteerStrength = 0.12f,
            antAccel = 0.07f
        };

        return job.Schedule(this, inputDeps);
    }
}
