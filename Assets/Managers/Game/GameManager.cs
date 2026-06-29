using System;
using Checkpoint;
using UnityEngine;

namespace Managers.Game
{
    public class GameManager : MonoBehaviour
    {
        public event Action OnDeathPlayer = delegate { };
        public Transform player;
        public float A;
        public static GameManager Instance { get; private set; }
        [SerializeField] private CheckpointSequence _checkpoint;

        private void Awake()
        {
            // Verifica si ya existe una instancia
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // Evita duplicados
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject); // Persiste entre escenas
        }


        public void PlayerDeath()
        {
            Debug.Log("Perdi");
            player.transform.position = _checkpoint.CurrentCheckpointPos();
            OnDeathPlayer?.Invoke();
        }
    }
}
