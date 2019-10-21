using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public struct AntComponent : IComponentData
{
	public Vector2 position;
	public float facingAngle;
	public float speed;
	public bool holdingResource;
	public float brightness;
}
