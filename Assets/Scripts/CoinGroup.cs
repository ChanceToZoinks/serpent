﻿using UnityEngine;
using System.Collections;

public class CoinGroup : MonoBehaviour {

	public float phaseDelta = 45;

	void Awake () {
		int n = 0;
		foreach (Transform coin in transform) {
			coin.Rotate (0, n * phaseDelta, 0);
			n++;
		}
	}
	
	void Update () {
	
	}
}