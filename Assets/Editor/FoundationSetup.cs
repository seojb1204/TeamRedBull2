using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using ReversalTower;

public static class FoundationSetup
{
    const string ScenePath="Assets/Scenes/TowerProduction.unity";
    static Color panel=new Color(.035f,.055f,.08f,.96f),gold=new Color(.85f,.65f,.3f);

    [MenuItem("Reversal Tower/Create Production Foundation")]
    public static void Create()
    {
        if(File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath);
            Verify();
            return;
        }
        Directory.CreateDirectory("Assets/Data/Bosses");
        Directory.CreateDirectory("Assets/Animation");
        Directory.CreateDirectory("Assets/Prefabs");
        AssetDatabase.Refresh();
        BossDefinition[] definitions=new BossDefinition[Catalog.Bosses.Length];
        for(int i=0;i<definitions.Length;i++)
        {
            string path="Assets/Data/Bosses/"+Catalog.Bosses[i].id+".asset";
            definitions[i]=AssetDatabase.LoadAssetAtPath<BossDefinition>(path);
            if(!definitions[i])
            {
                definitions[i]=ScriptableObject.CreateInstance<BossDefinition>();
                definitions[i].template=JsonUtility.FromJson<BossSpec>(JsonUtility.ToJson(Catalog.Bosses[i]));
                AssetDatabase.CreateAsset(definitions[i],path);
            }
        }
        AnimatorController arakus=CreateController(true),player=CreateController(false);
        CreatePrefab("Arakus",arakus,true);CreatePrefab("Player",player,false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var game=new GameObject("Game Flow").AddComponent<TowerGame>();
        var camera=new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=8.2f;
        camera.transform.position=new Vector3(0,5,-20);camera.gameObject.AddComponent<AudioListener>();
        var presentation=new GameObject("Scene Presentation").AddComponent<ScenePresentation>();
        presentation.player=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"))).GetComponent<ActorView>();
        presentation.arakus=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Arakus.prefab"))).GetComponent<ActorView>();
        presentation.bosses=definitions;
        presentation.ground=Collider("Ground (reference for manual collision migration)",new Vector2(0,-.5f),new Vector2(30,1));
        presentation.leftBoundary=Collider("Left Boundary",new Vector2(-12.5f,4),new Vector2(1,8));
        presentation.rightBoundary=Collider("Right Boundary",new Vector2(12.5f,4),new Vector2(1,8));
        game.scenePresentation=presentation;
        game.nativeUI=CreateCanvas(game);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        AssetDatabase.SaveAssets();
        Verify();
    }

    static BoxCollider2D Collider(string name,Vector2 position,Vector2 size)
    {var collider=new GameObject(name).AddComponent<BoxCollider2D>();collider.transform.position=position;collider.size=size;return collider;}

    static AnimatorController CreateController(bool boss)
    {
        string prefix=boss?"Arakus":"Player";
        string path="Assets/Animation/"+prefix+".controller";
        AnimatorController controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if(controller&&controller.layers.Length>0&&controller.layers[0].stateMachine.states.Length==(boss?15:10))return controller;
        if(controller)AssetDatabase.DeleteAsset(path);
        controller=AnimatorController.CreateAnimatorControllerAtPath(path);
        string[] states=boss?new[]{"Idle","Run","Attack","SwordSwing","ShieldStrike","Kick","Charge","ShieldThrow","Sweep","Phase","Hit","Death","QuickStep","ShieldDash","ShieldSlam"}:
            new[]{"Idle","Run","Jump","Fall","Attack","Skill","Parry","Dash","Hit","Death"};
        string[] sheets=boss?new[]{"Idle","Idle","NormalAttack_R_PT","Sword_Swinging_PT","NormalAttack_L_PT","NormalAttack_F_PT","Charge_PT","ShieldThrowing_DashAttack_PT","Aggressive_Attack_PT","Of_Wrath_PT","Groggy","Groggy","QuickStep_PT","ShieldDash_PT","Shield_Throwing_Fly_Attack_PT"}:
            states;   // 주인공: 상태마다 SilverRogue_<상태>.png 한 장 (서정범 님 은발 검사 v2, 오른쪽을 보도록 반전)
        for(int i=0;i<states.Length;i++)
        {
            string sheet=boss?"Assets/Art/Arakus/Arakus_"+sheets[i]+".png":"Assets/Art/Player/SilverRogue/SilverRogue_"+sheets[i]+".png";
            Sprite[] sprites=boss?ImportSheet(sheet,156,146,new Vector2(82f/156,4f/146)):ImportSheet(sheet,128,128,new Vector2(.4618f,.125f),75);
            var clip=new AnimationClip{name=prefix+"_"+states[i],frameRate=8};
            var keys=new ObjectReferenceKeyframe[sprites.Length+1];
            for(int n=0;n<sprites.Length;n++)keys[n]=new ObjectReferenceKeyframe{time=n/8f,value=sprites[n]};
            keys[sprites.Length]=new ObjectReferenceKeyframe{time=sprites.Length/8f,value=sprites[sprites.Length-1]};
            AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"},keys);
            AssetDatabase.CreateAsset(clip,"Assets/Animation/"+clip.name+".anim");
            var state=controller.layers[0].stateMachine.AddState(states[i]);state.motion=clip;
            if(i==0)controller.layers[0].stateMachine.defaultState=state;
        }
        // The projectile is also imported, but no existing pattern is silently replaced by a new mechanic.
        if(boss)ImportSheet("Assets/Art/Arakus/Arakus_Shield_Projectile.png",40,40,new Vector2(.5f,.5f));
        return controller;
    }

    static Sprite[] ImportSheet(string path,int width,int height,Vector2 pivot,float pixelsPerUnit=45)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        if(!importer)throw new Exception("Missing sprite sheet: "+path);
        int sourceWidth,sourceHeight;importer.GetSourceTextureWidthAndHeight(out sourceWidth,out sourceHeight);
        int count=sourceWidth/width;
        if(sourceWidth%width!=0||sourceHeight!=height)throw new Exception("Invalid frame dimensions: "+path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=pixelsPerUnit;importer.filterMode=FilterMode.Point;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;
        var slices=new SpriteMetaData[count];
        for(int i=0;i<count;i++)slices[i]=new SpriteMetaData{name=Path.GetFileNameWithoutExtension(path)+"_"+i.ToString("D2"),rect=new Rect(i*width,0,width,height),alignment=9,pivot=pivot};
#pragma warning disable 0618
        importer.spritesheet=slices;
#pragma warning restore 0618
        importer.SaveAndReimport();
        Sprite[] result=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
        if(result.Length!=count)throw new Exception("Sprite slicing failed: "+path);
        return result;
    }

    static void CreatePrefab(string name,AnimatorController controller,bool boss)
    {
        string path="Assets/Prefabs/"+name+".prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path))return;
        var root=new GameObject(name);var view=root.AddComponent<ActorView>();
        view.body=root.AddComponent<SpriteRenderer>();view.body.sortingOrder=1000;
        view.animator=root.AddComponent<Animator>();view.animator.runtimeAnimatorController=controller;
        view.animator.speed=0;view.bossActor=boss;view.sourcePack=boss?"Team Arakus_PixelSprites":"Team Silver Rogue v2";
        view.body.sprite=((AnimationClip)controller.layers[0].stateMachine.defaultState.motion).objectReferenceDefaultSprite();
        PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);
    }
    static Sprite objectReferenceDefaultSprite(this AnimationClip clip)
    {var binding=AnimationUtility.GetObjectReferenceCurveBindings(clip)[0];return (Sprite)AnimationUtility.GetObjectReferenceCurve(clip,binding)[0].value;}

    static GameObject Panel(Transform parent,string name,float x,float y,float w,float h,bool background=true)
    {
        var obj=new GameObject(name,typeof(RectTransform));obj.transform.SetParent(parent,false);
        var rect=(RectTransform)obj.transform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
        rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);
        if(background)obj.AddComponent<Image>().color=panel;
        return obj;
    }
    static Text Label(Transform parent,string name,string text,float x,float y,float w,float h,int size=22)
    {
        var obj=Panel(parent,name,x,y,w,h,false);var label=obj.AddComponent<Text>();
        label.text=text;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=size;
        label.color=new Color(.91f,.92f,.87f);label.raycastTarget=false;label.alignment=TextAnchor.UpperLeft;
        return label;
    }
    static Button Button(Transform parent,string text,float x,float y,float w,float h,UnityAction action)
    {
        var obj=Panel(parent,text,x,y,w,h);obj.GetComponent<Image>().color=new Color(.14f,.20f,.24f);
        var button=obj.AddComponent<Button>();if(action!=null)UnityEventTools.AddPersistentListener(button.onClick,action);
        var label=Label(obj.transform,"Label",text,14,12,w-28,h-15,21);return button;
    }
    static Image Health(Transform parent,float x,float y,Color color)
    {
        var back=Panel(parent,"HealthBackground",x,y,470,14);
        var fill=Panel(back.transform,"Fill",0,0,470,14).GetComponent<Image>();fill.color=color;fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;
        return fill;
    }
    static NativeGameUI CreateCanvas(TowerGame game)
    {
        var root=new GameObject("Game Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(CanvasGroup),typeof(NativeGameUI));
        root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        var ui=root.GetComponent<NativeGameUI>();ui.game=game;ui.inputGroup=root.GetComponent<CanvasGroup>();
        ui.titlePage=Panel(root.transform,"Title",0,0,1600,900,false);
        Label(ui.titlePage.transform,"Title","역전의 탑",100,180,700,120,72);
        Label(ui.titlePage.transform,"Subtitle","검을 받아내고, 쓰러뜨린 자의 힘을 계승하라.",104,330,720,70,25);
        Button(ui.titlePage.transform,"탑에 입장",104,510,400,65,ui.EnterLobby);
        Button(ui.titlePage.transform,"설정",104,600,190,55,ui.OpenSettings);Button(ui.titlePage.transform,"조작 안내",310,600,195,55,ui.OpenHelp);
        ui.lobbyPage=Panel(root.transform,"Lobby",0,0,1600,900,false);
        Label(ui.lobbyPage.transform,"Title","맹세의 회랑",70,45,600,70,40);
        ui.lobbyStats=Label(ui.lobbyPage.transform,"Progression","",70,125,850,90);
        ui.bossButtons=new Button[5];
        for(int i=0;i<5;i++)
        {
            ui.bossButtons[i]=Button(ui.lobbyPage.transform,Catalog.Bosses[i].name,70,245+i*76,360,62,null);
            UnityEventTools.AddIntPersistentListener(ui.bossButtons[i].onClick,ui.SelectBoss,i);
        }
        ui.bossDescription=Label(ui.lobbyPage.transform,"Boss Description","",490,245,620,330,25);
        Button(ui.lobbyPage.transform,"강화 없이 입장 [ENTER]",490,600,570,65,ui.EnterBattle);
        ui.upgradeButton=Button(ui.lobbyPage.transform,"강화 선택 [C]",70,735,620,65,ui.OpenCards);
        ui.upgradeLabel=ui.upgradeButton.GetComponentInChildren<Text>();
        Button(ui.lobbyPage.transform,"기록",1100,70,130,55,ui.OpenHistory);Button(ui.lobbyPage.transform,"설정",1245,70,130,55,ui.OpenSettings);Button(ui.lobbyPage.transform,"조작 안내",1390,70,150,55,ui.OpenHelp);
        ui.battlePage=Panel(root.transform,"Battle HUD",0,0,1600,180);
        ui.playerHealth=Health(ui.battlePage.transform,60,105,new Color(.3f,.85f,.8f));ui.bossHealth=Health(ui.battlePage.transform,1060,105,gold);
        ui.battleStats=Label(ui.battlePage.transform,"Battle Status","",60,15,1450,90,21);
        ui.resultPage=Panel(root.transform,"Result",250,140,1100,650);
        ui.resultStats=Label(ui.resultPage.transform,"Summary","",60,45,980,355,29);
        Button(ui.resultPage.transform,"로비로 돌아가기",60,455,430,60,ui.EnterLobby);
        ui.retryButton=Button(ui.resultPage.transform,"다시 도전",530,455,430,60,ui.Retry);
        ui.endingButton=Button(ui.resultPage.transform,"엔딩 보기",530,535,430,60,ui.ShowEnding);
        ui.endingPage=Panel(root.transform,"Ending",250,140,1100,650);
        ui.endingText=Label(ui.endingPage.transform,"Ending Text","",70,70,950,320,32);Button(ui.endingPage.transform,"로비로 돌아가기",70,500,500,65,ui.EnterLobby);
        ui.cardsPage=Panel(root.transform,"Lobby Card Selection",0,0,1600,900);
        Label(ui.cardsPage.transform,"Title","영구 강화 · 세 카드 중 하나를 선택하세요",230,180,1140,70,34);
        ui.cardButtons=new Button[3];ui.cardLabels=new Text[3];
        for(int i=0;i<3;i++)
        {
            ui.cardButtons[i]=Button(ui.cardsPage.transform,"",230+i*390,290,360,360,null);
            UnityEventTools.AddIntPersistentListener(ui.cardButtons[i].onClick,ui.ChooseCard,i);
            ui.cardLabels[i]=ui.cardButtons[i].GetComponentInChildren<Text>();ui.cardLabels[i].fontSize=23;
        }
        Button(ui.cardsPage.transform,"선택하지 않고 돌아가기",545,710,510,60,ui.CloseCards);
        ui.pausePage=Panel(root.transform,"Pause",490,210,620,490);
        Label(ui.pausePage.transform,"Title","잠시 검을 내려놓고",45,40,530,70,32);
        Button(ui.pausePage.transform,"계속하기",45,150,530,65,ui.Resume);Button(ui.pausePage.transform,"설정",45,235,530,65,ui.OpenSettings);Button(ui.pausePage.transform,"전투 중단",45,320,530,65,ui.Abandon);
        ui.notice=Label(root.transform,"Notice","",240,845,1120,45,19);ui.notice.color=gold;
        new GameObject("Event System",typeof(EventSystem),typeof(StandaloneInputModule));
        return ui;
    }

    public static void Verify()
    {
        foreach(string guid in AssetDatabase.FindAssets("t:BossDefinition"))
        {
            var definition=AssetDatabase.LoadAssetAtPath<BossDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            BossSpec copy=definition.CreateRuntime();float original=definition.template.patterns[0].damage;
            copy.patterns[0].damage+=100;
            if(definition.template.patterns[0].damage!=original)throw new Exception("Boss asset mutated through runtime clone");
        }
        if(AssetDatabase.FindAssets("t:AnimatorController",new[]{"Assets/Animation"}).Length!=2)throw new Exception("Missing controllers");
        if(!File.Exists(ScenePath)||!AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Arakus.prefab"))throw new Exception("Missing production scene/prefab");
        foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/Animation"}))
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
            var bindings=AnimationUtility.GetObjectReferenceCurveBindings(clip);
            if(bindings.Length!=1||AnimationUtility.GetObjectReferenceCurve(clip,bindings[0]).Any(key=>key.value==null))throw new Exception("Unbound animation sprite: "+clip.name);
        }
        var game=UnityEngine.Object.FindAnyObjectByType<TowerGame>();
        if(!game||!game.nativeUI||!game.scenePresentation||!game.scenePresentation.player||!game.scenePresentation.arakus)throw new Exception("Unbound production scene");
        Debug.Log("FOUNDATION_CHECKS_PASSED: cloned boss definitions, controllers, prefabs, production scene");
    }
    public static void Build()
    {
        Create();BuildProject.Build();
    }
}

