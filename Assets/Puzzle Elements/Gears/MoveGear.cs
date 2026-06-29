using UnityEngine;

public class MoveGear : MonoBehaviour
{
    public bool canRotate = true;
    // Start is called before the first frame update
    public void CanRotate()
    {
        canRotate = true;
    }

    public void CanTRotate()
    {
        canRotate = false;
    }
}
