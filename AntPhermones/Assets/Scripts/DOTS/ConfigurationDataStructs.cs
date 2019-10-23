﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct LevelData
{
	public int mapSize;
	public int instancesPerBatch;
	public int bucketResolution;
	public int obstacleRingCount;
	[Range(0f, 1f)] public float obstaclesPerRing;
	public float obstacleRadius;

	[HideInInspector] public Vector2 resourcePosition;
	[HideInInspector] public Vector2 colonyPosition;
	[HideInInspector] public Matrix4x4 resourceMatrix;
	[HideInInspector] public Matrix4x4 colonyMatrix;
	[HideInInspector] public Matrix4x4[][] obstacleMatrices;
}

[System.Serializable]
public struct RenderingData
{
	public GameObject antPrefab;
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
public struct AntMovementData
{
	public int antCount;
	public float antSpeed;
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