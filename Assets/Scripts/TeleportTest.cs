using Managers.Game;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_AI_NAVIGATION || ENABLE_NAVMESH
using UnityEngine.AI;
#endif

public class TeleportHotkeys : MonoBehaviour
{
    [Header("Puntos de teletransporte (máx. usados por hotkeys: 9)")]
    [SerializeField] private List<Transform> teleportPoints = new List<Transform>(9);

    [Header("Jugador a teletransportar (fallback si no hay GameManager)")]
    [SerializeField] private GameObject player;

    [Header("Opciones")]
    [Tooltip("Si es true, copia también la rotación del punto.")]
    [SerializeField] private bool matchRotation = true;

    [Tooltip("Pequeño offset vertical para evitar encastar el collider en el piso.")]
    [SerializeField] private float upOffset = 0.05f;

    void Update()
    {
        var p = ResolvePlayer();
        if (p == null || teleportPoints == null || teleportPoints.Count == 0) return;

        int maxKey = Mathf.Min(teleportPoints.Count, 9);
        for (int d = 1; d <= maxKey; d++)
        {
            if (PressedDigit(d))
            {
                Transform target = teleportPoints[d - 1];
                if (target != null)
                    TeleportTo(target);
            }
        }
    }

    private bool PressedDigit(int d)
    {
        // Teclas numéricas superiores y keypad
        KeyCode alpha = (KeyCode)((int)KeyCode.Alpha0 + d);
        KeyCode keypad = (KeyCode)((int)KeyCode.Keypad0 + d);
        return Input.GetKeyDown(alpha) || Input.GetKeyDown(keypad);
    }

    /// <summary>
    /// Resuelve el jugador: primero intenta GameManager.Instance.player; si no existe, usa el serializado.
    /// </summary>
    private GameObject ResolvePlayer()
    {
        // Si tenés tu propio GameManager en otro namespace, ajustá este acceso
        var gm = (object)GameManager.Instance != null ? GameManager.Instance : null;
        if (gm != null && GameManager.Instance.player != null)
            return GameManager.Instance.player.gameObject;

        return player;
    }

    /// <summary>
    /// Si el player tiene padre, lo des-parentea preservando la posición/rotación mundial.
    /// </summary>
    private void EnsureNotChild(GameObject p)
    {
        if (p != null && p.transform.parent != null)
        {
            p.transform.SetParent(null, true); // true = preserva espacio mundo
        }
    }

    private void TeleportTo(Transform target)
    {
        var p = ResolvePlayer();
        if (p == null || target == null) return;

        // Asegurar que no esté parentado a nada antes de moverlo
        EnsureNotChild(p);

        Vector3 destPos = target.position + Vector3.up * upOffset;
        Quaternion destRot = matchRotation ? target.rotation : p.transform.rotation;

        // 1) NavMeshAgent -> Warp
#if UNITY_AI_NAVIGATION || ENABLE_NAVMESH
        var agent = p.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.Warp(destPos);
            p.transform.rotation = destRot;
            Physics.SyncTransforms();
            return;
        }
#endif

        // 2) CharacterController -> deshabilitar, mover, re-habilitar
        var cc = p.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            p.transform.SetPositionAndRotation(destPos, destRot);
            Physics.SyncTransforms();
            cc.enabled = true;
            // Nudge para actualizar suelo
            cc.Move(Vector3.zero);
            return;
        }

        // 3) Rigidbody -> pausar física un instante
        var rb = p.GetComponent<Rigidbody>();
        if (rb != null)
        {
            bool wasKinematic = rb.isKinematic;
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            p.transform.SetPositionAndRotation(destPos, destRot);
            Physics.SyncTransforms();

            rb.isKinematic = wasKinematic;
            return;
        }

        // 4) Transform “a pelo”
        p.transform.SetPositionAndRotation(destPos, destRot);
        Physics.SyncTransforms();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (teleportPoints == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < teleportPoints.Count; i++)
        {
            var t = teleportPoints[i];
            if (t == null) continue;
            Gizmos.DrawWireSphere(t.position, 0.3f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(t.position + Vector3.up * 0.5f, $"{i + 1}");
#endif
        }
    }
#endif
}
