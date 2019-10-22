using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

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
		MaterialPropertyBlock block = new MaterialPropertyBlock();
		List<Matrix4x4> matrices = new List<Matrix4x4>(1);
		matrices.Add(new Matrix4x4());

		Entities.ForEach((ref Translation tran, ref Rotation rot, ref NonUniformScale scale, ref AntMaterial mat, ref HoldingResource resource) =>
		{
			matrices[0] = new Matrix4x4();
			matrices[0].SetTRS(tran.Value, rot.Value, scale.Value);


			Vector4 finalColor = resource.Value ? (Vector4)carryColor : (Vector4)searchColor;
			finalColor = (finalColor * mat.brightness - mat.currentColor) * .05f;
			mat.currentColor = finalColor;

			block.SetVector("_Color", finalColor);

			Graphics.DrawMeshInstanced(mesh, 0, material, matrices, block);
		});
	}
}
