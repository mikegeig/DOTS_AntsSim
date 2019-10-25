using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct AntTransform : IComponentData
{
	public float2 position;
	public float facingAngle;
}

public struct MoveSpeed : IComponentData
{
	public float Value;
}

public struct HoldingResource : IComponentData
{
	public bool Value;
}

public struct AntMaterial : IComponentData
{
	public float brightness;
	public float4 currentColor;
}

