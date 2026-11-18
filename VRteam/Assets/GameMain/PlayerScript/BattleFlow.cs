using UnityEngine;
using UnityEngine.SceneManagement;

public class BattleFlow : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private PracticeTarget practiceOpponent;
    [SerializeField] private VRSwingSkill skill;
    [SerializeField] private PunchCombat combat;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerActionLock actionLock;
    [SerializeField] private GameObject[] battleUI = new GameObject[0];
    [SerializeField] private bool loadResultScene = true;
    [SerializeField] private string resultScene = "Assets/GameMain/BattleResult.unity";
    public bool IsFinished {get;private set;}
    public static bool PlayerWon {get;private set;}
    public static string ReplayScene {get;private set;}="Assets/GameMain/Game.unity";
    public event System.Action<bool> Finished;
    private void OnEnable()
    {
        if(player!=null)player.Defeated+=PlayerDefeated;
        if(practiceOpponent!=null)practiceOpponent.Defeated+=OpponentDefeated;
    }
    private void OnDisable()
    {
        if(player!=null)player.Defeated-=PlayerDefeated;
        if(practiceOpponent!=null)practiceOpponent.Defeated-=OpponentDefeated;
        if(actionLock!=null)actionLock.Release(this);
    }
    private void PlayerDefeated()=>EndBattle(false);
    // The enemy owner can call this public method after integration.
    public void OpponentDefeated()=>EndBattle(true);
    public void EndBattle(bool won)
    {
        if(IsFinished)return;
        IsFinished=true;PlayerWon=won;ReplayScene=gameObject.scene.path;
        if(actionLock!=null)actionLock.Acquire(this);
        if(skill!=null)skill.enabled=false;
        if(combat!=null)combat.enabled=false;
        if(player!=null)player.enabled=false;
        if(animator!=null)animator.Play("Idle",0,0);
        if(practiceOpponent!=null)
        {
            practiceOpponent.enabled=false;
            var weak=practiceOpponent.GetComponent<WeakPointTarget>();if(weak!=null)weak.enabled=false;
        }
        foreach(var item in battleUI)if(item!=null)item.SetActive(false);
        Finished?.Invoke(won);
        if(loadResultScene)
        {
            if(!Application.CanStreamedLevelBeLoaded(resultScene)){Debug.LogError("Battle result scene is missing from the build: "+resultScene);return;}
            SceneManager.LoadSceneAsync(resultScene,LoadSceneMode.Additive);
        }
    }
}
