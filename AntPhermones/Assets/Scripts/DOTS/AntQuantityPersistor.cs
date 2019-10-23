using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AntQuantityPersistor : MonoBehaviour
{
	static AntQuantityPersistor instance;
	public static AntQuantityPersistor Instance {
		get {
			if (instance == null)
			{
				GameObject obj = new GameObject("Ant Quantity Persistor");
				DontDestroyOnLoad(obj);
				obj.AddComponent<AntQuantityPersistor>();
				instance = obj.GetComponent<AntQuantityPersistor>();
				return instance;
			}

			return instance;
		}
	}

	public int antCount;
}
