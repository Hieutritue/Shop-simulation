using UnityEngine;

public class PlayerCarry : MonoBehaviour
{
    // This is a stub for now so IInteractable and other scripts compile.
    // We will expand this in Module 6.
    public GameObject HeldObject { get; private set; }
    public Transform CarryPoint;

    public void CarryObject(GameObject obj)
    {
        HeldObject = obj;
    }

    public void ClearHeldObject()
    {
        HeldObject = null;
    }
}
