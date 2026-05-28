// csharp

using TMPro;
using UnityEngine;

public class HUDController : MonoBehaviour
{
    [SerializeField] private GameObject _crosshairInteractable;
    [SerializeField] private GameObject _crosshairNonInteractable;
    
    [SerializeField] private TMP_Text _moneyText;

    private PlayerInteract _playerInteract;

    private void Awake()
    {
        _playerInteract = FindObjectOfType<PlayerInteract>();
        if (_playerInteract == null)
        {
            Debug.LogWarning("PlayerInteract not found in scene. HUD crosshair will not update.");
        }
    }

    private void Update()
    {
        if (_playerInteract == null) return;

        bool looking = _playerInteract.IsLookingAtInteractable;

        if (_crosshairInteractable != null)
            _crosshairInteractable.SetActive(looking);

        if (_crosshairNonInteractable != null)
            _crosshairNonInteractable.SetActive(!looking);
    }
}