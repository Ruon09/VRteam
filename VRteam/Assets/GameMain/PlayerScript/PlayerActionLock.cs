using System.Collections.Generic;
using UnityEngine;

// HMD/hand tracking stays enabled; only explicit locomotion providers are suspended.
public class PlayerActionLock : MonoBehaviour
{
    [SerializeField] private Behaviour[] movementProviders = new Behaviour[0];
    private readonly HashSet<object> owners = new HashSet<object>();
    private bool[] previous;
    public bool IsLocked => owners.Count > 0;
    public void Acquire(object owner)
    {
        if(owner == null || !owners.Add(owner) || owners.Count != 1) return;
        previous = new bool[movementProviders.Length];
        for(int i=0;i<movementProviders.Length;i++)
            if(movementProviders[i]!=null){previous[i]=movementProviders[i].enabled;movementProviders[i].enabled=false;}
    }
    public void Release(object owner)
    {
        if(!owners.Remove(owner) || owners.Count>0) return;
        Restore();
    }
    private void Restore()
    {
        if(previous!=null)for(int i=0;i<movementProviders.Length;i++)
            if(movementProviders[i]!=null)movementProviders[i].enabled=previous[i];
        previous=null;
    }
    private void OnDisable(){owners.Clear();Restore();}
}
