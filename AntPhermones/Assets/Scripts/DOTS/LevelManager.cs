using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using UnityEngine.UI;
using Unity.Jobs;
using UnityEngine.Profiling;

public class LevelManager : MonoBehaviour
{
    public static LevelManager main;

    public static LevelConfigData LevelData { get { return main.levelData; } }
    public static RenderingConfigData RenderData { get { return main.renderData; } }
    public static AntConfigData AntData { get { return main.antData; } }
	public static ObstacleData GetObstacleData
	{
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
	public Text currentAntText;
	public Text nextAntText;

	public NativeArray<Ant2> ants;
    public NativeArray<Matrix4x4> matricesB1;
	public NativeArray<Matrix4x4> matricesB2;
	public NativeArray<Vector4> colorsB1;
	public NativeArray<Vector4> colorsB2;
	NativeArray<System.UInt64> obstacleBitGrid;
	public NativeArray<Matrix4x4> rotationMatrixLookup;

    [SerializeField] LevelConfigData levelData;
    [SerializeField] RenderingConfigData renderData;
    [SerializeField] AntConfigData antData;

    Material myPheromoneMaterial;
    NativeArray<Obstacle> obstacles;
    NativeArray<BucketIndex> bucketIndexes;
    NativeArray<Obstacle> obstaclesPacked;
    NativeArray<float> pheromones;
    NativeArray<Color> pheromonesColorB1;
	NativeArray<Color> pheromonesColorB2;

	JobHandle moveHandle;
	JobHandle renderDataHandle;
	JobHandle pheroUpdateHandle;
	JobHandle decayHandle;

	bool buffer0 = true;
	bool frame1 = true;

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

        // Pheromones
        pheromones = new NativeArray<float>(mapSize * mapSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
		pheromonesColorB1 = new NativeArray<Color>(mapSize * mapSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
		pheromonesColorB2 = new NativeArray<Color>(mapSize * mapSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);

		renderData.pheromoneTexture = new Texture2D(mapSize, mapSize);
        renderData.pheromoneTexture.wrapMode = TextureWrapMode.Mirror;
        myPheromoneMaterial = new Material(renderData.basePheromoneMaterial);
        myPheromoneMaterial.mainTexture = renderData.pheromoneTexture;
        renderData.pheromoneRenderer.sharedMaterial = myPheromoneMaterial;

		rotationMatrixLookup = new NativeArray<Matrix4x4>(levelData.rotationResolution, Allocator.Persistent);
		for (int i = 0; i < levelData.rotationResolution; i++)
		{
			float angle = (float)i / levelData.rotationResolution;
			angle *= 360f;
			rotationMatrixLookup[i] = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, angle), antData.antSize);
		}

		SpawnAnts();
		matricesB1 = new NativeArray<Matrix4x4>(ants.Length, Allocator.Persistent);
		colorsB1 = new NativeArray<Vector4>(ants.Length, Allocator.Persistent);
		matricesB2 = new NativeArray<Matrix4x4>(ants.Length, Allocator.Persistent);
		colorsB2 = new NativeArray<Vector4>(ants.Length, Allocator.Persistent);
	}


    private void OnDisable()
    {
		AntQuantityPersistor.Instance.antCount = antData.antCount;

		decayHandle.Complete();
		renderDataHandle.Complete();

		obstacles.Dispose();
        bucketIndexes.Dispose();
        obstaclesPacked.Dispose();
        pheromones.Dispose();
        pheromonesColorB1.Dispose();
		pheromonesColorB2.Dispose();
		ants.Dispose();
		rotationMatrixLookup.Dispose();
		matricesB1.Dispose();
		colorsB1.Dispose();
		matricesB2.Dispose();
		colorsB2.Dispose();
		obstacleBitGrid.Dispose();
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

		obstacleBitGrid = new NativeArray<System.UInt64>((bucketResolution * bucketResolution + 63) / 64, Allocator.Persistent);

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

		RunJobs();
    }

	void RunJobs()
	{
		renderDataHandle.Complete();
		decayHandle.Complete();

		MoveAntJob moveJob = new MoveAntJob
		{
			currentFrameCount = Time.frameCount,
			ants = ants,
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

		PheromoneUpdateJob updateJob = new PheromoneUpdateJob
		{
			pheromones = LevelManager.Pheromones,
			ants = ants,
			mapSize = LevelManager.LevelData.mapSize,
			trailAddSpeed = LevelManager.AntData.trailAddSpeed,
			defaultAntSpeed = .2f,
			deltaTime = Time.deltaTime
		};

		DecayJob decayJob = new DecayJob
		{
			pheromones = LevelManager.Pheromones,
			pheromonesColor = buffer0 ? pheromonesColorB1 : pheromonesColorB2,
			mapSize = LevelManager.LevelData.mapSize,
			trailDecay = LevelManager.AntData.trailDecay
		};

		RenderDataBuilderJob renderDataJob = new RenderDataBuilderJob
		{
			mapSize = levelData.mapSize,
			matrices = buffer0 ? matricesB1 : matricesB2,
			colors = buffer0 ? colorsB1 : colorsB2,
			rotationResolution = levelData.rotationResolution,
			ants = ants,
			rotations = rotationMatrixLookup,
			searchColor = renderData.searchColor,
			carryColor = renderData.carryColor
		};

		moveHandle = moveJob.Schedule(ants.Length, 64);
		renderDataHandle = renderDataJob.Schedule(ants.Length, 64, moveHandle);
		pheroUpdateHandle = updateJob.Schedule(moveHandle);
		decayHandle = decayJob.Schedule(pheroUpdateHandle);
		JobHandle.ScheduleBatchedJobs();
		
		Vector4[] colorManagedArray = null;
		Matrix4x4[] matrixManagedArray = null;
		MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
		Color[] pheromoneColorManagedArray = null;

		//Duplicate both buffers on first frame
		if (frame1)
		{
			frame1 = false;
			renderDataHandle.Complete();
			decayHandle.Complete();
			matricesB1.CopyTo(matricesB2);
			colorsB1.CopyTo(colorsB2);
			pheromonesColorB1.CopyTo(pheromonesColorB2);
		}


		//render ants
		Profiler.BeginSample("RenderAtns");

		int batchSize = levelData.instancesPerBatch;

		Mesh mesh = renderData.antMesh;
		Material material = renderData.antMaterial;


		if (colorManagedArray == null || colorManagedArray.Length != batchSize)
			colorManagedArray = new Vector4[batchSize];

		if (matrixManagedArray == null || matrixManagedArray.Length != batchSize)
			matrixManagedArray = new Matrix4x4[batchSize];

		for (int i = 0; i < colorsB1.Length; i += batchSize)
		{
			int actualBatchSize = Mathf.Min(batchSize, colorsB1.Length - i);

			NativeArray<Vector4>.Copy(buffer0 ? colorsB2 : colorsB1, i, colorManagedArray, 0, actualBatchSize);
			NativeArray<Matrix4x4>.Copy(buffer0 ? matricesB2 : matricesB1, i, matrixManagedArray, 0, actualBatchSize);

			materialPropertyBlock.SetVectorArray("_Color", colorManagedArray);

			Graphics.DrawMeshInstanced(mesh, 0, material, matrixManagedArray, actualBatchSize, materialPropertyBlock);
		}

		Profiler.EndSample();
	
		//Render level
		Graphics.DrawMesh(renderData.colonyMesh, levelData.colonyMatrix, renderData.colonyMaterial, 0);
		Graphics.DrawMesh(renderData.resourceMesh, levelData.resourceMatrix, renderData.resourceMaterial, 0);

		//Render Obstacles
		for (int i = 0; i < levelData.obstacleMatrices.Length; i++)
		{
			Graphics.DrawMeshInstanced(renderData.obstacleMesh, 0, renderData.obstacleMaterial, levelData.obstacleMatrices[i]);
		}
	
		//Render pheromones
		Profiler.BeginSample("RenderPheromones");

		int pheromoneCount = pheromonesColorB1.Length;
		if (pheromoneColorManagedArray == null || pheromoneColorManagedArray.Length != pheromoneCount)
			pheromoneColorManagedArray = new Color[pheromoneCount];

		if(buffer0)
			pheromonesColorB2.CopyTo(pheromoneColorManagedArray);
		else
			pheromonesColorB1.CopyTo(pheromoneColorManagedArray);

		renderData.pheromoneTexture.SetPixels(pheromoneColorManagedArray);
		renderData.pheromoneTexture.Apply();

		Profiler.EndSample();

		buffer0 = !buffer0;
	}

	void SpawnAnts()
	{
		Color antColor = renderData.searchColor;

		int mapSize = levelData.mapSize;

		ants = new NativeArray<Ant2>(antData.antCount, Allocator.Persistent);

		for (int i = 0; i < antData.antCount; i++)
		{
			ants[i] = new Ant2
			{
				position = new Vector2(Random.Range(-5f, 5f) + mapSize * .5f, Random.Range(-5f, 5f) + mapSize * .5f),
				facingAngle = Random.value * Mathf.PI * 2f,
				speed = 0f,
				holdingResource = false,
				brightness = Random.Range(.75f, 1.25f)
			};
		}
	}
}
