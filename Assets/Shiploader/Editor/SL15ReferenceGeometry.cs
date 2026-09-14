using System.Collections.Generic;
using UnityEngine;

namespace ZCJ.Shiploader.Editor
{
    public static partial class SL15ReferenceModel
    {
        static Mesh BevelBox(Vector3 size)
        {
            Vector3 h=size*.5f;
            float b=Mathf.Min(.045f,Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.22f);
            var v=new List<Vector3>();var tr=new List<int>();
            void Face(Vector3[] points,Vector3 normal) {
                int k=v.Count;
                if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),normal)<0) System.Array.Reverse(points);
                v.AddRange(points);for(int i=1;i<points.Length-1;i++)tr.AddRange(new[]{k,k+i,k+i+1});
            }
            for(int axis=0;axis<3;axis++) foreach(float sign in new[]{-1f,1f}) {
                int u=(axis+1)%3,w=(axis+2)%3;var q=new Vector3[8];
                float hu=h[u],hw=h[w];
                Vector2[] xy={new(-hu+b,-hw+b),new(hu-b,-hw+b),new(hu-b,hw-b),new(-hu+b,hw-b)};
                var f=new Vector3[4];for(int j=0;j<4;j++){f[j][axis]=sign*h[axis];f[j][u]=xy[j].x;f[j][w]=xy[j].y;}
                Vector3 n=Vector3.zero;n[axis]=sign;Face(f,n);
            }
            for(int along=0;along<3;along++) foreach(float su in new[]{-1f,1f}) foreach(float sw in new[]{-1f,1f}) {
                int u=(along+1)%3,w=(along+2)%3;var f=new Vector3[4];
                for(int j=0;j<4;j++) {
                    f[j][along]=(j<2?-1:1)*(h[along]-b);
                    bool first=j==0||j==3;f[j][u]=su*(h[u]-(first?0:b));f[j][w]=sw*(h[w]-(first?b:0));
                }
                Vector3 n=Vector3.zero;n[u]=su;n[w]=sw;Face(f,n);
            }
            foreach(float x in new[]{-1f,1f})foreach(float y in new[]{-1f,1f})foreach(float z in new[]{-1f,1f}) {
                Vector3 n=new Vector3(x,y,z),c=Vector3.Scale(h,n);
                Face(new[]{c-new Vector3(0,y*b,z*b),c-new Vector3(x*b,0,z*b),c-new Vector3(x*b,y*b,0)},n);
            }
            var mesh=new Mesh{name="BevelledPlate"};mesh.SetVertices(v);mesh.SetTriangles(tr,0);
            mesh.SetUVs(0,v.ConvertAll(p=>new Vector2(p.x+p.z,p.y+p.z)));mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        static Transform MeshObject(Transform p,string name,Mesh mesh,Material mat)
        {
            var g=new GameObject(name);g.transform.SetParent(p,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;return g.transform;
        }

        static void Ring(Transform p,string name,float y,float outer,float inner,float height,Material mat)
        {
            var v=new List<Vector3>();var t=new List<int>();int n=64;
            for(int j=0;j<=n;j++) {float a=j*Mathf.PI*2/n;foreach(var pair in new[]{new Vector2(outer,-height/2),new Vector2(outer,height/2),new Vector2(inner,height/2),new Vector2(inner,-height/2)})v.Add(new Vector3(Mathf.Cos(a)*pair.x,y+pair.y,Mathf.Sin(a)*pair.x));}
            for(int j=0;j<n;j++)for(int k=0;k<4;k++){int a=j*4+k,b=j*4+(k+1)%4;t.AddRange(new[]{a,b,b+4,a,b+4,a+4});}
            var m=new Mesh{name=name};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();MeshObject(p,name,m,mat);
        }

        static void Frustum(Transform p,string name,float top,float bottom,float topRadius,float bottomRadius,Material mat)
        {
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=48;i++){float a=i*Mathf.PI*2/48;v.Add(new Vector3(Mathf.Cos(a)*topRadius,top,Mathf.Sin(a)*topRadius));v.Add(new Vector3(Mathf.Cos(a)*bottomRadius,bottom,Mathf.Sin(a)*bottomRadius));}
            for(int i=0;i<48;i++){int k=i*2;t.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}
            var m=new Mesh{name=name};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();MeshObject(p,name,m,mat);
        }

        static void BuildPortalDiaphragms(Transform p,float y)
        {
            foreach(float z in new[]{-6.7f,6.7f}) {
                BoxBeam(p,"DiaphragmDiagonal",new Vector3(-6,y-.7f,z),new Vector3(-2.8f,y-3.4f,z),.65f,.65f,red);
                BoxBeam(p,"DiaphragmDiagonal",new Vector3(6,y-.7f,z),new Vector3(2.8f,y-3.4f,z),.65f,.65f,red);
                Box(p,"DiaphragmBottom",new Vector3(0,y-3.4f,z),new Vector3(6.3f,.6f,.7f),red);
                BoxBeam(p,"DiaphragmWeb",new Vector3(-2.8f,y-3.4f,z),new Vector3(0,y-.5f,z),.28f,.32f,edge);
                BoxBeam(p,"DiaphragmWeb",new Vector3(2.8f,y-3.4f,z),new Vector3(0,y-.5f,z),.28f,.32f,edge);
            }
        }

        static void BuildNestedTelescope(Transform p,ShiploaderRigController rig)
        {
            var follow=p.gameObject.AddComponent<ShiploaderTelescopicVisual>();follow.rig=rig;
            follow.sectionLength=rig.Config.telescopicOverlap+rig.Config.boomExtensionTravel/3;
            follow.sections=new Transform[3];Transform parent=p;
            for(int i=0;i<3;i++) {
                var section=new GameObject("RigidTelescopeSection_"+(i+1)).transform;section.SetParent(parent,false);
                follow.sections[i]=section;
                BuildBoom(section,follow.sectionLength,2.9f-i*.32f,1.65f-i*.27f,6,"RigidTelescope"+i,true);
                parent=section;
            }
            follow.Refresh();
        }

        static void BuildRotaryOutlet(Transform p,float endY)
        {
            // Hollow curved shell, ending on the retained discharge centerline.
            var v=new List<Vector3>();var t=new List<int>();int segments=12,sides=40;
            for(int i=0;i<=segments;i++) {
                float u=i/(float)segments;float y=endY+.8f*(1-u);
                float z=-.45f*Mathf.Sin(u*Mathf.PI);float radius=Mathf.Lerp(.61f,.42f,u);
                for(int j=0;j<=sides;j++){float a=j*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(a)*radius,y,z+Mathf.Sin(a)*radius));}
            }
            for(int i=0;i<segments;i++)for(int j=0;j<sides;j++){int k=i*(sides+1)+j;t.AddRange(new[]{k,k+1,k+sides+1,k+1,k+sides+2,k+sides+1});}
            var m=new Mesh{name="CurvedDischargeShell"};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();MeshObject(p,m.name,m,steel);
            Batch(p,"RotaryOutlet");
        }
    }
}
