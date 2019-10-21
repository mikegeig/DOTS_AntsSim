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
	public int mapSize = 128;

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
					position = new Vector2(Random.Range(-5f, 5f) + mapSize * .5f, Random.Range(-5f, 5f) + mapSize * .5f),
					facingAngle = Random.value * Mathf.PI * 2f,
					speed = 0f,
					holdingResource = false,
					brightness = Random.Range(.75f, 1.25f)

				};

				Translation trans = new Translation { Value = new Unity.Mathematics.float3(ant.position.x / mapSize, ant.position.y / mapSize, 0f) };

				manager.AddComponentData(ants[i], ant);
				manager.SetComponentData(ants[i], trans);
			}
		}	
	}
}
