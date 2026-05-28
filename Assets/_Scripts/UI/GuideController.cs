using TMPro;
using UnityEngine;

public class GuideController : MonoBehaviour
{
    [SerializeField] private GameObject _guideleft;
    [SerializeField] private GameObject _guideright;
    [SerializeField] private TMP_Text _guideTextLeft;
    [SerializeField] private TMP_Text _guideTextRight;
    
    private void Start()
    {
        _guideleft.SetActive(false);
        _guideright.SetActive(false);
    }
}
