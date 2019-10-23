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

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateBefore(typeof(AntRenderSystem))]
[UpdateAfter(typeof(AntTransformUpdateSystem))]
public class AntRenderDataBuilder : JobComponentSystem
{
    EntityQuery m_Group;
	RenderingConfigData renderData;


    public static JobHandle renderDataBuilderJobHandle;

    protected override void OnCreate()
    {
        m_Group = GetEntityQuery(
            ComponentType.ReadOnly<Translation>(),
            ComponentType.ReadOnly<Rotation>(),
            ComponentType.ReadOnly<NonUniformScale>(),
            ComponentType.ReadWrite<AntMaterial>(),
            ComponentType.ReadOnly<HoldingResource>());

    }

    [BurstCompile]
    public struct RenderDataBuilderJob : IJobForEachWithEntity<Translation, Rotation, NonUniformScale, AntMaterial, HoldingResource>
    {
        public int mapSize;

        public NativeArray<Matrix4x4> matrices;
        public NativeArray<Vector4> colors;
        public Vector4 searchColor;
        public Vector4 carryColor;

        public void Execute(Entity entity, int index, 
            [ReadOnly] ref Translation translation, 
            [ReadOnly] ref Rotation rotation, 
            [ReadOnly] ref NonUniformScale scale, 
            ref AntMaterial material, 
            [ReadOnly] ref HoldingResource holdingResouce)
        {
            Matrix4x4 matrix = new Matrix4x4();
            matrix.SetTRS(translation.Value, rotation.Value, scale.Value);
            matrices[index] = matrix;


            Vector4 finalColor = holdingResouce.Value ? carryColor : searchColor;
            finalColor += (finalColor * material.brightness - material.currentColor) * .05f;
            material.currentColor = finalColor;
            colors[index] = finalColor;
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
		renderData = LevelManager.RenderData;
		int entityCount = m_Group.CalculateEntityCount();

        LevelManager.main.matrices = new NativeArray<Matrix4x4>(entityCount, Allocator.TempJob);
        LevelManager.main.colors = new NativeArray<Vector4>(entityCount, Allocator.TempJob);

        RenderDataBuilderJob job = new RenderDataBuilderJob
        {
            matrices = LevelManager.main.matrices,
            colors = LevelManager.main.colors,
            searchColor = renderData.searchColor,
            carryColor = renderData.carryColor
        };

        renderDataBuilderJobHandle = job.Schedule(m_Group, inputDeps);
        return renderDataBuilderJobHandle;
    }
}

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateAfter(typeof(AntTransformUpdateSystem))]
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
        renderData = LevelManager.RenderData;
        levelData = LevelManager.LevelData;

        RenderAnts();
        RenderLevel();
        RenderObstacles();
        RenderPheromones();
    }

    void RenderAnts()
    {
        Profiler.BeginSample("RenderAtns");

        if (LevelManager.main.matrices.Length == 0)
            return;

        int batchSize = levelData.instancesPerBatch;

        Mesh mesh = renderData.antMesh;
        Material material = renderData.antMaterial;

        AntRenderDataBuilder.renderDataBuilderJobHandle.Complete();

        

        if (colorManagedArray == null || colorManagedArray.Length != batchSize)
            colorManagedArray = new Vector4[batchSize];

        if (matrixManagedArray == null || matrixManagedArray.Length != batchSize)
            matrixManagedArray = new Matrix4x4[batchSize];

        for (int i = 0; i < LevelManager.main.colors.Length; i += batchSize)
        {
            int actualBatchSize = Mathf.Min(batchSize, LevelManager.main.colors.Length - i);

            NativeSlice<Vector4> colorSlice = new NativeSlice<Vector4>(LevelManager.main.colors, i, actualBatchSize);
            NativeSlice<Matrix4x4> matrixSlice = new NativeSlice<Matrix4x4>(LevelManager.main.matrices, i, actualBatchSize);
 
            unsafe
            {
                {
                    void* colorPtr = colorSlice.GetUnsafeReadOnlyPtr();
                    void* colorManagedBuffer = UnsafeUtility.AddressOf(ref colorManagedArray[0]);
                    UnsafeUtility.MemCpy(colorManagedBuffer, colorPtr, actualBatchSize * sizeof(Vector4));
                }

                {
                    void* matrixPtr = matrixSlice.GetUnsafeReadOnlyPtr();
                    void* matrixManagedBuffer = UnsafeUtility.AddressOf(ref matrixManagedArray[0]);
                    UnsafeUtility.MemCpy(matrixManagedBuffer, matrixPtr, actualBatchSize * sizeof(Matrix4x4));
                }
            };

            materialPropertyBlock.SetVectorArray("_Color", colorManagedArray);

            Graphics.DrawMeshInstanced(mesh, 0, material, matrixManagedArray, actualBatchSize, materialPropertyBlock);
        }
        
        LevelManager.main.matrices.Dispose();
        LevelManager.main.colors.Dispose();

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

        int pheromoneCount = LevelManager.PheromonesColor.Length;
        if (pheromoneColorManagedArray == null || pheromoneColorManagedArray.Length != pheromoneCount)
            pheromoneColorManagedArray = new Color[pheromoneCount];

        unsafe
        {
            void* colorPtr = LevelManager.PheromonesColor.GetUnsafeReadOnlyPtr();
            void* colorManagedBuffer = UnsafeUtility.AddressOf(ref pheromoneColorManagedArray[0]);
            UnsafeUtility.MemCpy(colorManagedBuffer, colorPtr, pheromoneCount * sizeof(Vector4));
        }

        PheromoneUpdateSystem.decayJobHandle.Complete();
		renderData.pheromoneTexture.SetPixels(pheromoneColorManagedArray);
		renderData.pheromoneTexture.Apply();

        Profiler.EndSample();

    }
}
