using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
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

	public int obstacleRingCount;
	[Range(0f,1f)]
	public float obstaclesPerRing;
	public float obstacleRadius;
	

    NativeArray<float> pheromones;
    public float trailAddSpeed = 0.3f;
    public float trailDecay = 0.9985f;

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

        // Pheromones
        pheromones = new NativeArray<float>(mapSize * mapSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
    }

	void Update()
	{
		Graphics.DrawMesh(colonyMesh, colonyMatrix, colonyMaterial, 0);
		Graphics.DrawMesh(resourceMesh, resourceMatrix, resourceMaterial, 0);
	}

    private void OnDestroy()
    {
        pheromones.Dispose();
    }


    int PheromoneIndex(int x, int y)
    {
        return x + y * mapSize;
    }

    void DropPheromones(Vector2 position, float strength)
    {
        int x = Mathf.FloorToInt(position.x);
        int y = Mathf.FloorToInt(position.y);
        if (x < 0 || y < 0 || x >= mapSize || y >= mapSize)
        {
            return;
        }

        int index = PheromoneIndex(x, y);
        pheromones[index] += (trailAddSpeed * strength * Time.fixedDeltaTime) * (1f - pheromones[index]);
        if (pheromones[index] > 1f)
        {
            pheromones[index] = 1f;
        }
    }

    float PheromoneSteering(Ant ant, float distance)
    {
        float output = 0;

        for (int i = -1; i <= 1; i += 2)
        {
            float angle = ant.facingAngle + i * Mathf.PI * .25f;
            float testX = ant.position.x + Mathf.Cos(angle) * distance;
            float testY = ant.position.y + Mathf.Sin(angle) * distance;

            if (testX < 0 || testY < 0 || testX >= mapSize || testY >= mapSize)
            {

            }
            else
            {
                int index = PheromoneIndex((int)testX, (int)testY);
                float value = pheromones[index];
                output += value * i;
            }
        }
        return Mathf.Sign(output);
    }

    void DecayPheromones()
    {
        for (int x = 0; x < mapSize; x++)
        {
            for (int y = 0; y < mapSize; y++)
            {
                int index = PheromoneIndex(x, y);
                pheromones[index] *= trailDecay;
            }
        }
    }
}
