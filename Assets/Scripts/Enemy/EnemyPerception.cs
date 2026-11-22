using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyPerception : MonoBehaviour
{
    [SerializeField, Min(0f)] private float sightRange = 4.5f;
    [SerializeField, Min(0f)] private float heightTolerance = 1.2f;
    [SerializeField] private LayerMask obstacleMask = 1;

    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
    private int visionBlockerLayer;

    private void Awake() => visionBlockerLayer = LayerMask.NameToLayer("VisionBlocker");

    public bool CanSee(PlayerHiding target, int facingDirection)
    {
        if (target == null || !target.isActiveAndEnabled || target.IsHidden || target.IsCatchLocked)
            return false;

        Vector2 offset = target.transform.position - transform.position;
        return offset.sqrMagnitude <= sightRange * sightRange
            && Mathf.Abs(offset.y) <= heightTolerance
            && offset.x * facingDirection >= 0f
            && HasClearLineTo(target.transform);
    }

    public bool HasClearLineTo(Transform target)
    {
        return IsLineClear(target.position, target, true);
    }

    internal bool HasFreePathTo(float x, Transform target)
    {
        return IsLineClear(new Vector2(x, transform.position.y), target, false);
    }

    private bool IsLineClear(Vector2 destination, Transform target, bool includeCurtains)
    {
        Vector2 origin = transform.position;
        Vector2 offset = destination - origin;
        float distance = offset.magnitude;
        if (distance < 0.001f)
            return true;

        var filter = new ContactFilter2D { useTriggers = includeCurtains };
        int mask = obstacleMask.value;
        if (visionBlockerLayer >= 0)
            mask |= 1 << visionBlockerLayer;
        filter.SetLayerMask(mask);
        Physics2D.Raycast(origin, offset / distance, filter, hits, distance);
        foreach (RaycastHit2D hit in hits)
        {
            Collider2D collider = hit.collider;
            if (collider.transform.IsChildOf(transform) || collider.transform.IsChildOf(target))
                continue;
            if (!collider.isTrigger || (includeCurtains && collider.gameObject.layer == visionBlockerLayer))
                return false;
        }
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
    }
}
