﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Entities;

public class KeyboardInput : MonoBehaviour {

	Canvas canvas;
	public Text sceneName;
	int currentSceneIndex;


	void Start () {

		canvas = GetComponent<Canvas>();

		currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
		sceneName.text = SceneManager.GetActiveScene().name;
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
			canvas.enabled = !canvas.enabled;
		}
		if (Input.GetKeyDown(KeyCode.R)) {
			Time.timeScale = 1f;

            World.Active.EntityManager.DestroyEntity(World.Active.EntityManager.GetAllEntities());

            SceneManager.LoadScene(currentSceneIndex);
            
		}

		if (Input.GetKeyDown(KeyCode.C))
		{
			Time.timeScale = 1f;

			World.Active.EntityManager.DestroyEntity(World.Active.EntityManager.GetAllEntities());

			currentSceneIndex = currentSceneIndex == 0 ? 1 : 0;
			SceneManager.LoadScene(currentSceneIndex);
		}

		if (Input.GetButtonDown("Cancel"))
			Application.Quit();
	}
}
