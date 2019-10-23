﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Entities;

public class KeyboardInput : MonoBehaviour {
	Text text;

	static bool showText=true;

	void Start () {
		text = GetComponent<Text>();
		text.enabled = showText;
	}
	
	void Update () {

		if (Input.GetKeyDown(KeyCode.Alpha1))
		{
			Time.timeScale = 1f;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha2))
		{
			Time.timeScale = 2f;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha3))
		{
			Time.timeScale = 3f;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha4))
		{
			Time.timeScale = 4f;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha5))
		{
			Time.timeScale = 5f;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha6))
		{
			Time.timeScale = 6f;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha7))
		{
			Time.timeScale = 7f;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha8))
		{
			Time.timeScale = 8f;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha9))
		{
			Time.timeScale = 9f;
		}

		if (Input.GetKeyDown(KeyCode.H)) {
			showText = !showText;
			text.enabled = showText;
		}
		if (Input.GetKeyDown(KeyCode.R)) {
			Time.timeScale = 1f;

            World.Active.EntityManager.DestroyEntity(World.Active.EntityManager.GetAllEntities());

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            
		}

		if (Input.GetButtonDown("Cancel"))
			Application.Quit();
	}
}
