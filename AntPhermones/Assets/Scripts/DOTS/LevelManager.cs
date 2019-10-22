using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
	public static LevelManager main;

	[SerializeField] public Mesh colonyMesh;
	[SerializeField] Mesh resourceMesh;
	[SerializeField] Material resourceMaterial;
	[SerializeField] Material colonyMaterial;

	[SerializeField] int mapSize = 128;
	public static int MapSize { get { return main.mapSize; } }

	Vector2 resourcePosition;
	public static Vector2 ResourcePosition { get { return main.resourcePosition; } }

	Vector2 colonyPosition;
	public static Vector2 ColonyPosition { get { return main.colonyPosition; } }

	Matrix4x4 resourceMatrix;
	Matrix4x4 colonyMatrix;
	

	void Awake()
    {
		if (main != null && main != this)
		{
			Destroy(this);
			return;
		}

		main = this;

		colonyPosition = Vector2.one * mapSize * .5f;
		colonyMatrix = Matrix4x4.TRS(colonyPosition / mapSize, Quaternion.identity, new Vector3(4f, 4f, .1f) / mapSize);

		float resourceAngle = Random.value * 2f * Mathf.PI;
		resourcePosition = Vector2.one * mapSize * .5f + new Vector2(Mathf.Cos(resourceAngle) * mapSize * .475f, Mathf.Sin(resourceAngle) * mapSize * .475f);
		resourceMatrix = Matrix4x4.TRS(resourcePosition / mapSize, Quaternion.identity, new Vector3(4f, 4f, .1f) / mapSize);
	}

	void Update()
	{
		Graphics.DrawMesh(colonyMesh, colonyMatrix, colonyMaterial, 0);
		Graphics.DrawMesh(resourceMesh, resourceMatrix, resourceMaterial, 0);
	}
}
