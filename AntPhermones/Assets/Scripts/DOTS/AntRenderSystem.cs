using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateAfter(typeof(AntTransformUpdateSystem))]
public class AntRenderSystem : ComponentSystem
{
	AntSpawner spawner;

	protected override void OnCreate()
	{
		spawner = GameObject.FindObjectOfType<AntSpawner>();
	}

	protected override void OnUpdate()
	{
		Mesh mesh = spawner.antMesh;
		Material material = spawner.antMaterial;
		Color searchColor = spawner.searchColor;
		Color carryColor = spawner.carryColor;
		List<Matrix4x4> matrices = new List<Matrix4x4>(1);
		List<Vector4> colors = new List<Vector4>();

		Entities.ForEach((ref Translation tran, ref Rotation rot, ref NonUniformScale scale, ref AntMaterial mat, ref HoldingResource resource) =>
		{
			Matrix4x4 matrix = new Matrix4x4();
			matrix.SetTRS(tran.Value, rot.Value, scale.Value);
			matrices.Add(matrix);

			Vector4 finalColor = resource.Value ? (Vector4)carryColor : (Vector4)searchColor;
			finalColor = (finalColor * mat.brightness - mat.currentColor) * .05f;
			mat.currentColor = finalColor;
			colors.Add(finalColor);
		});

		MaterialPropertyBlock block = new MaterialPropertyBlock();
		block.SetVectorArray("_Color", colors);

		Graphics.DrawMeshInstanced(mesh, 0, material, matrices, block);
	}
}
