using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;

public class LevelManager : MonoBehaviour
{
	public static LevelManager main;

	public Material basePheromoneMaterial;
	public Renderer pheromoneRenderer;
	public Texture2D pheromoneTexture;
	Material myPheromoneMaterial;

	public Mesh obstacleMesh;
	public Mesh colonyMesh;
	public Mesh resourceMesh;
	public Material resourceMaterial;
	public Material colonyMaterial;
	public Material obstacleMaterial;

	[SerializeField] int mapSize = 128;
	public static int MapSize { get { return main.mapSize; } }

	Vector2 resourcePosition;
	public static Vector2 ResourcePosition { get { return main.resourcePosition; } }

	Vector2 colonyPosition;
	public static Vector2 ColonyPosition { get { return main.colonyPosition; } }

	public Matrix4x4 resourceMatrix;
	public Matrix4x4 colonyMatrix;

	public Matrix4x4[][] obstacleMatrices;
    public int bucketResolution;
	const int instancesPerBatch = 1023;

    Color searchColor;
    public static Color SearchColor { get { return main.searchColor; } }

    Color carryColor;
    public static Color CarryColor { get { return main.carryColor; } }

    public int obstacleRingCount;
	[Range(0f,1f)]
	public float obstaclesPerRing;
	public float obstacleRadius;
    public static float ObstacleRadius { get { return main.obstacleRadius; } }

    [SerializeField] float antSpeed = 0.2f;
    public static float AntSpeed { get { return main.antSpeed; } }

    [SerializeField] float randomSteering = 0.14f;
    public static float RandomSteering { get { return main.randomSteering; } }
    [SerializeField] float pheromoneSteerStrength = 0.015f;
    public static float PheromoneSteerStrength { get { return main.pheromoneSteerStrength; } }
    [SerializeField] float wallSteerStrength = 0.12f;
    public static float WallSteerStrength { get { return main.wallSteerStrength; } }
    [SerializeField] float antAccel = 0.07f;
    public static float AntAccel { get { return main.antAccel; } }
    [SerializeField] float outwardStrength = 0.003f;
    public static float OutwardStrength { get { return main.outwardStrength; } }
    [SerializeField] float inwardStrength = 0.003f;
    public static float InwardStrength { get { return main.inwardStrength; } }

    public NativeArray<Matrix4x4> matrices;
    public NativeArray<Vector4> colors;

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

		obstacleMatrices = new Matrix4x4[Mathf.CeilToInt((float)output.Count / instancesPerBatch)][];
		for (int i=0;i<obstacleMatrices.Length;i++) {
			obstacleMatrices[i] = new Matrix4x4[Mathf.Min(instancesPerBatch,output.Count - i * instancesPerBatch)];
			for (int j=0;j<obstacleMatrices[i].Length;j++) {
				obstacleMatrices[i][j] = Matrix4x4.TRS(output[i * instancesPerBatch + j].position / mapSize,Quaternion.identity,new Vector3(obstacleRadius*2f,obstacleRadius*2f,1f)/mapSize);
			}
		}

	    obstacles = new NativeArray<Obstacle>(output.ToArray() , Allocator.Persistent);

		bucketIndexes = new NativeArray<BucketIndex>(bucketResolution*bucketResolution, Allocator.Persistent);

		

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

		int obstaclePackedSize = 0;
		for (int x = 0; x < bucketResolution; x++) {
			for (int y = 0; y < bucketResolution; y++) {
				obstaclePackedSize += obstacleBuckets[x,y].Length;
			}
		}

		obstaclesPacked = new NativeArray<Obstacle>(obstaclePackedSize, Allocator.Persistent);
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

	public struct BucketIndex
	{
		public int start;
		public int count;
	}

    public struct ObstacleData
    {
        public NativeArray<Obstacle> obstacles;
        public NativeArray<BucketIndex> indexes;
        public int resolution;
    }
    public static ObstacleData GetObstacleData { get { return new ObstacleData { obstacles = main.obstaclesPacked, indexes = main.bucketIndexes, resolution = main.bucketResolution}; } }

    NativeArray<Obstacle> obstacles;

    NativeArray<BucketIndex> bucketIndexes;
    public static NativeArray<BucketIndex> BucketIndexes { get { return main.bucketIndexes; } }

    NativeArray<Obstacle> obstaclesPacked;
    public static NativeArray<Obstacle> ObstaclesPacked { get { return main.obstaclesPacked; } }

	public static NativeSlice<Obstacle> GetObstacleBucket([ReadOnly] ref ObstacleData obstacleData, int mapSize, float posX, float posY)
	{
		int x = (int)(posX / mapSize * obstacleData.resolution);
		int y = (int)(posY / mapSize * obstacleData.resolution);
		if (x<0 || y<0 || x>= obstacleData.resolution || y>= obstacleData.resolution) {
			return new NativeSlice<Obstacle>(obstacleData.obstacles, 0, 0);
		} else 
		{
			var bucketInfo = obstacleData.indexes[y * obstacleData.resolution + x];
			NativeSlice<Obstacle> slice = new NativeSlice<Obstacle>(obstacleData.obstacles, bucketInfo.start, bucketInfo.count); 
			return slice;
		}
	}


	public static NativeArray<float> Pheromones { get { return main.pheromones; } }
    NativeArray<float> pheromones;

    public float trailAddSpeed = 0.3f;
    public static float TrailAddSpeed { get { return main.trailAddSpeed; } }

    public float trailDecay = 0.9985f;
    public static float TrailDecay { get { return main.trailDecay; } }

    public float goalSteerStrength = 0.4f;
    public static float GoalSteerStrength { get { return main.goalSteerStrength; } }

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

		pheromoneTexture = new Texture2D(mapSize,mapSize);
		pheromoneTexture.wrapMode = TextureWrapMode.Mirror;
		myPheromoneMaterial = new Material(basePheromoneMaterial);
		myPheromoneMaterial.mainTexture = pheromoneTexture;
		pheromoneRenderer.sharedMaterial = myPheromoneMaterial;
    }


    private void OnDestroy()
    {
		obstacles.Dispose();
		bucketIndexes.Dispose();
		obstaclesPacked.Dispose();
        pheromones.Dispose();
    }



    int PheromoneIndex(int x, int y)
    {
        return x + y * mapSize;
    }

    float PheromoneSteering(Ant ant, float distance)
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
}
