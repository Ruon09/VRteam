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
    [SerializeField] private LineRenderer[] attackGuides;
    [SerializeField, Min(.01f)] private float swingThreshold = 2f;
    [SerializeField, Min(.01f)] private float swingWindow = .18f;
    [SerializeField, Min(.01f)] private float minimumSwingDistance = .18f;
    [SerializeField, Range(0,90)] private float directionTolerance = 40f;
    [SerializeField, Min(0)] private float cooldown = .5f;
    private readonly Vector3[] previous = new Vector3[2], start = new Vector3[2], forward = new Vector3[2], up = new Vector3[2];
    private readonly float[] started = new float[2];
    private readonly bool[] measuring = new bool[2], fast = new bool[2];
    private float nextAttack, feedbackUntil;
    private int feedbackIndex = -1;
    private Color feedbackColor;
    private MaterialPropertyBlock properties;
    public static string StateName(AttackKind kind) => kind.ToString();
    private void OnEnable()
    {
        properties = new MaterialPropertyBlock();
        for(int i=0;i<2;i++) {var hand=Hand(i); if(hand!=null) previous[i]=hand.localPosition; measuring[i]=fast[i]=false;}
        feedbackIndex=-1;
    }
    private Transform Hand(int i) => i==0 ? leftController : transform;
    public static bool Classify(Vector3 movement, Vector3 forward, Vector3 up, bool left, float tolerance, out AttackKind kind)
    {
        bool upper=Vector3.Angle(movement,up)<Vector3.Angle(movement,forward);
        kind=upper ? (left?AttackKind.LeftUppercut:AttackKind.RightUppercut) : (left?AttackKind.LeftJab:AttackKind.RightStraight);
        return movement.sqrMagnitude>0.000001f && Vector3.Angle(movement,upper?up:forward)<=tolerance;
    }
    public bool TryAttack(AttackKind kind)
    {
        if(!isActiveAndEnabled || playerAnimator==null || Time.time<nextAttack || playerAnimator.IsInTransition(0) || !playerAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle")) return false;
        playerAnimator.CrossFadeInFixedTime(StateName(kind),.06f,0,0);
        nextAttack=Time.time+cooldown;
        feedbackIndex=(int)kind; feedbackColor=Color.green; feedbackUntil=Time.time+.25f;
        bool left=kind==AttackKind.LeftJab || kind==AttackKind.LeftUppercut;
        var device=InputDevices.GetDeviceAtXRNode(left?XRNode.LeftHand:XRNode.RightHand);
        if(device.TryGetHapticCapabilities(out var cap)&&cap.supportsImpulse) device.SendHapticImpulse(0,.35f,.08f);
        return true;
    }
    private void LateUpdate()
    {
        if(Time.deltaTime<=0 || head==null) return;
        for(int i=0;i<2;i++)
        {
            var hand=Hand(i); if(hand==null) continue;
            Vector3 position=hand.localPosition, delta=position-previous[i];
            bool moving=delta.magnitude/Time.deltaTime>=swingThreshold, rising=moving&&!fast[i];
            Vector3 last=previous[i]; previous[i]=position; fast[i]=moving;
            var device=InputDevices.GetDeviceAtXRNode(i==0?XRNode.LeftHand:XRNode.RightHand);
            device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton,out bool pressed);
            if(i==1 && skillButton?.action?.IsPressed()==true) pressed=true;
            if(!pressed || Time.time<nextAttack || playerAnimator.IsInTransition(0) || !playerAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle")) {measuring[i]=false;continue;}
            if(!measuring[i] && rising)
            {
                measuring[i]=true; start[i]=last; started[i]=Time.time;
                var f=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
                if(f.sqrMagnitude<.01f) f=Vector3.ProjectOnPlane(head.up,Vector3.up).normalized;
                forward[i]=hand.parent!=null?hand.parent.InverseTransformDirection(f):f;
                up[i]=hand.parent!=null?hand.parent.InverseTransformDirection(Vector3.up):Vector3.up;
            }
            if(!measuring[i])continue;
            if(Time.time-started[i]>swingWindow){measuring[i]=false;continue;}
            var motion=position-start[i]; if(motion.magnitude<minimumSwingDistance)continue;
            measuring[i]=false;
            if(Classify(motion,forward[i],up[i],i==0,directionTolerance,out var kind)) TryAttack(kind);
            else {feedbackIndex=(int)kind;feedbackColor=Color.red;feedbackUntil=Time.time+.25f;}
        }
        bool busy=Time.time<nextAttack || playerAnimator.IsInTransition(0) || !playerAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle");
        for(int i=0;i<attackGuides.Length;i++)
        {
            var line=attackGuides[i]; if(line==null)continue;
            var color=Time.time<feedbackUntil && i==feedbackIndex ? feedbackColor : busy?Color.gray:new Color(.1f,.65f,1);
            properties.SetColor("_Color",color);properties.SetColor("_BaseColor",color);line.SetPropertyBlock(properties);
        }
    }
    private void OnDisable(){if(attackGuides!=null)foreach(var line in attackGuides)if(line!=null)line.SetPropertyBlock(null);}
}

