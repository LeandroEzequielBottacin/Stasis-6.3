using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

public class ScriptFin : MonoBehaviour
{
    [SerializeField] private GameObject _panel; 
    private void OnTriggerEnter(Collider other)
    {
        Model player = other.GetComponent<Model>();
        if(player != null)
        {
            _panel.gameObject.SetActive(true);
        }
    }
}
