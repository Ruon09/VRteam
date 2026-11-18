using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class VRSwingSkill : MonoBehaviour
{
    public enum AttackKind { LeftJab, RightStraight, LeftUppercut, RightUppercut }
    [SerializeField] private InputActionReference skillButton;
    [SerializeField] private Transform swingGuide;
    [SerializeField] private Transform leftController;
    [SerializeField] private Transform head;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private PunchCombat combat;
    [SerializeField] private PlayerActionLock actionLock;
    [SerializeField] private LineRenderer[] attackGuides = new LineRenderer[0];
    [SerializeField, Min(.01f)] private float swingThreshold = 2f;
    [SerializeField, Min(.01f)] private float swingWindow = .18f;
    [SerializeField, Min(.01f)] private float minimumSwingDistance = .18f;
    [SerializeField, Range(0,90)] private float directionTolerance = 40f;
    [SerializeField] private float[] skillCooldownSeconds = {3f,3f,3f,3f};
    private readonly float[] readyAt = new float[4];
    private readonly Vector3[] previous = new Vector3[2], start = new Vector3[2], forward = new Vector3[2], up = new Vector3[2];
    private readonly float[] started = new float[2];
    private readonly bool[] measuring = new bool[2], fast = new bool[2], skillGesture = new bool[2];
    private float feedbackUntil;
    private int feedbackIndex = -1;
    private Color feedbackColor;
    private MaterialPropertyBlock properties;
    public bool IsSkillActive { get; private set; }
    public static string StateName(AttackKind kind) => kind.ToString();
    public float CooldownDuration(AttackKind kind) => skillCooldownSeconds!=null && (int)kind<skillCooldownSeconds.Length ? Mathf.Max(.01f,skillCooldownSeconds[(int)kind]) : 3f;
    public float CooldownRemaining(AttackKind kind) => Mathf.Max(0,readyAt[(int)kind]-Time.time);
    public float CooldownFraction(AttackKind kind) => Mathf.Clamp01(CooldownRemaining(kind)/CooldownDuration(kind));
    private void OnEnable()
    {
        properties = new MaterialPropertyBlock();
        for(int i=0;i<2;i++){var hand=Hand(i);if(hand!=null)previous[i]=hand.localPosition;measuring[i]=fast[i]=false;}
        if(combat!=null)combat.AttackFinished+=FinishAttack;
        feedbackIndex=-1;
    }
    private Transform Hand(int i) => i==0 ? leftController : transform;
    public static bool Classify(Vector3 movement, Vector3 forward, Vector3 up, bool left, float tolerance, out AttackKind kind)
    {
        bool upper=Vector3.Angle(movement,up)<Vector3.Angle(movement,forward);
        kind=upper ? (left?AttackKind.LeftUppercut:AttackKind.RightUppercut) : (left?AttackKind.LeftJab:AttackKind.RightStraight);
        return movement.sqrMagnitude>0.000001f && Vector3.Angle(movement,upper?up:forward)<=tolerance;
    }
    public bool TryAttack(AttackKind kind) => TryAttack(kind,true,head!=null?head.forward:Vector3.forward);
    public bool TryAttack(AttackKind kind, bool useSkill, Vector3 swingDirection)
    {
        if(!isActiveAndEnabled || playerAnimator==null || combat==null || !combat.enabled || playerAnimator.IsInTransition(0) || !playerAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"))return false;
        if(!useSkill && kind!=AttackKind.LeftJab && kind!=AttackKind.RightStraight)return false;
        if(useSkill && CooldownRemaining(kind)>0)return false;
        combat.PrepareAttack(useSkill,swingDirection);
        IsSkillActive=useSkill;
        if(useSkill){readyAt[(int)kind]=Time.time+CooldownDuration(kind);if(actionLock!=null)actionLock.Acquire(this);}
        playerAnimator.CrossFadeInFixedTime(StateName(kind),.06f,0,0);
        Feedback((int)kind,Color.green);
        bool left=kind==AttackKind.LeftJab || kind==AttackKind.LeftUppercut;
        var device=InputDevices.GetDeviceAtXRNode(left?XRNode.LeftHand:XRNode.RightHand);
        if(device.TryGetHapticCapabilities(out var cap)&&cap.supportsImpulse)device.SendHapticImpulse(0,useSkill ? .35f : .15f,.08f);
        return true;
    }
    private void FinishAttack(){IsSkillActive=false;if(actionLock!=null)actionLock.Release(this);}
    private void Feedback(int index,Color color){feedbackIndex=index;feedbackColor=color;feedbackUntil=Time.time+.25f;}
    private void LateUpdate()
    {
        if(Time.deltaTime<=0 || head==null || playerAnimator==null)return;
        bool busy=playerAnimator.IsInTransition(0)||!playerAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle");
        for(int i=0;i<2;i++)
        {
            var hand=Hand(i);if(hand==null)continue;
            Vector3 position=hand.localPosition,delta=position-previous[i];
            bool moving=delta.magnitude/Time.deltaTime>=swingThreshold,rising=moving&&!fast[i];
            Vector3 last=previous[i];previous[i]=position;fast[i]=moving;
            var device=InputDevices.GetDeviceAtXRNode(i==0?XRNode.LeftHand:XRNode.RightHand);
            device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton,out bool pressed);
            if(i==1 && skillButton?.action?.IsPressed()==true)pressed=true;
            if(busy){measuring[i]=false;continue;}
            if(!measuring[i] && rising)
            {
                measuring[i]=true;start[i]=last;started[i]=Time.time;skillGesture[i]=pressed;
                var f=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
                if(f.sqrMagnitude<.01f)f=Vector3.ProjectOnPlane(head.up,Vector3.up).normalized;
                forward[i]=hand.parent!=null?hand.parent.InverseTransformDirection(f):f;
                up[i]=hand.parent!=null?hand.parent.InverseTransformDirection(Vector3.up):Vector3.up;
            }
            if(!measuring[i])continue;
            skillGesture[i]|=pressed;
            if(Time.time-started[i]>swingWindow){measuring[i]=false;continue;}
            var motion=position-start[i];if(motion.magnitude<minimumSwingDistance)continue;
            measuring[i]=false;
            var worldMotion=hand.parent!=null?hand.parent.TransformDirection(motion):motion;
            if(Classify(motion,forward[i],up[i],i==0,directionTolerance,out var kind) && TryAttack(kind,skillGesture[i],worldMotion))busy=true;
            else Feedback((int)kind,Color.red);
        }
        for(int i=0;i<attackGuides.Length;i++)
        {
            var line=attackGuides[i];if(line==null)continue;
            var color=Time.time<feedbackUntil && i==feedbackIndex?feedbackColor:busy||CooldownRemaining((AttackKind)i)>0?Color.gray:new Color(.1f,.65f,1);
            properties.SetColor("_Color",color);properties.SetColor("_BaseColor",color);line.SetPropertyBlock(properties);
        }
    }
    private void OnDisable()
    {
        if(combat!=null)combat.AttackFinished-=FinishAttack;
        FinishAttack();
        if(attackGuides!=null)foreach(var line in attackGuides)if(line!=null)line.SetPropertyBlock(null);
    }
}
