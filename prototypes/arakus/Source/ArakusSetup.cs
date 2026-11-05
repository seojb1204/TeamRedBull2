using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;
using System.Collections.Generic;
public static class ArakusSetup {
 [MenuItem("Arakus/Build and verify preview")]
 public static void BuildAndVerify(){
  var manifest=JsonUtility.FromJson<ArakusManifest>(File.ReadAllText("Assets/Arakus/clip_manifest.json"));
  AssetDatabase.Refresh();Directory.CreateDirectory("Assets/Arakus/Animation");Directory.CreateDirectory("Review");
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var camera=new GameObject("Pixel review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=4.8f;camera.transform.position=new Vector3(0,1.8f,-10);camera.backgroundColor=new Color(.08f,.09f,.12f);camera.clearFlags=CameraClearFlags.SolidColor;
  var actor=new GameObject("Arakus art preview");var sr=actor.AddComponent<SpriteRenderer>();sr.sharedMaterial=new Material(Shader.Find("Sprites/Default"));
  var control=actor.AddComponent<ArakusPreview>();control.target=sr;control.clips=manifest.clips;
  var frames=new List<Sprite>();var offsets=new List<int>();
  string controllerPath="Assets/Arakus/Animation/Arakus.controller";
  var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
  var animator=actor.AddComponent<Animator>();animator.runtimeAnimatorController=controller;
  foreach(var d in manifest.clips){
   offsets.Add(frames.Count);var keys=new List<ObjectReferenceKeyframe>();float t=0;
   for(int i=0;i<d.count;i++){
    string path="Assets/Arakus/Frames/"+d.id+"_"+i.ToString("00")+".png";
    var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;imp.filterMode=FilterMode.Point;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.spritePixelsPerUnit=40;var textureSettings=new TextureImporterSettings();imp.ReadTextureSettings(textureSettings);textureSettings.spriteAlignment=(int)SpriteAlignment.Custom;textureSettings.spritePivot=new Vector2(.5f,d.size==192?32f/192f:64f/384f);imp.SetTextureSettings(textureSettings);imp.SaveAndReimport();
    var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(sprite==null)throw new System.Exception("Missing sprite "+path);frames.Add(sprite);keys.Add(new ObjectReferenceKeyframe{time=t,value=sprite});t+=d.ms[i]/1000f;
   }
   var last=keys[keys.Count-1];keys.Add(new ObjectReferenceKeyframe{time=t-.001f,value=last.value});
   string clipPath="Assets/Arakus/Animation/"+d.id+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,clipPath);}clip.frameRate=1000;
   AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"},keys.ToArray());var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=d.loop;settings.stopTime=t;AnimationUtility.SetAnimationClipSettings(clip,settings);
   var sm=controller.layers[0].stateMachine;var state=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name==d.id);if(state==null)state=sm.AddState(d.id);state.motion=clip;
   string sheet="Assets/Arakus/"+d.id+".png";var si=(TextureImporter)AssetImporter.GetAtPath(sheet);si.textureType=TextureImporterType.Sprite;si.spriteImportMode=SpriteImportMode.Multiple;si.filterMode=FilterMode.Point;si.mipmapEnabled=false;si.textureCompression=TextureImporterCompression.Uncompressed;si.maxTextureSize=8192;si.spritePixelsPerUnit=40;
   #pragma warning disable 0618
   si.spritesheet=Enumerable.Range(0,d.count).Select(i=>new SpriteMetaData{name=d.id+"_"+i.ToString("00"),rect=new Rect(i*d.size,0,d.size,d.size),alignment=(int)SpriteAlignment.Custom,pivot=new Vector2(.5f,d.size==192?32f/192f:64f/384f)}).ToArray();
   #pragma warning restore 0618
   si.SaveAndReimport();
   var sheetSprites=AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().ToArray();if(sheetSprites.Length!=d.count)throw new System.Exception("Sheet slice mismatch "+d.id);
   EditorUtility.SetDirty(clip);EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();animator.Rebind();int sample=System.Math.Min(d.count/2,d.count-1);clip.SampleAnimation(actor,d.ms.Take(sample).Sum()/1000f+.001f);if(sr.sprite!=frames[offsets.Last()+sample])throw new System.Exception("Animation sampling mismatch "+d.id+" expected="+frames[offsets.Last()+sample].name+" actual="+(sr.sprite==null?"null":sr.sprite.name)+" keys="+string.Join(",",AnimationUtility.GetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"}).Select(k=>k.time.ToString())));if(System.Math.Abs(clip.length-t)>.002f)throw new System.Exception("Animation length mismatch "+d.id);Capture(camera,"Review/Unity_"+d.id+".png");if(d.id=="Idle"){sr.flipX=true;Capture(camera,"Review/Unity_Idle_Left.png");sr.flipX=false;}if(d.id=="Death"){clip.SampleAnimation(actor,t-.002f);if(sr.sprite!=frames[offsets.Last()+d.count-1])throw new System.Exception("Death final pose mismatch");Capture(camera,"Review/Unity_Death_Final.png");}
   Debug.Log("ARAKUS_CLIP_VERIFIED "+d.id+" "+d.count+" "+t);
  }
  animator.enabled=false;control.frames=frames.ToArray();control.offsets=offsets.ToArray();sr.sprite=frames[0];
  PrefabUtility.SaveAsPrefabAsset(actor,"Assets/Arakus/ArakusArtPreview.prefab");EditorSceneManager.SaveScene(scene,"Assets/Arakus/ArakusPreview.unity");AssetDatabase.SaveAssets();
  Debug.Log("ARAKUS_VERIFIED clips="+manifest.clips.Length+" frames="+frames.Count+" | standalone art preview, no combat integration");
 }
 static void Capture(Camera camera,string path){var rt=new RenderTexture(768,768,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var t=new Texture2D(768,768,TextureFormat.RGBA32,false);t.ReadPixels(new Rect(0,0,768,768),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(t);Object.DestroyImmediate(rt);}
}
