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
    [SerializeField, Min(.01f)] private float swingThreshold = .9f;
    [SerializeField, Min(.01f)] private float swingWindow = .35f;
    [SerializeField, Min(.01f)] private float minimumSwingDistance = .09f;
    [SerializeField, Range(0,90)] private float directionTolerance = 55f;
    [SerializeField] private float[] skillCooldownSeconds = {3f,3f,3f,3f};
    private readonly float[] readyAt = new float[4];
    [SerializeField, Range(0f,1f)] private float triggerThreshold = .2f;
    private readonly SwingGestureRecognizer[] gestures = {new SwingGestureRecognizer(),new SwingGestureRecognizer()};
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
        for(int i=0;i<2;i++){var hand=Hand(i);if(hand!=null)gestures[i].Reset(hand.localPosition);}
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
            var device=InputDevices.GetDeviceAtXRNode(i==0?XRNode.LeftHand:XRNode.RightHand);
            device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton,out bool pressed);
            if(device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger,out float pressure))pressed|=pressure>=triggerThreshold;
            if(i==1 && skillButton?.action?.IsPressed()==true)pressed=true;
            var f=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
            if(f.sqrMagnitude<.01f)f=Vector3.ProjectOnPlane(head.up,Vector3.up).normalized;
            var forward=hand.parent!=null?hand.parent.InverseTransformDirection(f):f;
            var up=hand.parent!=null?hand.parent.InverseTransformDirection(Vector3.up):Vector3.up;
            if(gestures[i].Sample(hand.localPosition,Time.time,Time.deltaTime,pressed,!busy,
                forward,up,i==0,swingThreshold,minimumSwingDistance,swingWindow,directionTolerance,
                out var kind,out bool useSkill,out var motion))
            {
                var worldMotion=hand.parent!=null?hand.parent.TransformDirection(motion):motion;
                if(TryAttack(kind,useSkill,worldMotion))busy=true;
                else Feedback((int)kind,Color.red);
            }
            else if(gestures[i].Rejected)Feedback((int)kind,Color.red);
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
