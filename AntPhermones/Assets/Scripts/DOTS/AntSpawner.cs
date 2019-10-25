using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class AntSpawner : MonoBehaviour
{
	EntityManager manager;

	void Start()
	{
		AntConfigData antData = LevelManager.AntData;
		Color antColor = LevelManager.RenderData.searchColor;

		int mapSize = LevelManager.LevelData.mapSize;

		manager = World.Active.EntityManager;

        EntityArchetype archetype = manager.CreateArchetype(
            typeof(AntTransform), 
            typeof(MoveSpeed), 
            typeof(HoldingResource), 
            typeof(AntMaterial),
            typeof(NonUniformScale));
        
        
        using (NativeArray<Entity> ants = new NativeArray<Entity>(antData.antCount, Allocator.TempJob))
        {
            manager.CreateEntity(archetype, ants);

            for (int i = 0; i < antData.antCount; i++)
            {
                AntTransform ant = new AntTransform
                {
                    position = new Vector2(Random.Range(-5f, 5f) + mapSize * .5f, Random.Range(-5f, 5f) + mapSize * .5f),
                    facingAngle = Random.value * Mathf.PI * 2f
                };

                MoveSpeed speed = new MoveSpeed { Value = 0f };
                HoldingResource resource = new HoldingResource { Value = false };
                AntMaterial brightness = new AntMaterial
                {
                    currentColor = new Unity.Mathematics.float4(antColor.r, antColor.g, antColor.b, Random.Range(.75f, 1.25f))
                };

                manager.SetComponentData(ants[i], ant);
                manager.SetComponentData(ants[i], speed);
                manager.SetComponentData(ants[i], resource);
                manager.SetComponentData(ants[i], brightness);
                manager.SetComponentData(ants[i], new NonUniformScale()
                {
                    Value = new Unity.Mathematics.float3(antData.antPrefab.transform.localScale)
                });
            }
        }
    }
}
