using UnityEngine;
using System;
[Serializable] public class ArakusClip { public string id,label,group,end; public int count,size; public int[] ms; public bool loop; public string[] shield; }
[Serializable] public class ArakusManifest { public ArakusClip[] clips; }
public class ArakusPreview:MonoBehaviour {
 public SpriteRenderer target; public ArakusClip[] clips; public Sprite[] frames; public int[] offsets;
 int selected,frame;float clock;bool pause,flip;Vector2 scroll;
 void Update(){if(!pause){clock+=Time.deltaTime;while(clock>=clips[selected].ms[frame]/1000f){clock-=clips[selected].ms[frame]/1000f;frame++;if(frame>=clips[selected].count){if(clips[selected].loop)frame=0;else{frame=clips[selected].count-1;pause=true;break;}}}Draw();}}
 void Draw(){target.sprite=frames[offsets[selected]+frame];target.flipX=flip;}
 void OnGUI(){GUILayout.BeginArea(new Rect(12,12,330,Screen.height-24));GUILayout.Label("ARAKUS | sprite review");scroll=GUILayout.BeginScrollView(scroll);for(int i=0;i<clips.Length;i++){if(GUILayout.Button(clips[i].id)){selected=i;frame=0;clock=0;pause=false;Draw();}}GUILayout.EndScrollView();GUILayout.EndArea();GUILayout.BeginArea(new Rect(Screen.width-250,12,240,220));GUILayout.Label(clips[selected].id+" | "+(frame+1)+" / "+clips[selected].count);if(GUILayout.Button(pause?"Play":"Pause"))pause=!pause;if(GUILayout.Button("Step")){pause=true;frame=(frame+1)%clips[selected].count;Draw();}if(GUILayout.Button("Flip")){flip=!flip;Draw();}GUILayout.EndArea();}
}
