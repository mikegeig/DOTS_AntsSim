using UnityEngine;
using UnityEngine.UI;

public class FPS : MonoBehaviour
{
	public Text fpsText;

	float deltaTime;

	void Update()
	{
		deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
		SetFPS();
	}

	void SetFPS()
	{
		float msec = deltaTime * 1000.0f;
		float fps = 1.0f / deltaTime;
		fpsText.text = $"FPS: {(int)fps} ({(int)msec} ms)";
	}
}