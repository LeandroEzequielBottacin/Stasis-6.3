using Player.Scripts.MovementFSM.MVC;
using Puzzle_Elements.PlataformasCorregidas;
using UnityEngine;

public class TriggerEvent : MonoBehaviour
{
    [SerializeField] private KinematicCargoPlatform _platform;
    private void OnTriggerEnter(Collider other)
    {
        Model player = other.GetComponent<Model>();
        if(player != null)
        {
            _platform.StartMove();
        }
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
