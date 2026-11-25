using UnityEngine;

public class HitImpactEffect : MonoBehaviour
{
    [System.Serializable]
    private class Layer
    {
        public SpriteRenderer renderer;
        public float duration=.2f;
        public float startDiameter=.12f;
        public float endDiameter=.4f;
        public float opacity=1;
    }
    [SerializeField] private Layer[] layers=new Layer[0];
    private float startedAt;
    private float size=1;
    private Camera viewer;
    public void Initialize(Camera camera,float sizeMultiplier)
    {
        viewer=camera;size=sizeMultiplier;startedAt=Time.time;UpdateAppearance(0);
    }
    private void OnEnable(){startedAt=Time.time;viewer=Camera.main;UpdateAppearance(0);}
    private void Update()
    {
        float age=Time.time-startedAt;UpdateAppearance(age);
        float lifetime=0;foreach(var layer in layers)lifetime=Mathf.Max(lifetime,layer.duration);
        if(age>=lifetime)Destroy(gameObject);
    }
    private void LateUpdate()
    {
        if(viewer!=null)transform.rotation=Quaternion.LookRotation(transform.position-viewer.transform.position,viewer.transform.up);
    }
    private void UpdateAppearance(float age)
    {
        foreach(var layer in layers)
        {
            if(layer.renderer==null || layer.renderer.sprite==null)continue;
            float t=Mathf.Clamp01(age/Mathf.Max(.01f,layer.duration));
            float growth=1-Mathf.Pow(1-t,2);
            float diameter=Mathf.Lerp(layer.startDiameter,layer.endDiameter,growth)*size;
            float width=Mathf.Max(.001f,layer.renderer.sprite.bounds.size.x);
            layer.renderer.transform.localScale=Vector3.one*(diameter/width);
            layer.renderer.color=new Color(1,1,1,layer.opacity*(1-t));
        }
    }
}
