using System.Collections.Generic;
using UnityEngine;

public class SequenceManager : MonoBehaviour
{
    [System.Serializable]
    public class SequenceGroup
    {
        public string name = "Sequence Group";

        public Collider entryCollider;
        public Collider exitCollider;
        public List<SequenceController> sequenceControllers = new();
        public bool isActive = false;

        [System.NonSerialized] public bool playerWasInsideEntry;
        [System.NonSerialized] public bool playerWasInsideExit;
    }

    [Header("Sequence Groups")]
    [SerializeField] private List<SequenceGroup> sequenceGroups = new();

    public List<SequenceGroup> SequenceGroups => sequenceGroups;

    private void Awake()
    {
        foreach (SequenceGroup group in sequenceGroups)
        {
            if (group != null)
                group.isActive = false;
        }
    }

    private void FixedUpdate()
    {
        if (sequenceGroups == null)
            return;

        foreach (SequenceGroup group in sequenceGroups)
        {
            if (group == null)
                continue;

            bool playerInsideEntry = ContainsPlayer(group.entryCollider);
            bool playerInsideExit = ContainsPlayer(group.exitCollider);

            if (playerInsideEntry && !group.playerWasInsideEntry)
                ActivateGroup(group);

            if (playerInsideExit && !group.playerWasInsideExit)
                DeactivateGroup(group);

            group.playerWasInsideEntry = playerInsideEntry;
            group.playerWasInsideExit = playerInsideExit;
        }
    }

    private static bool ContainsPlayer(Collider triggerCollider)
    {
        if (triggerCollider == null ||
            !triggerCollider.enabled ||
            !triggerCollider.gameObject.activeInHierarchy)
        {
            return false;
        }

        Bounds bounds = triggerCollider.bounds;

        Collider[] overlappingColliders = Physics.OverlapBox(
            bounds.center,
            bounds.extents,
            Quaternion.identity,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide
        );

        foreach (Collider other in overlappingColliders)
        {
            if (other == triggerCollider)
                continue;

            if (other.CompareTag("Player"))
                return true;

            if (other.attachedRigidbody != null &&
                other.attachedRigidbody.CompareTag("Player"))
            {
                return true;
            }

            if (other.transform.root.CompareTag("Player"))
                return true;
        }

        return false;
    }

    public void PlayGroup(int index)
    {
        if (!TryGetGroup(index, out SequenceGroup group))
            return;

        PlayGroup(group);
    }

    public void StopGroup(int index)
    {
        if (!TryGetGroup(index, out SequenceGroup group))
            return;

        StopGroup(group);
    }

    public void SetGroupActive(int index, bool active)
    {
        if (!TryGetGroup(index, out SequenceGroup group))
            return;

        if (active)
            ActivateGroup(group);
        else
            DeactivateGroup(group);
    }

    private void ActivateGroup(SequenceGroup group)
    {
        if (group.isActive)
            return;

        group.isActive = true;
        PlayGroup(group);
    }

    private void DeactivateGroup(SequenceGroup group)
    {
        if (!group.isActive)
            return;

        group.isActive = false;
        StopGroup(group);
    }

    private void PlayGroup(SequenceGroup group)
    {
        if (!group.isActive || group.sequenceControllers == null)
            return;

        foreach (SequenceController sequence in group.sequenceControllers)
        {
            if (sequence != null)
                sequence.Play();
        }
    }

    private void StopGroup(SequenceGroup group)
    {
        if (group.sequenceControllers == null)
            return;

        foreach (SequenceController sequence in group.sequenceControllers)
        {
            if (sequence != null)
                sequence.Stop();
        }
    }

    private bool TryGetGroup(int index, out SequenceGroup group)
    {
        group = null;

        if (sequenceGroups == null ||
            index < 0 ||
            index >= sequenceGroups.Count)
        {
            return false;
        }

        group = sequenceGroups[index];
        return group != null;
    }

}