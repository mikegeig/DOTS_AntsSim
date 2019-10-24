using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using UnityEngine.UI;

public class LevelManager : MonoBehaviour
{
    public static LevelManager main;

    public static LevelConfigData LevelData { get { return main.levelData; } }
    public static RenderingConfigData RenderData { get { return main.renderData; } }
    public static AntConfigData AntData { get { return main.antData; } }
    public static ObstacleData GetObstacleData { 
        get 
        { 
            return new ObstacleData 
            {                                   
                obstacles = main.obstaclesPacked, 
                indexes = main.bucketIndexes, 
                resolution = main.levelData.bucketResolution, 
                obstacleBitGrid = main.obstacleBitGrid                                 
            }; 
        } 
    }

    public static NativeArray<BucketIndex> BucketIndexes { get { return main.bucketIndexes; } }
    public static NativeArray<Obstacle> ObstaclesPacked { get { return main.obstaclesPacked; } }
    public static NativeArray<float> Pheromones { get { return main.pheromones; } }
    public static NativeArray<Color> PheromonesColorDraw { get { return main.swapBuffer ? main.pheromonesColorA : main.pheromonesColorB; } }
    public static NativeArray<Color> PheromonesColorCompute { get { return main.swapBuffer ? main.pheromonesColorB : main.pheromonesColorA; } }
    public Text currentAntText;
	public Text nextAntText;

    public static NativeArray<Matrix4x4> MatrixDraw { get { return main.swapBuffer ? main.matricesA : main.matricesB; } }
    public static NativeArray<Matrix4x4> MatrixCompute { get { return main.swapBuffer ? main.matricesB : main.matricesA; } }
    public static NativeArray<Vector4> ColorDraw { get { return main.swapBuffer ? main.colorsA : main.colorsB; } }
    public static NativeArray<Vector4> ColorCompute { get { return main.swapBuffer ? main.colorsB : main.colorsA; } }

    NativeArray<Matrix4x4> matricesA;
    NativeArray<Vector4> colorsA;
    NativeArray<Matrix4x4> matricesB;
    NativeArray<Vector4> colorsB;

    [SerializeField] LevelConfigData levelData;
    [SerializeField] RenderingConfigData renderData;
    [SerializeField] AntConfigData antData;

    Material myPheromoneMaterial;
    NativeArray<Obstacle> obstacles;
    NativeArray<BucketIndex> bucketIndexes;
    NativeArray<Obstacle> obstaclesPacked;
    NativeArray<System.UInt64> obstacleBitGrid;
    NativeArray<float> pheromones;
    NativeArray<Color> pheromonesColorA;
    NativeArray<Color> pheromonesColorB;

    AntMovementSystem movementSystem;
    PheromoneUpdateSystem pheromoneUpdateSystem;
    AntRenderSystem antRenderSystem;

    bool swapBuffer = false;

    void Awake()
    {
        if (main != null && main != this)
        {
            Destroy(this);
            return;
        }

        main = this;

		if (AntQuantityPersistor.Instance.antCount == 0)
			AntQuantityPersistor.Instance.antCount = antData.antCount;
		else
			antData.antCount = AntQuantityPersistor.Instance.antCount;

		currentAntText.text = "Current ant count: " + antData.antCount;
		nextAntText.text = "Next ant count: " + antData.antCount;


		int mapSize = levelData.mapSize;

        levelData.colonyPosition = Vector2.one * mapSize * .5f;
        levelData.colonyMatrix = Matrix4x4.TRS(levelData.colonyPosition / mapSize, Quaternion.identity, new Vector3(4f, 4f, .1f) / mapSize);

        float resourceAngle = Random.value * 2f * Mathf.PI;
        levelData.resourcePosition = Vector2.one * mapSize * .5f + new Vector2(Mathf.Cos(resourceAngle) * mapSize * .475f, Mathf.Sin(resourceAngle) * mapSize * .475f);
        levelData.resourceMatrix = Matrix4x4.TRS(levelData.resourcePosition / mapSize, Quaternion.identity, new Vector3(4f, 4f, .1f) / mapSize);

        GenerateObstacles();

        LevelManager.main.matricesA = new NativeArray<Matrix4x4>(antData.antCount, Allocator.Persistent);
        LevelManager.main.matricesB = new NativeArray<Matrix4x4>(antData.antCount, Allocator.Persistent);
        LevelManager.main.colorsA = new NativeArray<Vector4>(antData.antCount, Allocator.Persistent);
        LevelManager.main.colorsB = new NativeArray<Vector4>(antData.antCount, Allocator.Persistent);

        // Pheromones
        pheromones = new NativeArray<float>(mapSize * mapSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        pheromonesColorA = new NativeArray<Color>(mapSize * mapSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        pheromonesColorB = new NativeArray<Color>(mapSize * mapSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        renderData.pheromoneTexture = new Texture2D(mapSize, mapSize);
        renderData.pheromoneTexture.wrapMode = TextureWrapMode.Mirror;
        myPheromoneMaterial = new Material(renderData.basePheromoneMaterial);
        myPheromoneMaterial.mainTexture = renderData.pheromoneTexture;
        renderData.pheromoneRenderer.sharedMaterial = myPheromoneMaterial;

/*
        movementSystem = World.Active.GetOrCreateSystem<AntMovementSystem>();
        pheromoneUpdateSystem = World.Active.GetOrCreateSystem<PheromoneUpdateSystem>();
        antTransformUpdateSystem = World.Active.GetOrCreateSystem<AntTransformUpdateSystem>();
        antRenderSystem = World.Active.GetOrCreateSystem<AntRenderSystem>();*/
    }


    private void OnDestroy()
    {

	}

	private void OnDisable()
	{
		AntQuantityPersistor.Instance.antCount = antData.antCount;

        AntRenderDataBuilder.renderDataBuilderJobHandle.Complete();
        PheromoneUpdateSystem.decayJobHandle.Complete();
        obstacles.Dispose();
        bucketIndexes.Dispose();
        obstaclesPacked.Dispose();
        obstacleBitGrid.Dispose();
        pheromones.Dispose();
        pheromonesColorA.Dispose();
        pheromonesColorB.Dispose();
        matricesA.Dispose();
        colorsA.Dispose();
        matricesB.Dispose();
        colorsB.Dispose();
    }

	void GenerateObstacles()
    {
        int mapSize = levelData.mapSize;

        List<Obstacle> output = new List<Obstacle>();
        for (int i = 1; i <= levelData.obstacleRingCount; i++)
        {
            float ringRadius = (i / (levelData.obstacleRingCount + 1f)) * (mapSize * .5f);
            float circumference = ringRadius * 2f * Mathf.PI;
            int maxCount = Mathf.CeilToInt(circumference / (2f * levelData.obstacleRadius) * 2f);
            int offset = Random.Range(0, maxCount);
            int holeCount = Random.Range(1, 3);
            for (int j = 0; j < maxCount; j++)
            {
                float t = (float)j / maxCount;
                if ((t * holeCount) % 1f < levelData.obstaclesPerRing)
                {
                    float angle = (j + offset) / (float)maxCount * (2f * Mathf.PI);
                    Obstacle obstacle = new Obstacle();
                    obstacle.position = new Vector2(mapSize * .5f + Mathf.Cos(angle) * ringRadius, mapSize * .5f + Mathf.Sin(angle) * ringRadius);
                    obstacle.radius = levelData.obstacleRadius;
                    output.Add(obstacle);
                    //Debug.DrawRay(obstacle.position / mapSize,-Vector3.forward * .05f,Color.green,10000f);
                }
            }
        }

        int instancesPerBatch = levelData.instancesPerBatch;

        Matrix4x4[][] tempMatrix = new Matrix4x4[Mathf.CeilToInt((float)output.Count / instancesPerBatch)][];
        for (int i = 0; i < tempMatrix.Length; i++)
        {
            tempMatrix[i] = new Matrix4x4[Mathf.Min(instancesPerBatch, output.Count - i * instancesPerBatch)];
            for (int j = 0; j < tempMatrix[i].Length; j++)
            {
                tempMatrix[i][j] = Matrix4x4.TRS(output[i * instancesPerBatch + j].position / mapSize, Quaternion.identity, new Vector3(levelData.obstacleRadius * 2f, levelData.obstacleRadius * 2f, 1f) / mapSize);
            }
        }

        levelData.obstacleMatrices = tempMatrix;

        obstacles = new NativeArray<Obstacle>(output.ToArray(), Allocator.Persistent);

        int bucketResolution = levelData.bucketResolution;

        bucketIndexes = new NativeArray<BucketIndex>(bucketResolution * bucketResolution, Allocator.Persistent);



        List<Obstacle>[,] tempObstacleBuckets = new List<Obstacle>[bucketResolution, bucketResolution];

        for (int x = 0; x < bucketResolution; x++)
        {
            for (int y = 0; y < bucketResolution; y++)
            {
                tempObstacleBuckets[x, y] = new List<Obstacle>();
            }
        }

        for (int i = 0; i < obstacles.Length; i++)
        {
            Vector2 pos = obstacles[i].position;
            float radius = obstacles[i].radius;
            for (int x = Mathf.FloorToInt((pos.x - radius) / mapSize * bucketResolution); x <= Mathf.FloorToInt((pos.x + radius) / mapSize * bucketResolution); x++)
            {
                if (x < 0 || x >= bucketResolution)
                {
                    continue;
                }
                for (int y = Mathf.FloorToInt((pos.y - radius) / mapSize * bucketResolution); y <= Mathf.FloorToInt((pos.y + radius) / mapSize * bucketResolution); y++)
                {
                    if (y < 0 || y >= bucketResolution)
                    {
                        continue;
                    }
                    tempObstacleBuckets[x, y].Add(obstacles[i]);
                }
            }
        }

        var obstacleBuckets = new Obstacle[bucketResolution, bucketResolution][];
        for (int x = 0; x < bucketResolution; x++)
        {
            for (int y = 0; y < bucketResolution; y++)
            {
                obstacleBuckets[x, y] = tempObstacleBuckets[x, y].ToArray();
            }
        }

        int obstaclePackedSize = 0;
        for (int x = 0; x < bucketResolution; x++)
        {
            for (int y = 0; y < bucketResolution; y++)
            {
                obstaclePackedSize += obstacleBuckets[x, y].Length;
            }
        }


        obstacleBitGrid = new NativeArray<System.UInt64>((bucketResolution * bucketResolution + 63)/ 64, Allocator.Persistent);

        obstaclesPacked = new NativeArray<Obstacle>(obstaclePackedSize, Allocator.Persistent);
        int packedObstaclesIndex = 0;
        for (int x = 0; x < bucketResolution; x++)
        {
            for (int y = 0; y < bucketResolution; y++)
            {
                var bucket = obstacleBuckets[x, y];
                bucketIndexes[y * bucketResolution + x] =
                    new BucketIndex { start = packedObstaclesIndex, count = bucket.Length };

                foreach (var obstacle in bucket)
                {
                    obstaclesPacked[packedObstaclesIndex] = obstacle;
                    ++packedObstaclesIndex;
                }

                // Build a packed 1 bit per cell array for line casting
                {
                    bool hasObstacle = bucket.Length > 0;
                    int bitIndex = y * bucketResolution + x;
                    int elementIndex = bitIndex / 64;
                    int bitOffset = bitIndex & 63;
                    obstacleBitGrid[elementIndex] |= hasObstacle ? (1UL << bitOffset) : 0;
                }
            }
        }
        
    }

    public static NativeSlice<Obstacle> GetObstacleBucket([ReadOnly] ref ObstacleData obstacleData, int mapSize, float posX, float posY)
    {
        int x = (int)(posX / mapSize * obstacleData.resolution);
        int y = (int)(posY / mapSize * obstacleData.resolution);
        if (x < 0 || y < 0 || x >= obstacleData.resolution || y >= obstacleData.resolution)
        {
            return new NativeSlice<Obstacle>(obstacleData.obstacles, 0, 0);
        }
        else
        {
            var bucketInfo = obstacleData.indexes[y * obstacleData.resolution + x];
            NativeSlice<Obstacle> slice = new NativeSlice<Obstacle>(obstacleData.obstacles, bucketInfo.start, bucketInfo.count);
            return slice;
        }
    }
    public static bool HasObstackeInBucket([ReadOnly] ref ObstacleData obstacleData, int mapSize, float posX, float posY)
    {
        int x = (int)(posX / mapSize * obstacleData.resolution);
        int y = (int)(posY / mapSize * obstacleData.resolution);

        if (x < 0 || y < 0 || x >= obstacleData.resolution || y >= obstacleData.resolution)
        {
            return false;
        }

        int bitIndex = y * obstacleData.resolution + x;
        int elementIndex = bitIndex / 64;
        int bitOffset = bitIndex & 63;
        System.UInt64 bitArray = obstacleData.obstacleBitGrid[elementIndex];
        bool hasObstacle = (bitArray & (1UL << bitOffset)) != 0;

        return hasObstacle;
    }
    int PheromoneIndex(int x, int y)
    {
        return x + y * levelData.mapSize;
    }

    float PheromoneSteering(Ant ant, float distance)
    {
        float output = 0;

        for (int i = -1; i <= 1; i += 2)
        {
            float angle = ant.facingAngle + i * Mathf.PI * .25f;
            float testX = ant.position.x + Mathf.Cos(angle) * distance;
            float testY = ant.position.y + Mathf.Sin(angle) * distance;

            if (testX < 0 || testY < 0 || testX >= levelData.mapSize || testY >= levelData.mapSize)
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

    void Update()
    {
		if (Input.GetKeyDown(KeyCode.M))
		{
			antData.antCount += antData.antIncreaseAmount;
			nextAntText.text = "Next ant count: " + antData.antCount;
		}
		else if (Input.GetKeyDown(KeyCode.N))
		{
			antData.antCount -= antData.antIncreaseAmount;
			if (antData.antCount < 0)
				antData.antCount = 0;

			nextAntText.text = "Next ant count: " + antData.antCount;
		}

        AntRenderDataBuilder.renderDataBuilderJobHandle.Complete();
        PheromoneUpdateSystem.decayJobHandle.Complete();
        swapBuffer = !swapBuffer;
		/*movementSystem.Update();
        pheromoneUpdateSystem.Update();
        antTransformUpdateSystem.Update();
        antRenderSystem.Update();
        PheromoneUpdateSystem.decayJobHandle.Complete();*/
    }
}
