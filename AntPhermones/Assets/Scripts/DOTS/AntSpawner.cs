using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class AntSpawner : MonoBehaviour
{
	EntityManager manager;
	Entity antPrefabDOTS;

	void Start()
	{
		AntConfigData antData = LevelManager.AntData;
		Color antColor = LevelManager.RenderData.searchColor;

		int mapSize = LevelManager.LevelData.mapSize;

		antPrefabDOTS = GameObjectConversionUtility.ConvertGameObjectHierarchy(antData.antPrefab, World.Active);
		manager = World.Active.EntityManager;

		using (NativeArray<Entity> ants = new NativeArray<Entity>(antData.antCount, Allocator.TempJob))
		{
			manager.Instantiate(antPrefabDOTS, ants);

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
					brightness = Random.Range(.75f, 1.25f),
					currentColor = antColor
				};

				manager.AddComponentData(ants[i], ant);
				manager.AddComponentData(ants[i], speed);
				manager.AddComponentData(ants[i], resource);
				manager.AddComponentData(ants[i], brightness);
			}
		}	
	}
}
