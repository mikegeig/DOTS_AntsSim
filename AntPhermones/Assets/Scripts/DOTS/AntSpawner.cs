using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class AntSpawner : MonoBehaviour
{
	public GameObject antPrefab;
	public Mesh antMesh;
	public Material antMaterial;
	public int antCount;
	public Color searchColor;
	public Color carryColor;

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
				AntTransform ant = new AntTransform
				{
					position = new Vector2(Random.Range(-5f, 5f) + LevelManager.MapSize * .5f, Random.Range(-5f, 5f) + LevelManager.MapSize * .5f),
					facingAngle = Random.value * Mathf.PI * 2f
				};

				MoveSpeed speed = new MoveSpeed { Value = 0f };
				HoldingResource resource = new HoldingResource { Value = false };
				AntMaterial brightness = new AntMaterial
				{
					brightness = Random.Range(.75f, 1.25f),
					currentColor = carryColor
				};

				manager.AddComponentData(ants[i], ant);
				manager.AddComponentData(ants[i], speed);
				manager.AddComponentData(ants[i], resource);
				manager.AddComponentData(ants[i], brightness);
			}
		}	
	}
}
