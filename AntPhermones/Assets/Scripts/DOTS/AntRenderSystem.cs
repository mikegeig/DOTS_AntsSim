using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateBefore(typeof(AntRenderSystem))]
[UpdateAfter(typeof(AntTransformUpdateSystem))]
public class AntRenderDataBuilder : JobComponentSystem
{
    EntityQuery m_Group;
    AntSpawner spawner;

    public static JobHandle renderDataBuilderJobHandle;

    protected override void OnCreate()
    {
        m_Group = GetEntityQuery(
            ComponentType.ReadOnly<Translation>(),
            ComponentType.ReadOnly<Rotation>(),
            ComponentType.ReadOnly<NonUniformScale>(),
            ComponentType.ReadWrite<AntMaterial>(),
            ComponentType.ReadOnly<HoldingResource>());

        spawner = GameObject.FindObjectOfType<AntSpawner>();
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
        int entityCount = m_Group.CalculateEntityCount();

        LevelManager.main.matrices = new NativeArray<Matrix4x4>(entityCount, Allocator.TempJob);
        LevelManager.main.colors = new NativeArray<Vector4>(entityCount, Allocator.TempJob);

        RenderDataBuilderJob job = new RenderDataBuilderJob
        {
            matrices = LevelManager.main.matrices,
            colors = LevelManager.main.colors,
            searchColor = spawner.searchColor,
            carryColor = spawner.carryColor
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
	AntSpawner spawner;
	const int batchSize = 1023;

	protected override void OnCreate()
	{
		spawner = GameObject.FindObjectOfType<AntSpawner>();
	}

	protected override void OnUpdate()
	{
		RenderAnts();
		RenderLevel();
		RenderObstacles();
		RenderPheromones();
	}

	void RenderAnts()
	{
		Mesh mesh = spawner.antMesh;
		Material material = spawner.antMaterial;

        AntRenderDataBuilder.renderDataBuilderJobHandle.Complete();


        for (int i = 0; i < LevelManager.main.colors.Length; i += batchSize)
        {
            int actualBatchSize = Mathf.Min(batchSize, LevelManager.main.colors.Length - i);

            NativeSlice<Vector4> colorSlice = new NativeSlice<Vector4>(LevelManager.main.colors, i, actualBatchSize);
            NativeSlice<Matrix4x4> matrixSlice = new NativeSlice<Matrix4x4>(LevelManager.main.matrices, i, actualBatchSize);

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetVectorArray("_Color", colorSlice.ToArray());

            Graphics.DrawMeshInstanced(mesh, 0, material, matrixSlice.ToArray(), matrixSlice.Length, block);
        }
        
        LevelManager.main.matrices.Dispose();
        LevelManager.main.colors.Dispose();
    }

	void RenderLevel()
	{
		Graphics.DrawMesh(LevelManager.main.colonyMesh, LevelManager.main.colonyMatrix, LevelManager.main.colonyMaterial, 0);
		Graphics.DrawMesh(LevelManager.main.resourceMesh, LevelManager.main.resourceMatrix, LevelManager.main.resourceMaterial, 0);
	}

	void RenderObstacles()
	{
		for (int i = 0; i < LevelManager.main.obstacleMatrices.Length; i++)
		{
			Graphics.DrawMeshInstanced(LevelManager.main.obstacleMesh, 0, LevelManager.main.obstacleMaterial, LevelManager.main.obstacleMatrices[i]);
		}
	}

	void RenderPheromones()
	{
        PheromoneUpdateSystem.decayJobHandle.Complete();

        Color[] pheromonesColors = new Color[LevelManager.Pheromones.Length];
		for (int i = 0; i < LevelManager.Pheromones.Length; ++i)
		{
			pheromonesColors[i].r = LevelManager.Pheromones[i];
		}
		LevelManager.main.pheromoneTexture.SetPixels(pheromonesColors);
		LevelManager.main.pheromoneTexture.Apply();
	}
}
