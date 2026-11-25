using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(200)]
public class PunchCombat : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private SphereCollider leftFist;
    [SerializeField] private SphereCollider rightFist;
    [SerializeField] private Vector2 jabWindow = new Vector2(0.1f, 0.25f);
    [SerializeField] private Vector2 crossWindow = new Vector2(0.35f, 0.5f);
    [SerializeField, Min(1)] private int jabDamage = 10;
    [SerializeField, Min(1)] private int crossDamage = 20;
    [SerializeField] private LayerMask targetLayers = ~0;

    [SerializeField, Range(.1f,1f)] private float normalDamageMultiplier = .5f;
    [Header("Straight punch reach assistance (world metres)")]
    [SerializeField, Min(0f)] private float straightReach = .25f;
    [SerializeField, Min(.01f)] private float straightRadius = .12f;
    [Header("Hit visual")]
    [SerializeField] private HitImpactEffect hitEffectPrefab;
    [SerializeField, Min(.1f)] private float hitEffectScale = 1f;
    private Vector3 hitOrigin;
    private bool straightAttack;
    public event System.Action AttackFinished;
    private bool skillAttack;
    private Vector3 gestureDirection;
    public void PrepareAttack(bool isSkill,Vector3 direction){skillAttack=isSkill;gestureDirection=direction.normalized;}
    public void EndAttack()
    {
        if(leftFist!=null)leftFist.enabled=false;
        if(rightFist!=null)rightFist.enabled=false;
        AttackFinished?.Invoke();
    }
    private readonly HashSet<IDamageable> jabHits = new HashSet<IDamageable>();
    private readonly HashSet<IDamageable> crossHits = new HashSet<IDamageable>();
    private Vector3 previousLeft;
    private Vector3 previousRight;
    private bool leftActive;
    private bool rightActive;

    public void BeginAttack()
    {
        jabHits.Clear();
        crossHits.Clear();
        leftActive = rightActive = false;
    }

    private void LateUpdate()
    {
        if(animator==null || leftFist==null || rightFist==null) return;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if(animator.IsInTransition(0)) state=animator.GetNextAnimatorStateInfo(0);
        straightAttack=state.IsName("LeftJab") || state.IsName("RightStraight");
        bool valid=state.normalizedTime<1f;
        bool left=valid && ((state.IsName("LeftJab") && InWindow(state.normalizedTime,new Vector2(.20f,.44f))) || (state.IsName("LeftUppercut") && InWindow(state.normalizedTime,new Vector2(.3f,.58f))));
        bool right=valid && ((state.IsName("RightStraight") && InWindow(state.normalizedTime,new Vector2(.18f,.45f))) || (state.IsName("RightUppercut") && InWindow(state.normalizedTime,new Vector2(.3f,.58f))));
        Physics.SyncTransforms();
        CheckFist(leftFist,left,state.IsName("LeftUppercut")?crossDamage:jabDamage,jabHits,ref previousLeft,ref leftActive);
        CheckFist(rightFist,right,crossDamage,crossHits,ref previousRight,ref rightActive);
    }

    private static bool InWindow(float time, Vector2 window)
    {
        return time >= window.x && time <= window.y;
    }

    private void CheckFist(SphereCollider fist, bool active, int damage,
        HashSet<IDamageable> hits, ref Vector3 previous, ref bool wasActive)
    {
        Vector3 position = fist.transform.TransformPoint(fist.center);
        hitOrigin=position;
        fist.enabled = active;
        if (active)
        {
            Vector3 scale = fist.transform.lossyScale;
            float radius = fist.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Vector3 extension=Vector3.zero;
            if(straightAttack)
            {
                radius=Mathf.Max(radius,straightRadius);
                var view=Camera.main;
                Vector3 forward=Vector3.ProjectOnPlane(view!=null?view.transform.forward:transform.forward,Vector3.up);
                if(forward.sqrMagnitude<.0001f)forward=Vector3.ProjectOnPlane(transform.forward,Vector3.up);
                extension=forward.normalized*Mathf.Max(0,straightReach);
            }
            // Keep the original fist volume and extend only towards the opponent.
            var contacts=extension.sqrMagnitude>.000001f
                ? Physics.OverlapCapsule(position,position+extension,radius,targetLayers,QueryTriggerInteraction.Collide)
                : Physics.OverlapSphere(position,radius,targetLayers,QueryTriggerInteraction.Collide);
            foreach(var collider in contacts)Hit(collider,damage,hits);
            Vector3 motion = position - previous;
            if (wasActive && motion.sqrMagnitude > 0.000001f)
            {
                foreach (var hit in Physics.SphereCastAll(previous, radius, motion.normalized,
                    motion.magnitude, targetLayers, QueryTriggerInteraction.Collide))
                    Hit(hit.collider, damage, hits);
                if(extension.sqrMagnitude>.000001f)
                    foreach(var hit in Physics.SphereCastAll(previous+extension,radius,motion.normalized,
                        motion.magnitude,targetLayers,QueryTriggerInteraction.Collide))
                        Hit(hit.collider,damage,hits);
            }
        }
        previous = position;
        wasActive = active;
    }

    private void Hit(Collider collider, int damage, HashSet<IDamageable> hits)
    {
        if (collider.transform.IsChildOf(transform))
            return;
        var target = collider.GetComponentInParent<IDamageable>();
        if (target != null && target != GetComponentInParent<IDamageable>() && hits.Add(target))
        {
            float multiplier=skillAttack?1f:normalDamageMultiplier;
            var weak=collider.GetComponentInParent<WeakPointTarget>();
            float bonus=skillAttack && weak!=null?weak.Multiplier(gestureDirection,Camera.main):1f;
            if(target.TryTakeDamage(Mathf.Max(1,Mathf.RoundToInt(damage*multiplier*bonus))))
            {
                if(bonus>1f)weak.ShowHit(bonus);
                SpawnImpact(collider);
            }
        }
    }

    private void SpawnImpact(Collider collider)
    {
        if(hitEffectPrefab==null)return;
        var camera=Camera.main;
        Vector3 point=collider.ClosestPoint(hitOrigin);
        Vector3 origin=camera!=null?camera.transform.position:hitOrigin;
        Vector3 direction=point-origin;
        if(direction.sqrMagnitude<.000001f)direction=collider.bounds.center-origin;
        if(direction.sqrMagnitude>.000001f && collider.Raycast(new Ray(origin,direction.normalized),out var surface,direction.magnitude+collider.bounds.size.magnitude))point=surface.point;
        Vector3 towardsViewer=(origin-point).normalized;
        var effect=Instantiate(hitEffectPrefab,point+towardsViewer*.015f,Quaternion.identity);
        effect.Initialize(camera,hitEffectScale*(skillAttack?1.15f:1f));
    }

    private void OnDisable()
    {
        if (leftFist != null) leftFist.enabled = false;
        if (rightFist != null) rightFist.enabled = false;
        BeginAttack();
        EndAttack();
    }
}
