using Player.Scripts.Interactor;
using Puzzle_Elements.Hedro_conteiner.Scripts;
using UnityEngine;

namespace Puzzle_Elements.PanelInteractable
{
    public class InteractablePanel : MonoBehaviour, IInteractable
    {
        [SerializeField] private HedronContainerIn _hedronContainer;

        public void Interact()
        {
            _hedronContainer.Interact();
            Debug.Log("Interactuo con el panel");
        }
    }
}