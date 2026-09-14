using UnityEngine;

namespace ZCJ.Shiploader
{
    [ExecuteAlways]
    public sealed class ShiploaderCoalStream : MonoBehaviour
    {
        public ShiploaderEffectsController effects;
        public Transform outlet;
        private Renderer stream;
        private HoldCargoVisualController[] holds;
        public void Refresh()
        {
            if(stream==null)stream=GetComponent<Renderer>();
            if(effects==null||outlet==null||!effects.CoalFlowEnabled){stream.enabled=false;return;}
            if(holds==null||holds.Length==0)holds=FindObjectsByType<HoldCargoVisualController>(FindObjectsSortMode.None);
            Vector3 top=outlet.position;float bottom=top.y;
            foreach(var hold in holds) {
                if(hold==null)continue;var coal=hold.transform.Find("CoalSurface");if(coal==null)continue;
                Bounds b=coal.GetComponent<Renderer>().bounds;
                if(top.x>b.min.x&&top.x<b.max.x&&top.z>b.min.z&&top.z<b.max.z)bottom=Mathf.Min(bottom,b.max.y);
            }
            float length=top.y-bottom;
            stream.enabled=length>.05f;
            if(!stream.enabled)return;
            transform.position=top+Vector3.down*length*.5f;transform.rotation=Quaternion.identity;transform.localScale=new Vector3(.44f,length,.44f);
        }
        private void LateUpdate()=>Refresh();
    }
}
