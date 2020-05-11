using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour {

    public GameObject target = null;
    public float lerpSpeed = 5.0f;
	public Vector2 cameraSize;

	private Bounds _bounds = new Bounds(Vector3.zero, Vector3.positiveInfinity);
	private Vector3 targetPosition = Vector3.zero;	


	private void Awake()
	{
		targetPosition = transform.position;
		RefreshTargetPosition();
	}

	// Update is called once per frame
	void Update () {
		RefreshTargetPosition();
		Vector3 position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lerpSpeed);
        transform.position = (Vector3)position;
    }

	void RefreshTargetPosition()
	{
		if (target == null)
			return;
		targetPosition = KeepInBounds(target.transform.position);
		targetPosition.z = transform.position.z;
	}

    Vector3 KeepInBounds(Vector3 point)
    {
		float z = point.z;
		point.z = 0;
		if (!_bounds.Contains(point))
        {            
			point = _bounds.ClosestPoint(point);
        }
		point.z = z;
		return point;
	}

    public void SetBounds(Bounds bounds)
    {
        _bounds = bounds;
		_bounds.Expand(-cameraSize);
		targetPosition = KeepInBounds(targetPosition);
    }

	public void SnapToTarget()
	{
		RefreshTargetPosition();
		transform.position = targetPosition;
	}

}
