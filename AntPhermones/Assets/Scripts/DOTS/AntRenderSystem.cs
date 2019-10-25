using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Profiling;
using Unity.Mathematics;

[UpdateAfter(typeof(AntMovementSystem))]
public class AntRenderDataBuilder : JobComponentSystem
{
    EntityQuery m_Group;
	RenderingConfigData renderData;


    public static JobHandle renderDataBuilderJobHandle;

    protected override void OnCreate()
    {
        m_Group = GetEntityQuery(
            ComponentType.ReadOnly<AntTransform>(),
            ComponentType.ReadOnly<NonUniformScale>(),
            ComponentType.ReadWrite<AntMaterial>(),
            ComponentType.ReadOnly<HoldingResource>());

    }

    [BurstCompile]
    public struct RenderDataBuilderJob : IJobForEachWithEntity<AntTransform, NonUniformScale, AntMaterial, HoldingResource>
    {
        public int mapSize;

        public NativeArray<Matrix4x4> matrices;
        public NativeArray<Vector4> colors;
        public Vector4 searchColor;
        public Vector4 carryColor;

        public void Execute(Entity entity, int index,
            //[ReadOnly] ref Translation translation, 
            //[ReadOnly] ref Rotation rotation, 
            [ReadOnly] ref AntTransform antTransform,
            [ReadOnly] ref NonUniformScale scale, 
            ref AntMaterial material, 
            [ReadOnly] ref HoldingResource holdingResouce)
        {
            float3 position = new float3(antTransform.position.x / mapSize, antTransform.position.y / mapSize, 0f);
            quaternion rot = quaternion.Euler(0f, 0f, antTransform.facingAngle);

            Matrix4x4 matrix = new Matrix4x4();
            matrix.SetTRS(position, rot, scale.Value);
            matrices[index] = matrix;


            float4 finalColor = holdingResouce.Value ? carryColor : searchColor;
            finalColor += (finalColor * material.currentColor.w - material.currentColor) * .05f;
            material.currentColor = finalColor;
            colors[index] = finalColor;
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
		renderData = LevelManager.RenderData;

        if (LevelManager.ColorCompute.Length == 0)
            return inputDeps;

        RenderDataBuilderJob job = new RenderDataBuilderJob
        {
            matrices = LevelManager.MatrixCompute,
            colors = LevelManager.ColorCompute,
            mapSize = LevelManager.LevelData.mapSize,
            searchColor = renderData.searchColor,
            carryColor = renderData.carryColor
        };

        renderDataBuilderJobHandle = job.Schedule(m_Group, inputDeps);
        return renderDataBuilderJobHandle;
    }
}

//[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[AlwaysUpdateSystem]
public class AntRenderSystem : ComponentSystem
{
    RenderingConfigData renderData;
    LevelConfigData levelData;

    Vector4[] colorManagedArray;
    Matrix4x4[] matrixManagedArray;
    MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
    Color[] pheromoneColorManagedArray;

    protected override void OnUpdate()
    {
		if (LevelManager.main == null)
			return;

        renderData = LevelManager.RenderData;
        levelData = LevelManager.LevelData;

        RenderAnts();
        RenderLevel();
        RenderObstacles();
        //RenderPheromones();
    }

    void RenderAnts()
    {
        Profiler.BeginSample("RenderAnts");

        if (LevelManager.MatrixDraw.Length == 0)
        {
            Profiler.EndSample();
            return;
        }

        int batchSize = levelData.instancesPerBatch;

        Mesh mesh = renderData.antMesh;
        Material material = renderData.antMaterial;

        //AntRenderDataBuilder.renderDataBuilderJobHandle.Complete();

        

        if (colorManagedArray == null || colorManagedArray.Length != batchSize)
            colorManagedArray = new Vector4[batchSize];

        if (matrixManagedArray == null || matrixManagedArray.Length != batchSize)
            matrixManagedArray = new Matrix4x4[batchSize];

        for (int i = 0; i < LevelManager.ColorDraw.Length; i += batchSize)
        {
            Profiler.BeginSample("RenderAnts_Batch");
            Profiler.BeginSample("RenderAnts_Batch_Copy"); 
            int actualBatchSize = Mathf.Min(batchSize, LevelManager.ColorDraw.Length - i);

            NativeArray<Vector4>.Copy(LevelManager.ColorDraw, i, colorManagedArray, 0, actualBatchSize);
            NativeArray<Matrix4x4>.Copy(LevelManager.MatrixDraw, i, matrixManagedArray, 0, actualBatchSize);

            materialPropertyBlock.SetVectorArray("_Color", colorManagedArray);
            Profiler.EndSample();
            Profiler.BeginSample("RenderAnts_Batch_Draw");
            Graphics.DrawMeshInstanced(mesh, 0, material, matrixManagedArray, actualBatchSize, materialPropertyBlock);
            Profiler.EndSample();
            Profiler.EndSample();
        }

        Profiler.EndSample();
    }

	void RenderLevel()
	{
		Graphics.DrawMesh(renderData.colonyMesh, levelData.colonyMatrix, renderData.colonyMaterial, 0);
		Graphics.DrawMesh(renderData.resourceMesh, levelData.resourceMatrix, renderData.resourceMaterial, 0);
	}

	void RenderObstacles()
	{
		for (int i = 0; i < levelData.obstacleMatrices.Length; i++)
		{
			Graphics.DrawMeshInstanced(renderData.obstacleMesh, 0, renderData.obstacleMaterial, levelData.obstacleMatrices[i]);
		}
	}

	void RenderPheromones()
	{
        Profiler.BeginSample("RenderPheromones");
        //PheromoneUpdateSystem.decayJobHandle.Complete();

        int pheromoneCount = LevelManager.PheromonesColorDraw.Length;
        if (pheromoneColorManagedArray == null || pheromoneColorManagedArray.Length != pheromoneCount)
            pheromoneColorManagedArray = new Color[pheromoneCount];

        LevelManager.PheromonesColorDraw.CopyTo(pheromoneColorManagedArray);

        Profiler.BeginSample("RenderPheromones_SetPixels");
        renderData.pheromoneTexture.SetPixels(pheromoneColorManagedArray);
		renderData.pheromoneTexture.Apply();
        Profiler.EndSample();

        Profiler.EndSample();

    }
}
