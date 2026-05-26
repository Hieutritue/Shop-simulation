using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Follow Target")]
    [SerializeField] private Transform _target;

    [Header("Offset Settings")]
    [SerializeField] private Vector3 _offset = new Vector3(0, 10f, -6f);
    [SerializeField] private float _smoothTime = 0.3f;

    private Vector3 _velocity = Vector3.zero;

    private void Start()
    {
        if (_target == null)
        {
            // Find player automatically if not assigned
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _target = player.transform;
            }
        }
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        // Calculate target camera position
        Vector3 targetPosition = _target.position + _offset;

        // Smoothly move the camera to the target position
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _velocity, _smoothTime);
        
        // Always look at target
        transform.LookAt(_target);
    }
}
