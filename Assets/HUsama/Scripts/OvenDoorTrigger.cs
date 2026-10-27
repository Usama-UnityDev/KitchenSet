using UnityEngine;

public class OvenDoorTrigger : MonoBehaviour
{
    private void OnMouseDown()
    {
        if (Recipe3Controller.Instance != null)
        {
            Recipe3Controller.Instance.OpenOvenDoor();
        }
    }

}
