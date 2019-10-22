using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public struct AntTransform : IComponentData
{
	public Vector2 position;
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

public struct Brightness : IComponentData
{
	public float Value;
}

public struct ObstacleComponent : IComponentData
{
	public Vector2 position;
	public float radius;
}
