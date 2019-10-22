using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class AntSpawner : MonoBehaviour
{
	public GameObject antPrefab;
	public int antCount;

	EntityManager manager;
	Entity antPrefabDOTS;

	private void Start()
	{
		antPrefabDOTS = GameObjectConversionUtility.ConvertGameObjectHierarchy(antPrefab, World.Active);
		manager = World.Active.EntityManager;

		using (NativeArray<Entity> ants = new NativeArray<Entity>(antCount, Allocator.TempJob))
		{
			manager.Instantiate(antPrefabDOTS, ants);

			for (int i = 0; i < antCount; i++)
			{
				AntComponent ant = new AntComponent
				{
					position = new Vector2(Random.Range(-5f, 5f) + LevelManager.MapSize * .5f, Random.Range(-5f, 5f) + LevelManager.MapSize * .5f),
					facingAngle = Random.value * Mathf.PI * 2f,
					speed = 0f,
					holdingResource = false,
					brightness = Random.Range(.75f, 1.25f)

				};

				manager.AddComponentData(ants[i], ant);
			}
		}	
	}
}
