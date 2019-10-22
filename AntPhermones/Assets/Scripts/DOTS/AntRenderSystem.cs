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
public class AntRenderDataBuilder : JobComponentSystem
{
    EntityQuery m_Group;
    AntSpawner spawner;

    protected override void OnCreate()
    {
        //
        //ranslation tran, ref Rotation rot, ref NonUniformScale scale, ref AntMaterial mat, ref HoldingResource

        // Cached access to a set of ComponentData based on a specific query
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
            matrices[index].SetTRS(translation.Value, rotation.Value, scale.Value);


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

        return job.Schedule(m_Group, inputDeps);
    }
}

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateAfter(typeof(AntTransformUpdateSystem))]
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
		Vector4 searchColor = spawner.searchColor;
		Vector4 carryColor = spawner.carryColor;
		//List<Matrix4x4> matrices = new List<Matrix4x4>(1);
		//List<Vector4> colors = new List<Vector4>();



		//Entities.ForEach((ref Translation tran, ref Rotation rot, ref NonUniformScale scale, ref AntMaterial mat, ref HoldingResource resource) =>
		//{
		//	Matrix4x4 matrix = new Matrix4x4();
		//	matrix.SetTRS(tran.Value, rot.Value, scale.Value);
		//	matrices.Add(matrix);

		//	Vector4 finalColor = resource.Value ? carryColor : searchColor;
		//	finalColor += (finalColor * mat.brightness - mat.currentColor) * .05f;
		//	mat.currentColor = finalColor;

		//	colors.Add(finalColor);
		//});

        

		MaterialPropertyBlock block = new MaterialPropertyBlock();
		block.SetVectorArray("_Color", LevelManager.main.colors.ToArray());

		Graphics.DrawMeshInstanced(mesh, 0, material, LevelManager.main.matrices.ToArray(), LevelManager.main.matrices.Length, block);
        
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
		Color[] pheromonesColors = new Color[LevelManager.Pheromones.Length];
		for (int i = 0; i < LevelManager.Pheromones.Length; ++i)
		{
			pheromonesColors[i] = new Color(LevelManager.Pheromones[i], 0.0f, 0.0f);
		}
		LevelManager.main.pheromoneTexture.SetPixels(pheromonesColors);
		LevelManager.main.pheromoneTexture.Apply();
	}
}
