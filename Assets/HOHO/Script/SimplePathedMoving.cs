using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SimplePathedMoving : MonoBehaviour {
	public bool use = false;
	public bool isLoop=true;
	public Vector3[] localWaypoints;
	Vector3[] globalWaypoints;

	public float speed = 2;
	public bool cyclic;
	public float waitTime;
	[Range(0,2)]
	public float easeAmount = 0.5f;

	int fromWaypointIndex;
	float percentBetweenWaypoints;
	float nextMoveTime;

	[HideInInspector]
	public bool allowMoving = true;

	[HideInInspector]
	public bool finishWay = false;

    public void Start () {
		if (!use)
			Destroy(this);

		globalWaypoints = new Vector3[localWaypoints.Length];
		for (int i =0; i < localWaypoints.Length; i++) {
			globalWaypoints[i] = localWaypoints[i] + transform.position;
		}
	}

	void FixedUpdate () {
		if (!allowMoving)
			return;
		
		Vector3 velocity = CalculatePlatformMovement();
		transform.Translate (velocity);
	}

	float Ease(float x) {
		float a = easeAmount + 1;
		return Mathf.Pow(x,a) / (Mathf.Pow(x,a) + Mathf.Pow(1-x,a));
	}
	
	Vector3 CalculatePlatformMovement() {

		if (Time.time < nextMoveTime) {
			return Vector3.zero;
		}

		fromWaypointIndex %= globalWaypoints.Length;
		int toWaypointIndex = (fromWaypointIndex + 1) % globalWaypoints.Length;
		float distanceBetweenWaypoints = Vector3.Distance (globalWaypoints [fromWaypointIndex], globalWaypoints [toWaypointIndex]);
		percentBetweenWaypoints += Time.deltaTime * speed/distanceBetweenWaypoints;
		percentBetweenWaypoints = Mathf.Clamp01 (percentBetweenWaypoints);
		float easedPercentBetweenWaypoints = Ease (percentBetweenWaypoints);

		Vector3 newPos = Vector3.Lerp (globalWaypoints [fromWaypointIndex], globalWaypoints [toWaypointIndex], easedPercentBetweenWaypoints);

		if (percentBetweenWaypoints >= 1) {
			percentBetweenWaypoints = 0;
			fromWaypointIndex ++;

			if(fromWaypointIndex >= globalWaypoints.Length-1){
				finishWay = true;
				if (!isLoop) {
					enabled = false;
				}
			}

			if (!cyclic) {
				if (fromWaypointIndex >= globalWaypoints.Length-1) {
					fromWaypointIndex = 0;
					System.Array.Reverse(globalWaypoints);
				}
			}
			nextMoveTime = Time.time + waitTime;
		}

		return newPos - transform.position;
	}

	void OnDrawGizmos() {
		if (!use)
			return;

		if (!Application.isPlaying && localWaypoints != null) {
			Gizmos.color = Color.red;
			float size = .3f;
			globalWaypoints = new Vector3[localWaypoints.Length];
			for (int i =0; i < localWaypoints.Length; i ++) {
				Vector3 globalWaypointPos = (Application.isPlaying)?globalWaypoints[i] : localWaypoints[i] + transform.position;
				Gizmos.DrawLine(globalWaypointPos - Vector3.up * size, globalWaypointPos + Vector3.up * size);
				Gizmos.DrawLine(globalWaypointPos - Vector3.left * size, globalWaypointPos + Vector3.left * size);
			}
		}
	}
}
