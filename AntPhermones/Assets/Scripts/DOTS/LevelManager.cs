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
    public static ObstacleData GetObstacleData { get { return new ObstacleData { obstacles = main.obstaclesPacked, indexes = main.bucketIndexes, resolution = main.levelData.bucketResolution }; } }
    public static NativeArray<BucketIndex> BucketIndexes { get { return main.bucketIndexes; } }
    public static NativeArray<Obstacle> ObstaclesPacked { get { return main.obstaclesPacked; } }
    public static NativeArray<float> Pheromones { get { return main.pheromones; } }
    public static NativeArray<Color> PheromonesColor { get { return main.pheromonesColor; } }
	public Text currentAntText;
	public Text nextAntText;

	public NativeArray<Ant2> ants;
    public NativeArray<Matrix4x4> matrices;
    public NativeArray<Vector4> colors;
	public NativeArray<Matrix4x4> rotationMatrixLookup;

    [SerializeField] LevelConfigData levelData;
    [SerializeField] RenderingConfigData renderData;
    [SerializeField] AntConfigData antData;

    Material myPheromoneMaterial;
    NativeArray<Obstacle> obstacles;
    NativeArray<BucketIndex> bucketIndexes;
    NativeArray<Obstacle> obstaclesPacked;
    NativeArray<float> pheromones;
    NativeArray<Color> pheromonesColor;

    AntMovementSystem movementSystem;
    PheromoneUpdateSystem pheromoneUpdateSystem;

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
        pheromonesColor = new NativeArray<Color>(mapSize * mapSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);

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

		movementSystem = World.Active.GetOrCreateSystem<AntMovementSystem>();
        pheromoneUpdateSystem = World.Active.GetOrCreateSystem<PheromoneUpdateSystem>();
    }


    private void OnDestroy()
    {
        obstacles.Dispose();
        bucketIndexes.Dispose();
        obstaclesPacked.Dispose();
        pheromones.Dispose();
        pheromonesColor.Dispose();
		ants.Dispose();
		rotationMatrixLookup.Dispose();
	}

	private void OnDisable()
	{
		AntQuantityPersistor.Instance.antCount = antData.antCount;
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
		//movementSystem.Update();
        //pheromoneUpdateSystem.Update();
        //PheromoneUpdateSystem.decayJobHandle.Complete();
    }

	void RunJobs()
	{
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
			pheromonesColor = LevelManager.PheromonesColor,
			mapSize = LevelManager.LevelData.mapSize,
			trailDecay = LevelManager.AntData.trailDecay
		};

		matrices = new NativeArray<Matrix4x4>(ants.Length, Allocator.TempJob);
		colors = new NativeArray<Vector4>(ants.Length, Allocator.TempJob);

		RenderDataBuilderJob renderDataJob = new RenderDataBuilderJob
		{
			mapSize = levelData.mapSize,
			matrices = matrices,
			colors = colors,
			rotationResolution = levelData.rotationResolution,
			ants = ants,
			rotations = rotationMatrixLookup,
			searchColor = renderData.searchColor,
			carryColor = renderData.carryColor
		};

		JobHandle moveHandle = moveJob.Schedule(ants.Length, 64);
		JobHandle renderDataHandle = renderDataJob.Schedule(ants.Length, 64, moveHandle);
		JobHandle pheroUpdateHandle = updateJob.Schedule(moveHandle);
		JobHandle decayHandle = decayJob.Schedule(pheroUpdateHandle);

		Vector4[] colorManagedArray = null;
		Matrix4x4[] matrixManagedArray = null;
		MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
		Color[] pheromoneColorManagedArray = null;


		//render ants
		Profiler.BeginSample("RenderAtns");

		renderDataHandle.Complete();

		int batchSize = levelData.instancesPerBatch;

		Mesh mesh = renderData.antMesh;
		Material material = renderData.antMaterial;


		if (colorManagedArray == null || colorManagedArray.Length != batchSize)
			colorManagedArray = new Vector4[batchSize];

		if (matrixManagedArray == null || matrixManagedArray.Length != batchSize)
			matrixManagedArray = new Matrix4x4[batchSize];

		for (int i = 0; i < colors.Length; i += batchSize)
		{
			int actualBatchSize = Mathf.Min(batchSize, colors.Length - i);

			NativeArray<Vector4>.Copy(colors, i, colorManagedArray, 0, actualBatchSize);
			NativeArray<Matrix4x4>.Copy(matrices, i, matrixManagedArray, 0, actualBatchSize);

			materialPropertyBlock.SetVectorArray("_Color", colorManagedArray);

			Graphics.DrawMeshInstanced(mesh, 0, material, matrixManagedArray, actualBatchSize, materialPropertyBlock);
		}

		matrices.Dispose();
		colors.Dispose();

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

		int pheromoneCount = PheromonesColor.Length;
		if (pheromoneColorManagedArray == null || pheromoneColorManagedArray.Length != pheromoneCount)
			pheromoneColorManagedArray = new Color[pheromoneCount];

		PheromonesColor.CopyTo(pheromoneColorManagedArray);

		decayHandle.Complete();
		renderData.pheromoneTexture.SetPixels(pheromoneColorManagedArray);
		renderData.pheromoneTexture.Apply();

		Profiler.EndSample();
	}
}
