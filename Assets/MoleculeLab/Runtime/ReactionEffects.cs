using System.Collections;
using UnityEngine;

namespace MoleculeLab
{
    /// <summary>
    /// Small, Quest-friendly particle effects and procedural sounds for the reaction station: a flame on the spirit lamp,
    /// drops from the acid dropper, powder from the catalyst, and one effect per reaction type (fire and steam, sparkles,
    /// bubbles and a colour change). Uses the UI shader (always in the build) with a soft round texture, so nothing can render pink.
    /// </summary>
    public static class ReactionEffects
    {
        static Material particle;
        static Texture2D soft;
        public static Material ParticleMaterial
        {
            get
            {
                if(particle!=null) return particle;
                soft=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Soft dot",wrapMode=TextureWrapMode.Clamp};
                for(var y=0;y<32;y++) for(var x=0;x<32;x++)
                {
                    var d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(16,16))/16f;
                    soft.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(1-d*d)));
                }
                soft.Apply();
                particle=new Material(Canvas.GetDefaultCanvasMaterial()){name="Effect particles",mainTexture=soft};
                return particle;
            }
        }

        /// <summary>A particle system with sensible defaults; emission is off until configured.</summary>
        public static ParticleSystem Create(string name,Transform parent,Vector3 localPosition,float lifetime,float speed,float size,Gradient colour,int maxParticles,float gravity=0)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.localPosition=localPosition; go.transform.localRotation=Quaternion.Euler(-90,0,0); // emit upwards
            var ps=go.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main; main.playOnAwake=false; main.loop=true; main.duration=1; main.startLifetime=lifetime; main.startSpeed=speed; main.startSize=size;
            main.simulationSpace=ParticleSystemSimulationSpace.World; main.maxParticles=maxParticles; main.gravityModifier=gravity; main.scalingMode=ParticleSystemScalingMode.Shape;
            var emission=ps.emission; emission.rateOverTime=0;
            var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Cone; shape.angle=12; shape.radius=.004f;
            var col=ps.colorOverLifetime; col.enabled=true; col.color=new ParticleSystem.MinMaxGradient(colour);
            var sol=ps.sizeOverLifetime; sol.enabled=true; sol.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,.2f));
            var renderer=go.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=ParticleMaterial; renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
            ps.Play();
            return ps;
        }
        public static Gradient Fade(Color a,Color b,float startAlpha=1)
        {
            var g=new Gradient();
            g.SetKeys(new[]{new GradientColorKey(a,0),new GradientColorKey(b,1)},new[]{new GradientAlphaKey(startAlpha,0),new GradientAlphaKey(startAlpha*.8f,.5f),new GradientAlphaKey(0,1)});
            return g;
        }
        public static void Rate(ParticleSystem ps,float perSecond) { if(ps==null) return; var e=ps.emission; e.rateOverTime=perSecond; }

        // ---------- sounds (generated once; no audio files needed) ----------
        static AudioClip whoosh, fizz, bubbling, chime, drip;
        static AudioClip Make(string name,float seconds,System.Func<float,float,System.Random,float> sample)
        {
            const int rate=24000; var n=(int)(seconds*rate); var data=new float[n]; var random=new System.Random(name.GetHashCode());
            for(var i=0;i<n;i++) data[i]=Mathf.Clamp(sample(i/(float)rate,i/(float)n,random),-1,1);
            var clip=AudioClip.Create(name,n,1,rate,false); clip.SetData(data,0); return clip;
        }
        public static AudioClip Whoosh => whoosh!=null?whoosh:whoosh=MakeWhoosh();
        static AudioClip MakeWhoosh() { float low=0; return Make("Whoosh",.9f,(t,u,r)=>{ low+=((float)r.NextDouble()*2-1-low)*.08f; return low*3.2f*Mathf.Sin(Mathf.PI*Mathf.Pow(u,.6f))*(1-u*.4f); }); }
        public static AudioClip Fizz => fizz!=null?fizz:fizz=Make("Fizz",1.4f,(t,u,r)=>((float)r.NextDouble()*2-1)*(r.NextDouble()<.35?.5f:.08f)*Mathf.Sin(Mathf.PI*u)*.6f);
        public static AudioClip Bubbling => bubbling!=null?bubbling:bubbling=MakeBubbles();
        static AudioClip MakeBubbles()
        {
            var starts=new float[22]; var pitch=new float[22]; var r0=new System.Random(5);
            for(var i=0;i<starts.Length;i++) { starts[i]=(float)r0.NextDouble()*1.6f; pitch[i]=260+(float)r0.NextDouble()*420; }
            return Make("Bubbling",1.8f,(t,u,r)=>{ float s=0; for(var i=0;i<starts.Length;i++){ var d=t-starts[i]; if(d>0 && d<.09f) s+=Mathf.Sin(2*Mathf.PI*pitch[i]*(1+d*4)*d)*(1-d/.09f)*.35f; } return s; });
        }
        public static AudioClip Chime => chime!=null?chime:chime=Make("Products ready",.7f,(t,u,r)=>(Mathf.Sin(2*Mathf.PI*1046*t)+.5f*Mathf.Sin(2*Mathf.PI*1568*t))*.18f*Mathf.Exp(-t*5));
        public static AudioClip Drip => drip!=null?drip:drip=Make("Drip",.12f,(t,u,r)=>Mathf.Sin(2*Mathf.PI*(900-3000*t)*t)*.25f*(1-u));
    }
}
