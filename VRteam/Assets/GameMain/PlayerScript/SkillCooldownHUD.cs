using UnityEngine;
using UnityEngine.UI;

public class SkillCooldownHUD : MonoBehaviour
{
    [SerializeField] private VRSwingSkill skills;
    [SerializeField] private Image[] masks = new Image[4];
    [SerializeField] private Text[] labels = new Text[4];
    private static readonly string[] Names={"L JAB","R STRAIGHT","L UPPER","R UPPER"};
    private void Update()
    {
        if(skills==null)return;
        for(int i=0;i<4;i++)
        {
            var kind=(VRSwingSkill.AttackKind)i;
            float remaining=skills.CooldownRemaining(kind);
            if(i<masks.Length && masks[i]!=null)masks[i].fillAmount=skills.CooldownFraction(kind);
            if(i<labels.Length && labels[i]!=null)labels[i].text=Names[i]+"\n"+(remaining>0?remaining.ToString("F1")+"s":"READY");
        }
    }
}
