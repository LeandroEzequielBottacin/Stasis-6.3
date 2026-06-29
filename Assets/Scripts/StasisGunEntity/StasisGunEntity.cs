using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Player.Stasis;
using Player.Scripts.Interactor;
using Fracture.Destruction_System.Refractored;   // <- Necesario para DestroyedPieceController

public class StasisGunEntity : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Transform stasisOrigin;
    [SerializeField] private GameObject stasisBeamPrefab;
    [SerializeField] private float beamDuration = 0.2f;

    private StasisBeam _activeBeam;
    private Coroutine _beamCoroutine;

    [Header("Targets")]
    public List<GameObject> targets = new List<GameObject>();

    // ---------------------------------
    // API
    // ---------------------------------
    public IEnumerator waitTryStasis(Transform end, DestroyedPieceController part)
    {
        yield return new WaitForSeconds(0.5f);
        TryStasis(end, part);
    }

    public void StasisAllObjects()
    {
        foreach (var item in targets)
            TryStasis(item.transform);
    }

    // ---------------------------------
    // TRY STASIS (SIMPLE)
    // ---------------------------------
    public void TryStasis(Transform end)
    {
        if (!end) return;

        Vector3 direction = (end.position - stasisOrigin.position).normalized;

        if (Physics.Raycast(stasisOrigin.position, direction, out RaycastHit hit, Mathf.Infinity))
        {
            ProcessHitForStasis(hit);
            SpawnBeam(hit.point, direction);
        }
    }

    // ---------------------------------
    // TRY STASIS (CON PIEZA)
    // ---------------------------------
    public void TryStasis(Transform end, DestroyedPieceController part)
    {
        if (!end) return;

        Vector3 direction = (end.position - stasisOrigin.position).normalized;

        if (Physics.Raycast(stasisOrigin.position, direction, out RaycastHit hit, Mathf.Infinity))
        {
            // Caso especial: No le pegaste directamente a la parte rota
            if (part && part.gameObject != hit.collider.gameObject)
            {
                if (part.TryGetComponent<IStasis>(out var stasisPart))
                    ToggleStasis(stasisPart);
            }

            ProcessHitForStasis(hit);
            SpawnBeam(hit.point, direction);
        }
    }

    // ---------------------------------
    // BUSCAR IStasis
    // ---------------------------------
    private void ProcessHitForStasis(RaycastHit hit)
    {
        GameObject hitObj = hit.collider.gameObject;

        IStasis stasis = null;

        // 1) Collider directo
        hitObj.TryGetComponent(out stasis);

        // 2) Padres
        if (stasis == null)
            stasis = hit.collider.GetComponentInParent<IStasis>();

        // 3) Hijos
        if (stasis == null)
            stasis = hit.collider.GetComponentInChildren<IStasis>();

        if (stasis != null)
        {
            ToggleStasis(stasis);
            Debug.Log($"[EntityStasisGun] Stasis toggled en {hitObj.name}");
        }
        else
        {
            Debug.Log($"[EntityStasisGun] NO se encontró IStasis en {hitObj.name}");
        }
    }

    // ---------------------------------
    // ACTIVAR / DESACTIVAR STASIS
    // ---------------------------------
    private void ToggleStasis(IStasis stasis)
    {
        if (stasis.IsFreezed)
            stasis.StatisEffectDeactivate();   
        else
            stasis.StatisEffectActivate();     
    }

    // ---------------------------------
    // BEAM
    // ---------------------------------
    private void SpawnBeam(Vector3 hitPoint, Vector3 direction)
    {
        GameObject beamInstance = Instantiate(
            stasisBeamPrefab,
            stasisOrigin.position,
            Quaternion.LookRotation(direction)
        );

        _activeBeam = beamInstance.GetComponent<StasisBeam>();
        _activeBeam.SetBeam(stasisOrigin.position, hitPoint, true);

        if (_beamCoroutine != null)
            StopCoroutine(_beamCoroutine);

        _beamCoroutine = StartCoroutine(DisableBeamAfterDuration(beamDuration));
    }

    private IEnumerator DisableBeamAfterDuration(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (_activeBeam)
        {
            Destroy(_activeBeam.gameObject);
            _activeBeam = null;
        }
    }
}





