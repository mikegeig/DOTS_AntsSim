using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

[System.Serializable]
public struct LevelConfigData
{
	public int mapSize;
	public int instancesPerBatch;
	public int bucketResolution;
	public int obstacleRingCount;
	[Range(0f, 1f)] public float obstaclesPerRing;
	public float obstacleRadius;
	public int rotationResolution;

	[HideInInspector] public Vector2 resourcePosition;
	[HideInInspector] public Vector2 colonyPosition;
	[HideInInspector] public Matrix4x4 resourceMatrix;
	[HideInInspector] public Matrix4x4 colonyMatrix;
	[HideInInspector] public Matrix4x4[][] obstacleMatrices;
}

[System.Serializable]
public struct RenderingConfigData
{
	public Mesh antMesh;
	public Mesh obstacleMesh;
	public Mesh colonyMesh;
	public Mesh resourceMesh;
	public Material antMaterial;
	public Material basePheromoneMaterial;
	public Material resourceMaterial;
	public Material colonyMaterial;
	public Material obstacleMaterial;
	public Color searchColor;
	public Color carryColor;
	public Renderer pheromoneRenderer;
	[HideInInspector] public Texture2D pheromoneTexture;
}

[System.Serializable]
public struct AntConfigData
{
	public GameObject antPrefab;
	public int antCount;
	public int antIncreaseAmount;
	public float antSpeed;
	public Vector3 antSize;
	public float randomSteering;
	public float pheromoneSteerStrength;
	public float wallSteerStrength;
	public float antAccel;
	public float outwardStrength;
	public float inwardStrength;
	public float trailAddSpeed;
	public float trailDecay;
	public float goalSteerStrength;
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

public struct Obstacle
{
	public Vector2 position;
	public float radius;
}

public struct Ant2
{
	public Vector2 position;
	public float facingAngle;
	public float speed;
	public bool holdingResource;
	public Vector4 color;
	public float brightness;
}