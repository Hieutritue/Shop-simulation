using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawn khách theo interval, giới hạn bằng maxConcurrent.
/// Mỗi khách được inject exitPoint qua CustomerAgent.Init().
/// </summary>
public class CustomerSpawner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject _customerPrefab;
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private Transform _exitPoint;
    [SerializeField] private CheckoutCounter _checkout;

    [Header("Spawn Config")]
    [SerializeField] private float _spawnInterval = 5f;
    [SerializeField] private int _maxConcurrent = 4;
    [SerializeField] private float _initialDelay = 1f;

    private float _timer;
    private readonly List<CustomerAgent> _alive = new List<CustomerAgent>();

    private void Awake()
    {
        if (_spawnPoint == null) _spawnPoint = transform;
        if (_exitPoint == null) _exitPoint = transform;
        _timer = _initialDelay;
    }

    private void Update()
    {
        _alive.RemoveAll(c => c == null);
        if (_alive.Count >= _maxConcurrent) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            Spawn();
            _timer = _spawnInterval;
        }
    }

    private void Spawn()
    {
        if (_customerPrefab == null) return;
        GameObject go = Instantiate(_customerPrefab, _spawnPoint.position, _spawnPoint.rotation);
        if (go.TryGetComponent<CustomerAgent>(out var agent))
        {
            agent.Init(_exitPoint, _checkout);
            _alive.Add(agent);
        }
    }
}
