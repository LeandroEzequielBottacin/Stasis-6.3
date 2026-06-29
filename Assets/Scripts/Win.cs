using Player.Scripts.MovementFSM.MVC;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Win : MonoBehaviour
{
    [SerializeField] private GameObject _gOWin;


    private void OnCollisionEnter(Collision collision)
    {
        Model player = collision.gameObject.GetComponent<Model>();
        if(player != null)
        {
            _gOWin.SetActive(true);
        }
    }
}
