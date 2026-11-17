using UnityEngine;
using System;
using System.IO;
public class InputCaptureProbe : MonoBehaviour {
    string output; int down,up,held,frames; float started,next;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init(){new GameObject("OwnedInputCaptureProbe").AddComponent<InputCaptureProbe>();}
    void Awake(){
        Application.runInBackground=true; Application.targetFrameRate=30;
        started=Time.realtimeSinceStartup;
        var args=Environment.GetCommandLineArgs();
        for(int i=0;i+1<args.Length;i++)if(args[i]=="-probe-output")output=args[i+1];
    }
    void Update(){
        frames++;
        if(Input.GetKeyDown(KeyCode.J))down++;
        if(Input.GetKeyUp(KeyCode.J))up++;
        if(Input.GetKey(KeyCode.J))held++;
        if(Time.realtimeSinceStartup>next){next=Time.realtimeSinceStartup+.5f;Report();}
        if(Time.realtimeSinceStartup-started>15){Report();Application.Quit();}
    }
    void Report(){
        if(String.IsNullOrEmpty(output))return;
        File.WriteAllText(output,"{\"frames\":"+frames+",\"keydown\":"+down+",\"keyup\":"+up+",\"heldFrames\":"+held+",\"focused\":"+(Application.isFocused?"true":"false")+"}");
    }
    void OnGUI(){
        GUI.color=new Color(220f/255,30f/255,30f/255);GUI.DrawTexture(new Rect(0,0,Screen.width/2,Screen.height),Texture2D.whiteTexture);
        GUI.color=new Color(30f/255,60f/255,220f/255);GUI.DrawTexture(new Rect(Screen.width/2,0,Screen.width/2,Screen.height),Texture2D.whiteTexture);
        GUI.color=Color.white;GUI.Label(new Rect(8,8,300,40),"Unity capture probe J="+down);
    }
}