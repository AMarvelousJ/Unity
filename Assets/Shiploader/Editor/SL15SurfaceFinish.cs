using UnityEngine;
using UnityEditor;

namespace ZCJ.Shiploader.Editor
{
    public static partial class SL15ReferenceModel
    {
        static void BuildCoalStream(Transform parent,ShiploaderEffectsController effects,Transform outlet)
        {
            var v=new System.Collections.Generic.List<Vector3>();var tr=new System.Collections.Generic.List<int>();int sides=18,rings=12;
            for(int r=0;r<=rings;r++)for(int i=0;i<=sides;i++) {
                float a=i*Mathf.PI*2/sides,u=r/(float)rings;
                float radius=(.45f+u*.08f)*(1+.13f*Mathf.Sin(i*2.3f+r*1.7f));
                v.Add(new Vector3(Mathf.Cos(a)*radius,.5f-u,Mathf.Sin(a)*radius));
            }
            for(int r=0;r<rings;r++)for(int i=0;i<sides;i++){int a=r*(sides+1)+i;tr.AddRange(new[]{a,a+1,a+sides+1,a+1,a+sides+2,a+sides+1});}
            var mesh=new Mesh{name="DenseCoalStream"};mesh.SetVertices(v);mesh.SetUVs(0,v.ConvertAll(p=>new Vector2(p.x*6,p.y*10)));mesh.SetTriangles(tr,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var material=Mat("CoalStream",new Color32(20,22,23,255),.02f,.14f);material.SetTexture("_BaseMap",SurfaceTexture("CoalGrain",true));
            var t=MeshObject(parent,"DenseCoalStream",PersistMesh(mesh,"DenseCoalStream"),material);
            var follow=t.gameObject.AddComponent<ShiploaderCoalStream>();follow.effects=effects;follow.outlet=outlet;follow.Refresh();
            var particles=outlet.parent.GetComponentInChildren<ParticleSystem>();
            if(particles!=null){var main=particles.main;main.startSize=new ParticleSystem.MinMaxCurve(.025f,.065f);main.maxParticles=1600;var emission=particles.emission;emission.rateOverTime=400;}
        }
        static void ApplySurfaceFinish()
        {
            var texture=SurfaceTexture("PaintGrain",false);
            foreach(var material in new[]{red,edge,blue,deck}) {
                material.SetTexture("_BaseMap",texture);material.SetTextureScale("_BaseMap",new Vector2(.7f,.7f));EditorUtility.SetDirty(material);
            }
        }
        static Texture2D SurfaceTexture(string name,bool coal)
        {
            string path=Folder+"/"+name+".asset";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(texture!=null)return texture;
            texture=new Texture2D(256,256,TextureFormat.RGBA32,true){name=name,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            var pixels=new Color[256*256];var random=new System.Random(1357);
            for(int y=0;y<256;y++)for(int x=0;x<256;x++) {
                float coarse=Mathf.PerlinNoise(x*.07f,y*.07f),grain=(float)random.NextDouble();
                float shade=coal?.45f+.4f*coarse+.3f*grain:.89f+.07f*coarse+.04f*grain;
                pixels[y*256+x]=new Color(shade,shade,shade,1);
            }
            texture.SetPixels(pixels);texture.Apply();AssetDatabase.CreateAsset(texture,path);return texture;
        }
    }
}
