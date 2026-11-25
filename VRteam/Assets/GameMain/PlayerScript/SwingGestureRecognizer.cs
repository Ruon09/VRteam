using UnityEngine;

// Positions/directions use the tracking-origin coordinate system, excluding locomotion.
public sealed class SwingGestureRecognizer
{
    private Vector3 previous, start, forward, up;
    private bool initialized, measuring, armed = true, skillGesture;
    private float startedAt;
    public bool Rejected { get; private set; }
    public void Reset(Vector3 position)
    {
        previous=position;initialized=true;measuring=false;armed=true;Rejected=false;
    }
    public bool Sample(Vector3 position,float now,float deltaTime,bool trigger,bool canAttack,
        Vector3 viewForward,Vector3 viewUp,bool left,float speedThreshold,float minimumDistance,
        float window,float tolerance,out VRSwingSkill.AttackKind kind,out bool isSkill,out Vector3 motion)
    {
        kind=left?VRSwingSkill.AttackKind.LeftJab:VRSwingSkill.AttackKind.RightStraight;
        isSkill=false;motion=Vector3.zero;Rejected=false;
        if(!initialized){Reset(position);return false;}
        Vector3 last=previous,delta=position-previous;previous=position;
        // Discard stalled frames and pose jumps instead of interpreting them as punches.
        if(deltaTime<=0 || deltaTime>.1f || delta.magnitude>.35f)
        {
            measuring=false;armed=false;return false;
        }
        float speed=delta.magnitude/deltaTime;
        if(!canAttack){measuring=false;armed=speed<speedThreshold*.6f;return false;}
        if(!armed && speed<speedThreshold*.6f)armed=true;
        if(!measuring && armed && speed>=speedThreshold)
        {
            measuring=true;armed=false;start=last;startedAt=now;skillGesture=trigger;
            forward=viewForward.normalized;up=viewUp.normalized;
        }
        if(!measuring)return false;
        skillGesture|=trigger;isSkill=skillGesture;motion=position-start;
        if(now-startedAt>window)
        {
            measuring=false;Rejected=motion.magnitude>=minimumDistance;return false;
        }
        if(motion.magnitude<minimumDistance)return false;
        bool valid;
        if(skillGesture)valid=VRSwingSkill.Classify(motion,forward,up,left,tolerance,out kind);
        else valid=Vector3.Angle(motion,forward)<=tolerance;
        // A curved wind-up is allowed to continue until the gesture window expires.
        if(!valid)return false;
        measuring=false;return true;
    }
}
