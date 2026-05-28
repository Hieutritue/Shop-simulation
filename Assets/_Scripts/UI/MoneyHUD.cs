using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Hiển thị tổng tiền hiện tại. Subscribe MoneyChangedEvent qua EventBus.
/// Adds a small transform "juice" when money changes (PrimeTween if available, fallback coroutine).
/// </summary>
public class MoneyHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text _moneyText;

    [Header("Pop tween settings")]
    [Tooltip("Scale multiplier for the pop (1 = no change)")]
    [SerializeField] private float _popScale = 1.18f;
    [Tooltip("Total pop duration in seconds")]
    [SerializeField] private float _popDuration = 0.18f;

    private Coroutine _popCoroutine;
    private Vector3 _originalScale;

    private void Awake()
    {
        _originalScale = transform.localScale;
    }

    private void OnEnable() => EventBus.Subscribe<MoneyChangedEvent>(OnMoneyChanged);
    private void OnDisable() => EventBus.Unsubscribe<MoneyChangedEvent>(OnMoneyChanged);

    private void OnMoneyChanged(MoneyChangedEvent evt)
    {
        if (_moneyText != null) _moneyText.text = evt.NewTotal.ToString();

        // Try PrimeTween first, otherwise fallback
        if (!TryPrimeTweenPop())
        {
            if (_popCoroutine != null) StopCoroutine(_popCoroutine);
            _popCoroutine = StartCoroutine(PopCoroutine());
        }
    }

    // Attempt to find a PrimeTween scale method at runtime and use it.
    // Returns true if invoked; false if PrimeTween not found or signature didn't match.
    private bool TryPrimeTweenPop()
    {
        try
        {
            // look for a type named PrimeTween anywhere in loaded assemblies
            var primeType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); } catch { return Type.EmptyTypes; }
                })
                .FirstOrDefault(t => string.Equals(t.Name, "PrimeTween", StringComparison.OrdinalIgnoreCase));

            if (primeType == null) return false;

            // find a static method whose name contains "scale" and accepts 3 parameters (subject, Vector3, float)
            var methods = primeType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            var scaleMethod = methods.FirstOrDefault(m =>
            {
                var nameMatch = m.Name.IndexOf("scale", StringComparison.OrdinalIgnoreCase) >= 0;
                var parms = m.GetParameters();
                if (!nameMatch || parms.Length != 3) return false;
                // first param can be Transform or GameObject, second Vector3, third float/double
                var p0 = parms[0].ParameterType;
                var p1 = parms[1].ParameterType;
                var p2 = parms[2].ParameterType;
                bool p0ok = p0 == typeof(Transform) || p0 == typeof(GameObject) || typeof(Component).IsAssignableFrom(p0);
                bool p1ok = p1 == typeof(Vector3);
                bool p2ok = p2 == typeof(float) || p2 == typeof(double);
                return p0ok && p1ok && p2ok;
            });

            if (scaleMethod == null) return false;

            // call scale up, then schedule scale back
            var half = _popDuration * 0.5f;
            object subjectArg = scaleMethod.GetParameters()[0].ParameterType == typeof(GameObject) ? (object)gameObject : (object)transform;

            // Invoke scale up
            scaleMethod.Invoke(null, new object[] { subjectArg, _originalScale * _popScale, half });

            // schedule revert after half duration
            StartCoroutine(InvokeAfter(half, () =>
            {
                try
                {
                    scaleMethod.Invoke(null, new object[] { subjectArg, _originalScale, half });
                }
                catch { /* swallow */ }
            }));

            return true;
        }
        catch
        {
            return false;
        }
    }

    private IEnumerator InvokeAfter(float wait, Action action)
    {
        yield return new WaitForSeconds(wait);
        action?.Invoke();
    }

    // Fallback pop coroutine: smooth scale up then back with an ease-out then ease-in
    private IEnumerator PopCoroutine()
    {
        var target = _originalScale * _popScale;
        var half = _popDuration * 0.5f;

        // scale up (ease out)
        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / half);
            // EaseOutBack-like curve (overshoot handled by popScale already)
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            transform.localScale = Vector3.LerpUnclamped(_originalScale, target, eased);
            yield return null;
        }

        // scale back (ease in)
        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / half);
            float eased = Mathf.Pow(k, 2f); // ease in
            transform.localScale = Vector3.LerpUnclamped(target, _originalScale, eased);
            yield return null;
        }

        transform.localScale = _originalScale;
        _popCoroutine = null;
    }
}
