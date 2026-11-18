using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

public class BattleResultUI : MonoBehaviour
{
    [SerializeField] private Text title;
    [SerializeField] private Transform panel;
    private bool previousPrimary,previousSecondary,loading;
    private float readyAt;
    private void Start()
    {
        if(title!=null){title.text=BattleFlow.PlayerWon?"VICTORY":"DEFEAT";title.color=BattleFlow.PlayerWon?Color.green:new Color(1,.3f,.3f);}
        readyAt=Time.unscaledTime+.5f;
    }
    private void LateUpdate()
    {
        var camera=Camera.main;
        if(camera!=null && panel!=null)
        {
            panel.SetPositionAndRotation(camera.transform.TransformPoint(new Vector3(0,0,1.2f)),camera.transform.rotation);
        }
    }
    private void Update()
    {
        var device=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        device.TryGetFeatureValue(CommonUsages.primaryButton,out bool primary);
        device.TryGetFeatureValue(CommonUsages.secondaryButton,out bool secondary);
        if(Time.unscaledTime>=readyAt)
        {
            if(primary&&!previousPrimary)Replay();
            if(secondary&&!previousSecondary)Quit();
        }
        previousPrimary=primary;previousSecondary=secondary;
    }
    public void Replay()
    {
        if(loading)return;
        if(!Application.CanStreamedLevelBeLoaded(BattleFlow.ReplayScene)){Debug.LogError("Replay scene missing: "+BattleFlow.ReplayScene);return;}
        loading=true;SceneManager.LoadSceneAsync(BattleFlow.ReplayScene,LoadSceneMode.Single);
    }
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }
}
