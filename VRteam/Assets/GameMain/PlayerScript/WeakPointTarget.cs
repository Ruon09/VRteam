using UnityEngine;

// Enemy integration: disable previewCycle and call SetWindowOpen during the telegraph.
public class WeakPointTarget : MonoBehaviour
{
    [SerializeField] private LineRenderer line;
    [SerializeField] private TextMesh label;
    [SerializeField] private Vector3 lineStart = new Vector3(0,-.2f,-.51f);
    [SerializeField] private Vector3 lineEnd = new Vector3(0,.2f,-.51f);
    [SerializeField] private bool previewCycle = true;
    [SerializeField, Min(.1f)] private float openSeconds = 3f;
    [SerializeField, Min(.1f)] private float closedSeconds = 2f;
    [SerializeField, Range(1,90)] private float tolerance = 40f;
    [SerializeField, Min(1)] private float maximumMultiplier = 2f;
    private bool windowOpen;
    private float cycleStarted,hitUntil,lastMultiplier=1;
    private Camera viewer;
    private PracticeTarget practice;
    public bool WindowOpen => windowOpen;
    private void OnEnable(){viewer=Camera.main;practice=GetComponent<PracticeTarget>();cycleStarted=Time.time;windowOpen=false;Refresh();}
    public void SetWindowOpen(bool open){previewCycle=false;windowOpen=open;Refresh();}
    public static float DirectionAccuracy(Vector3 movement,Vector3 direction,float tolerance)
    {
        if(movement.sqrMagnitude<.000001f || direction.sqrMagnitude<.000001f)return 0;
        return Mathf.Clamp01(1-Vector3.Angle(movement,direction)/Mathf.Max(1,tolerance));
    }
    public bool IsVisible(Camera camera)
    {
        if(!isActiveAndEnabled || !windowOpen || camera==null || practice!=null && practice.CurrentHealth<=0)return false;
        var point=camera.WorldToViewportPoint(transform.TransformPoint((lineStart+lineEnd)*.5f));
        return point.z>camera.nearClipPlane && point.z<camera.farClipPlane && point.x>=0 && point.x<=1 && point.y>=0 && point.y<=1;
    }
    public float Multiplier(Vector3 movement,Camera camera)
    {
        if(!IsVisible(camera))return 1;
        float accuracy=DirectionAccuracy(movement,transform.TransformVector(lineEnd-lineStart),tolerance);
        return Mathf.Lerp(1,maximumMultiplier,accuracy);
    }
    public void ShowHit(float multiplier){lastMultiplier=multiplier;hitUntil=Time.time+.45f;}
    private void Update()
    {
        if(previewCycle)windowOpen=(Time.time-cycleStarted)%(openSeconds+closedSeconds)<openSeconds;
        Refresh();
    }
    private void Refresh()
    {
        if(viewer==null)viewer=Camera.main;
        bool visible=IsVisible(viewer);
        if(line!=null){line.enabled=visible;line.useWorldSpace=false;line.positionCount=2;line.SetPosition(0,lineStart);line.SetPosition(1,lineEnd);line.startColor=line.endColor=Time.time<hitUntil?Color.green:Color.yellow;}
        if(label!=null)
        {
            label.gameObject.SetActive(visible);
            label.text=Time.time<hitUntil?"WEAK POINT x"+lastMultiplier.ToString("F2"):"WEAK POINT: UP";
            if(viewer!=null)label.transform.rotation=viewer.transform.rotation;
        }
    }
    private void OnDisable(){if(line!=null)line.enabled=false;if(label!=null)label.gameObject.SetActive(false);}
}
