using UnityEngine;

public class TestDamageZone : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private BoxCollider area;
    [SerializeField, Min(1)] private int damage = 25;

    private void Update()
    {
        if (player == null || player.Head == null || area == null || !area.enabled)
            return;
        Vector3 localPoint = area.transform.InverseTransformPoint(player.Head.position) - area.center;
        Vector3 halfSize = area.size * 0.5f;
        if (Mathf.Abs(localPoint.x) <= halfSize.x &&
            Mathf.Abs(localPoint.y) <= halfSize.y &&
            Mathf.Abs(localPoint.z) <= halfSize.z)
            player.TryTakeDamage(damage);
    }

    private void OnDrawGizmosSelected()
    {
        if (area == null) return;
        Gizmos.color = Color.red;
        Gizmos.matrix = area.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(area.center, area.size);
    }
}
