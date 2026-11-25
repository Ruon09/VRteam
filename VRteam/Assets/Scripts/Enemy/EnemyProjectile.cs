using UnityEngine;

/// <summary>敵の遠距離攻撃用の弾。プレイヤーのコライダーまたはカメラ位置への到達で命中する。</summary>
public sealed class EnemyProjectile : MonoBehaviour
{
    private EnemyStateMachine owner;
    private Transform target;
    private Vector3 direction;
    private float damage;
    private float speed;
    private float lifetime;
    private float hitRadius;
    private float elapsed;
    private bool resolved;

    public void Initialize(EnemyStateMachine projectileOwner, Transform projectileTarget, Vector3 travelDirection,
        float projectileDamage, float projectileSpeed, float projectileLifetime, float targetHitRadius)
    {
        owner = projectileOwner;
        target = projectileTarget;
        direction = travelDirection.normalized;
        damage = projectileDamage;
        speed = projectileSpeed;
        lifetime = projectileLifetime;
        hitRadius = targetHitRadius;
    }

    private void Update()
    {
        if (resolved) return;
        transform.position += direction * (speed * Time.deltaTime);
        elapsed += Time.deltaTime;

        if (target != null && Vector3.Distance(transform.position, target.position) <= hitRadius)
        {
            HitPlayer();
            return;
        }

        if (elapsed >= lifetime)
        {
            Debug.Log("EnemyProjectile: 射程内にプレイヤーを捉えられず消滅しました。", this);
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (resolved || owner == null || target == null) return;
        if (other.transform.root == owner.transform.root) return;

        if (other.transform == target || other.transform.IsChildOf(target) || other.transform.root == target.root)
        {
            HitPlayer();
            return;
        }

        Debug.Log("EnemyProjectile: 障害物に当たりました。", this);
        Destroy(gameObject);
    }

    private void HitPlayer()
    {
        if (resolved) return;
        resolved = true;
        if (owner != null) owner.ReportProjectileHit(damage);
        Destroy(gameObject);
    }
}
