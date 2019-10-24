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


		//LevelManager.main.ants = new NativeArray<Ant2>(antData.antCount, Allocator.Persistent);

		for (int i = 0; i < antData.antCount; i++)
		{
			LevelManager.main.ants[i] = new Ant2
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
