﻿using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Collections;
using Unity.Entities;

public class LevelManager : MonoBehaviour
{
	public static LevelManager main;

	[SerializeField] public Mesh colonyMesh;
	[SerializeField] Mesh resourceMesh;
	[SerializeField] Material resourceMaterial;
	[SerializeField] Material colonyMaterial;

	[SerializeField] public int mapSize = 128;
	public static int MapSize { get { return main.mapSize; } }

	Vector2 resourcePosition;
	public static Vector2 ResourcePosition { get { return main.resourcePosition; } }

	Vector2 colonyPosition;
	public static Vector2 ColonyPosition { get { return main.colonyPosition; } }

	Matrix4x4 resourceMatrix;
	Matrix4x4 colonyMatrix;
    public int bucketResolution;
	const int instancesPerBatch = 1023;

	public int obstacleRingCount;
	[Range(0f,1f)]
	public float obstaclesPerRing;
	public float obstacleRadius;
	
	void GenerateObstacles() {
		List<Obstacle> output = new List<Obstacle>();
		for (int i=1;i<=obstacleRingCount;i++) {
			float ringRadius = (i / (obstacleRingCount+1f)) * (mapSize * .5f);
			float circumference = ringRadius * 2f * Mathf.PI;
			int maxCount = Mathf.CeilToInt(circumference / (2f * obstacleRadius) * 2f);
			int offset = Random.Range(0,maxCount);
			int holeCount = Random.Range(1,3);
			for (int j=0;j<maxCount;j++) {
				float t = (float)j / maxCount;
				if ((t * holeCount)%1f < obstaclesPerRing) {
					float angle = (j + offset) / (float)maxCount * (2f * Mathf.PI);
					Obstacle obstacle = new Obstacle();
					obstacle.position = new Vector2(mapSize * .5f + Mathf.Cos(angle) * ringRadius,mapSize * .5f + Mathf.Sin(angle) * ringRadius);
					obstacle.radius = obstacleRadius;
					output.Add(obstacle);
					//Debug.DrawRay(obstacle.position / mapSize,-Vector3.forward * .05f,Color.green,10000f);
				}
			}
		}

		var obstacleMatrices = new Matrix4x4[Mathf.CeilToInt((float)output.Count / instancesPerBatch)][];
		for (int i=0;i<obstacleMatrices.Length;i++) {
			obstacleMatrices[i] = new Matrix4x4[Mathf.Min(instancesPerBatch,output.Count - i * instancesPerBatch)];
			for (int j=0;j<obstacleMatrices[i].Length;j++) {
				obstacleMatrices[i][j] = Matrix4x4.TRS(output[i * instancesPerBatch + j].position / mapSize,Quaternion.identity,new Vector3(obstacleRadius*2f,obstacleRadius*2f,1f)/mapSize);
			}
		}

	    obstacles = new NativeArray<Obstacle>(output.ToArray() , Allocator.Persistent);

		bucketIndexes = new NativeArray<BucketIndex>(bucketResolution, Allocator.Persistent);

		obstaclesPacked = new NativeArray<Obstacle>(obstacles.Length, Allocator.Persistent);

		List<Obstacle>[,] tempObstacleBuckets = new List<Obstacle>[bucketResolution,bucketResolution];

		for (int x = 0; x < bucketResolution; x++) {
			for (int y = 0; y < bucketResolution; y++) {
				tempObstacleBuckets[x,y] = new List<Obstacle>();
			}
		}

		for (int i = 0; i < obstacles.Length; i++) {
			Vector2 pos = obstacles[i].position;
			float radius = obstacles[i].radius;
			for (int x = Mathf.FloorToInt((pos.x - radius)/mapSize*bucketResolution); x <= Mathf.FloorToInt((pos.x + radius)/mapSize*bucketResolution); x++) {
				if (x < 0 || x >= bucketResolution) {
					continue;
				}
				for (int y = Mathf.FloorToInt((pos.y - radius) / mapSize * bucketResolution); y <= Mathf.FloorToInt((pos.y + radius) / mapSize * bucketResolution); y++) {
					if (y<0 || y>=bucketResolution) {
						continue;
					}
					tempObstacleBuckets[x,y].Add(obstacles[i]);
				}
			}
		}

		var obstacleBuckets = new Obstacle[bucketResolution,bucketResolution][];
		for (int x = 0; x < bucketResolution; x++) {
			for (int y = 0; y < bucketResolution; y++) {
				obstacleBuckets[x,y] = tempObstacleBuckets[x,y].ToArray();
			}
		}

        int packedObstaclesIndex = 0;
		for (int x = 0; x < bucketResolution; x++) {
			for (int y = 0; y < bucketResolution; y++) {
				var bucket = obstacleBuckets[x,y];
				bucketIndexes[y * bucketResolution + x] = 
					new BucketIndex {start = packedObstaclesIndex, count = bucket.Length};
				
				foreach(var obstacle in bucket)
				{
					obstaclesPacked[packedObstaclesIndex] = obstacle;
					++packedObstaclesIndex;
				}
			}
		}		
	}

	struct BucketIndex
	{
		public int start;
		public int count;
	}

	NativeArray<Obstacle> obstacles;


	NativeArray<BucketIndex> bucketIndexes;

	NativeArray<Obstacle> obstaclesPacked;


	NativeSlice<Obstacle> GetObstacleBucket(Vector2 pos)
	{
		return GetObstacleBucket(pos.x, pos.y);
	}

	NativeSlice<Obstacle> GetObstacleBucket(float posX, float posY)
	{
		int x = (int)(posX / mapSize * bucketResolution);
		int y = (int)(posY / mapSize * bucketResolution);
		if (x<0 || y<0 || x>=bucketResolution || y>=bucketResolution) {
			return new NativeSlice<Obstacle>(obstaclesPacked, 0, 0);
		} else 
		{
			var bucketInfo = bucketIndexes[y * bucketResolution + x];
			NativeSlice<Obstacle> slice = new NativeSlice<Obstacle>(obstaclesPacked, bucketInfo.start, bucketInfo.count); 
			return slice;
		}
	}

    public NativeArray<float> pheromones;
    public float trailAddSpeed = 0.3f;
    public float trailDecay = 0.9985f;

    void Awake()
    {
		if (main != null && main != this)
		{
			Destroy(this);
			return;
		}

		main = this;

		colonyPosition = Vector2.one * mapSize * .5f;
		colonyMatrix = Matrix4x4.TRS(colonyPosition / mapSize, Quaternion.identity, new Vector3(4f, 4f, .1f) / mapSize);

		float resourceAngle = Random.value * 2f * Mathf.PI;
		resourcePosition = Vector2.one * mapSize * .5f + new Vector2(Mathf.Cos(resourceAngle) * mapSize * .475f, Mathf.Sin(resourceAngle) * mapSize * .475f);
		resourceMatrix = Matrix4x4.TRS(resourcePosition / mapSize, Quaternion.identity, new Vector3(4f, 4f, .1f) / mapSize);
	
		GenerateObstacles();

        // Pheromones
        pheromones = new NativeArray<float>(mapSize * mapSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
    }

	void Update()
	{
		Graphics.DrawMesh(colonyMesh, colonyMatrix, colonyMaterial, 0);
		Graphics.DrawMesh(resourceMesh, resourceMatrix, resourceMaterial, 0);
	}

    private void OnDestroy()
    {
		obstacles.Dispose();
		bucketIndexes.Dispose();
		obstaclesPacked.Dispose();
        pheromones.Dispose();

    }
}
