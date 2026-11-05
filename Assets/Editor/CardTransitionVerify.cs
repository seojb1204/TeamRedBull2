using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ReversalTower;

// Uses an editor-only, unsaved production scene and a fresh isolated save.
public static class CardTransitionVerify
{
    static int checks;
    static TowerGame game;
    static NativeGameUI ui;
    static BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Debug.Log("CARD_INPUT_CHECK_PASS "+name);}
    static object Get(object target,string name)=>target.GetType().GetField(name,Private).GetValue(target);
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
    static void Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
    static void Fixture()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/TowerProduction.unity");
        game=UnityEngine.Object.FindAnyObjectByType<TowerGame>();ui=UnityEngine.Object.FindAnyObjectByType<NativeGameUI>();
        Check(game&&ui,"production scene bindings exist");
        var events=UnityEngine.Object.FindAnyObjectByType<EventSystem>();Call(events,"OnEnable");
        foreach(var caster in UnityEngine.Object.FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None))Call(caster,"OnEnable");
        SaveStore.UseIsolatedRoot(Path.GetFullPath("_verify/card-input-save-"+Guid.NewGuid().ToString("N")));
        Check(SaveStore.IsIsolated,"test save is isolated");
        game.save=new SaveData{gold=1000,unlockedBoss=4,tutorialSeen=true,cardOffer=new[]{"blade","vitality","remedy"}};
        game.mode=ScreenMode.Lobby;game.nativeUI=ui;ui.game=game;
        var sound=game.gameObject.AddComponent<Soundscape>();Set(game,"sound",sound);
        Call(ui,"Awake");ui.Refresh();game.OpenCards();ui.Refresh();Set(ui,"cardsClock",1f);
        foreach(var button in ui.cardButtons)for(int i=0;i<button.onClick.GetPersistentEventCount();i++)button.onClick.SetPersistentListenerState(i,UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
        Check(game.showCards,"three-card choice opened");
    }
    static void Keys(bool help=false,bool escape=false,bool enter=false,bool left=false,bool right=false,bool cards=false)
    {Call(game,"HandleScreenInput",help,escape,enter,left,right,cards);}
    static void VerifyPointerShield()
    {
        var canvas=ui.GetComponent<Canvas>();var camera=Camera.main;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var rt=new RenderTexture(Screen.width,Screen.height,24);camera.targetTexture=rt;
        Canvas.ForceUpdateCanvases();camera.Render();Canvas.ForceUpdateCanvases();
        var button=ui.lobbyPage.GetComponentsInChildren<Button>(true).First(b=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)=="EnterBattle"));
        Check(!button.IsInteractable(),"behind-card battle button is non-interactable");
        var rect=(RectTransform)button.transform;
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center))};
        var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(ui.cardsPage.transform),"real UI raycast over lobby button hits card overlay (hits="+hits.Count+", depth="+button.image.depth+")");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
        Check(game.mode==ScreenMode.Lobby,"pointer over lobby battle button cannot start battle");
        if(Mathf.Abs((float)Get(ui,"outro")-.33f)<.001f){Directory.CreateDirectory("_verify");RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes("_verify/card-outro-locked.png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=null;}
        camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);
    }
    public static void Verify()
    {
        var previousRoot=SaveStore.IsIsolated?SaveStore.Root:null;
        try
        {
            Fixture();Check(ui.BlocksLobbyInput,"open cards block lobby");
            ui.ChooseCard(-1);ui.ChooseCard(3);Check(!game.save.cardChosen&&game.save.gold==1000,"invalid card indices do not purchase");
            EventSystem.current.SetSelectedGameObject(ui.cardButtons[0].gameObject);
            ui.cardButtons[0].onClick.Invoke();
            Check(game.save.gold==920&&game.save.cardChosen&&!game.showCards,"serialized card click commits once immediately");
            Check(EventSystem.current.currentSelectedGameObject==null,"purchase clears stale submit focus");
            Check(ui.BlocksLobbyInput&&!((CanvasGroup)Get(ui,"lobbyGroup")).interactable,"purchase blocks lobby before next refresh");
            ui.ChooseCard(1);Check(game.save.gold==920&&LobbyCards.Count(game.save,"blade")==1,"repeat purchase is ignored");
            foreach(float t in new[]{0f,.33f,.65f,.66f})
            {
                Set(ui,"outro",t);Call(ui,"RefreshCards",0f);Call(ui,"ApplyLobbyInputState");
                Check(ui.cardsPage.activeSelf&&ui.CardOutroBlocksInput,"outro remains locked at "+t);
                Check(ui.cardsPage.GetComponent<CanvasGroup>().blocksRaycasts,"outro retains overlay raycasts at "+t);
                VerifyPointerShield();
                int before=game.bossIndex;
                Keys(help:true,escape:true,enter:true,left:true,right:true,cards:true);
                ui.SelectBoss(1);ui.EnterBattle();ui.OpenCards();ui.CloseCards();ui.OpenSettings();ui.OpenHelp();ui.OpenHistory();ui.CycleUltimate();
                Check(game.mode==ScreenMode.Lobby&&game.bossIndex==before&&!game.showCards,"keyboard and callbacks cannot change lobby at "+t);
                Check(!game.showHelp&&!game.showSettings&&!game.showHistory,"outro cannot open background dialogs at "+t);
                Check(game.save.gold==920&&game.save.cardPurchases==1,"no duplicate payment at "+t);
            }
            Set(ui,"outro",.67f);Call(ui,"RefreshCards",0f);Call(ui,"ApplyLobbyInputState");
            Check(!ui.cardsPage.activeSelf&&ui.CardOutroBlocksInput,"completion frame hides cards but consumes input");
            Keys(escape:true,enter:true);ui.EnterBattle();Check(game.mode==ScreenMode.Lobby,"completion input does not reach lobby");
            // Editor-only deterministic next-frame boundary; no wall-clock sleep or personal save is needed.
            Set(ui,"cardsReleasedFrame",Time.frameCount-1);ui.Refresh();
            Check(!ui.BlocksLobbyInput&&((CanvasGroup)Get(ui,"lobbyGroup")).interactable,"next frame restores lobby interaction");
            ui.SelectBoss(1);Check(game.bossIndex==1,"boss mouse callback works after completion");
            Keys(left:true);Check(game.bossIndex==0,"left shortcut restored");Keys(right:true);Check(game.bossIndex==1,"right shortcut restored");
            Keys(enter:true);Check(game.mode==ScreenMode.Battle,"enter shortcut restored");

            Fixture();game.save.gold=0;ui.ChooseCard(0);
            Check(game.showCards&&!ui.CardOutroBlocksInput&&!game.save.cardChosen,"failed purchase starts no outro");
            ui.CloseCards();ui.Refresh();Check(!ui.BlocksLobbyInput&&!ui.cardsPage.activeSelf,"cancel restores input after failed purchase");
            Keys(right:true);Check(game.bossIndex==1,"cancel leaves lobby usable");

            Fixture();Keys(escape:true,enter:true);
            Check(!game.showCards&&game.mode==ScreenMode.Lobby,"Escape close cannot also enter battle in same input batch");
            ui.Refresh();Keys(enter:true);Check(game.mode==ScreenMode.Battle,"next input after Escape works");

            Fixture();ui.ChooseCard(0);game.mode=ScreenMode.Title;ui.Refresh();
            Check(!ui.CardOutroBlocksInput,"mode change clears outro lock");
            game.mode=ScreenMode.Lobby;ui.Refresh();Check(!ui.BlocksLobbyInput,"returning to lobby has no stale lock");

            Fixture();ui.ChooseCard(0);ui.enabled=false;Call(ui,"OnDisable"); // Non-ExecuteAlways component lifecycle is invoked explicitly in this editor fixture.
            Check(!ui.CardOutroBlocksInput,"disabled UI releases presentation lock");
            ui.enabled=true;ui.Refresh();Check(!ui.BlocksLobbyInput,"re-enabled UI has no stale lock");
            Directory.CreateDirectory("_verify");File.WriteAllText("_verify/card-input-result.json","{\"passed\":true,\"checks\":"+checks+",\"scope\":\"Unity editor unsaved production scene, camera-rendered UI raycasts, injected keyboard/lifecycle signals; deterministic next-frame boundary\"}");
            Debug.Log("CARD_INPUT_FIXED_VERIFIED checks="+checks);
        }
        finally {SaveStore.UseIsolatedRoot(previousRoot);}
    }
}
