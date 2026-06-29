using UnityEngine;
using UnityEngine.Events;

namespace Managers.Dialogue_System
{
    public class DialogueTrigger : MonoBehaviour
    {
        [TextArea] public string[] lines;
        public UnityEvent onDialogueComplete;

        private bool alreadyTriggered = false;

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log("A");
            if (!alreadyTriggered && other.CompareTag("Player"))
            {
                Debug.Log("B");
                alreadyTriggered = true;

                // Convertir UnityEvent a UnityAction
                DialogueSystem.Instance.StartDialogue(
                    lines,
                    onDialogueComplete.Invoke
                );
                gameObject.SetActive(false);
            }
        }
        
    }
}