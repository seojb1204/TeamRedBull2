using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ReversalTower;
using U=UnityEngine;

public sealed class PreviewHost:Form
{
    TowerGame game;Timer timer;System.Diagnostics.Stopwatch clock=new System.Diagnostics.Stopwatch();double previous;
    string capture;string captureMode;bool initialized;int ticks;bool quiet,smoke;bool sawLobby,sawBattle,sawHelp,sawDamage,sawPause;
    static void Call(TowerGame game,string name)=>typeof(TowerGame).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game,null);
    [STAThread] public static int Main(string[] args)
    {
        try{Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);if(Array.IndexOf(args,"-checkAssets")>=0)return CheckAssets();if(Array.IndexOf(args,"-checkMedia")>=0)return CheckMedia();Application.Run(new PreviewHost(args));return 0;}
        catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-error.txt"),e.ToString());return 1;}
    }
    PreviewHost(string[] args)
    {
        Text="역전의 탑 · Asset Preview 0.3";ClientSize=new Size(1600,900);MinimumSize=new Size(960,600);StartPosition=FormStartPosition.CenterScreen;DoubleBuffered=true;KeyPreview=true;BackColor=Color.Black;
        U.Application.persistentDataPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"UserData");
        int at=Array.IndexOf(args,"-capture");if(at>=0&&at+1<args.Length){capture=Path.GetFullPath(args[at+1]);quiet=true;ShowInTaskbar=false;Opacity=0;}
        int mode=Array.IndexOf(args,"-view");if(mode>=0&&mode+1<args.Length)captureMode=args[mode+1];
        if(quiet)U.Application.persistentDataPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CaptureData",Guid.NewGuid().ToString("N"));
        smoke=Array.IndexOf(args,"-smoke")>=0;if(smoke){quiet=true;ShowInTaskbar=false;Opacity=0;U.Application.persistentDataPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SmokeData",Guid.NewGuid().ToString("N"));}
        KeyDown+=(s,e)=>{var key=Map(e.KeyCode);if(key.HasValue){if(!U.Input.held.Contains(key.Value))U.Input.down.Add(key.Value);U.Input.held.Add(key.Value);}e.Handled=true;e.SuppressKeyPress=true;};
        KeyUp+=(s,e)=>{var key=Map(e.KeyCode);if(key.HasValue)U.Input.held.Remove(key.Value);};
        MouseDown+=(s,e)=>{U.Input.mouseDown.Add(e.Button==MouseButtons.Right?1:0);U.Input.mouseHeld=true;};MouseUp+=(s,e)=>U.Input.mouseHeld=false;
        MouseWheel+=(s,e)=>U.Event.wheel+=e.Delta;
        Deactivate+=(s,e)=>{U.Input.Clear();if(!quiet&&game!=null&&game.mode==ScreenMode.Battle)game.paused=true;};
        FormClosing+=(s,e)=>{if(game!=null&&!quiet)Call(game,"OnApplicationQuit");NativeMixer.Stop();};
        Load+=(s,e)=>Initialize();
    }
    void Initialize()
    {
        game=new TowerGame();Call(game,"Awake");
        U.ScreenCapture.capture=path=>BeginInvoke((Action)(()=>{
            using(var bitmap=new Bitmap(ClientSize.Width,ClientSize.Height))using(var graphics=Graphics.FromImage(bitmap))
            {Render(graphics);Directory.CreateDirectory(Path.GetDirectoryName(path));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}
        }));
        if(quiet&&!smoke)
        {
            U.AudioListener.volume=0;
            if(captureMode=="lobby")game.mode=ScreenMode.Lobby;
            if(captureMode=="battle"){game.save.tutorialSeen=true;game.StartBattle(0);game.player=new U.Vector2(-1,0);game.boss=new U.Vector2(2,0);typeof(TowerGame).GetMethod("BeginPattern",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game,new object[]{game.spec.patterns[0]});game.patternAge=game.windup-.18f;game.seconds=43.2f;game.ult=63;}
            if(captureMode=="help")game.showHelp=true;
            if(captureMode=="settings")game.showSettings=true;
            if(captureMode=="result"||captureMode=="ending"){game.save.tutorialSeen=true;game.StartBattle(0);game.seconds=129;game.parries=12;game.DamageBoss(game.bossHp);game.flash=0;game.showHelp=false;if(captureMode=="ending"){game.bossIndex=3;game.mode=ScreenMode.Ending;}}
        }
        if(quiet)U.AudioListener.volume=0;
        initialized=true;clock.Start();timer=new Timer{Interval=smoke?1:16};timer.Tick+=(s,e)=>Step();timer.Start();
    }
    void Step()
    {
        double now=clock.Elapsed.TotalSeconds;U.Time.unscaledDeltaTime=(float)Math.Min(.05,now-previous);previous=now;
        U.Screen.width=ClientSize.Width;U.Screen.height=ClientSize.Height;
        var mouse=PointToClient(Cursor.Position);U.Event.rawMouse=new U.Vector2(mouse.X,mouse.Y);
        if(smoke){SmokeInput();U.Time.unscaledDeltaTime=1f/60;Call(game,"Update");}
        else if(!quiet)Call(game,"Update");else game.realClock=.6f;
        if(!quiet)NativeMixer.SetBattle(game.mode==ScreenMode.Battle&&!game.paused&&!game.showHelp);
        Call(game,"LateUpdate");Invalidate();Update();U.Input.down.Clear();U.Input.mouseDown.Clear();U.Event.wheel=0;ticks++;
        if(quiet&&!smoke&&ticks==3)
        {
            using(var bitmap=new Bitmap(ClientSize.Width,ClientSize.Height))using(var graphics=Graphics.FromImage(bitmap)){Render(graphics);Directory.CreateDirectory(Path.GetDirectoryName(capture));bitmap.Save(capture,System.Drawing.Imaging.ImageFormat.Png);}
            timer.Stop();Close();
        }
        if(smoke&&ticks>=750)
        {
            bool passed=sawLobby&&sawBattle&&sawHelp&&sawDamage&&sawPause;
            string report="PREVIEW_SMOKE_"+(passed?"PASSED":"FAILED")+"\nTitle to lobby: "+sawLobby+"\nLobby to battle: "+sawBattle+"\nHelp modal: "+sawHelp+"\nAttack damages boss: "+sawDamage+"\nPause and resume: "+sawPause+"\nFrames: "+ticks+"\n";
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-results.txt"),report);
            timer.Stop();if(!passed)throw new Exception(report);Close();
        }
    }
    void SmokeInput()
    {
        U.Input.held.Clear();
        if(ticks==0||ticks==2)U.Input.down.Add(U.KeyCode.Return);
        if(game.mode==ScreenMode.Lobby)sawLobby=true;
        if(game.mode==ScreenMode.Battle)
        {
            sawBattle=true;if(game.showHelp){sawHelp=true;U.Event.rawMouse=new U.Vector2(1180,746);U.Input.mouseDown.Add(0);}
            else
            {
                if(Math.Abs(game.boss.x-game.player.x)>1.7)U.Input.held.Add(game.boss.x>game.player.x?U.KeyCode.D:U.KeyCode.A);
                if(ticks%24==0)U.Input.down.Add(U.KeyCode.J);
                if(ticks%300==0)U.Input.down.Add(U.KeyCode.W);
                if(game.bossHp<game.spec.hp)sawDamage=true;
                if(ticks==400||ticks==405)U.Input.down.Add(U.KeyCode.Escape);
                if(game.paused)sawPause=true;
            }
        }
    }
    static int CheckAssets()
    {
        var report=new System.Text.StringBuilder();
        foreach(string n in new[]{"attack","parry","hit","dash","battle"})
        {var w=ExternalAssets.Wave(n);if(w==null||w.channels!=1||w.rate!=22050||w.samples.Length==0)throw new Exception("Invalid audio: "+n);float peak=0;foreach(float s in w.samples)peak=Math.Max(peak,Math.Abs(s));if(peak<.01f)throw new Exception("Silent audio: "+n);report.AppendLine("PASS "+n+" PCM mono 22050Hz, "+((float)w.samples.Length/w.rate).ToString("F2")+" sec");}
        foreach(string action in new[]{"idle","run","jump","J","Q","hit","parry","dash"})
        {for(int i=0;i<10;i++)if(ExternalAssets.Hero(action,i*.1f,i/10f,1)==null)throw new Exception("Missing animation: "+action);report.AppendLine("PASS animation "+action);}
        foreach(string n in new[]{"parallaxBG-interior1.png","parallaxBG-interior2.png","twilight-tiles.png"})if(ExternalAssets.Picture("Art/Castle/"+n)==null)throw new Exception("Missing environment: "+n);
        report.AppendLine("PASS castle images and colour-key import");report.AppendLine("ASSET_CHECKS_PASSED");
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"asset-checks.txt"),report.ToString());return 0;
    }
    static int CheckMedia()
    {
        int opened=0;bool failed=false;var players=new System.Collections.Generic.List<System.Windows.Media.MediaPlayer>();var report=new System.Text.StringBuilder();
        var dispatcher=System.Windows.Threading.Dispatcher.CurrentDispatcher;
        foreach(string name in new[]{"attack","battle"})
        {
            var player=new System.Windows.Media.MediaPlayer();players.Add(player);string item=name;
            player.MediaOpened+=(s,e)=>{report.AppendLine("PASS Windows MediaPlayer decoded "+item);opened++;if(opened==2)dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background);};
            player.MediaFailed+=(s,e)=>{failed=true;report.AppendLine("FAIL "+item+": "+e.ErrorException.Message);dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background);};
            player.Open(new Uri(Path.GetFullPath(Path.Combine(ExternalAssets.Root,"Audio",name+".wav"))));
        }
        var timeout=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(8)};
        timeout.Tick+=(s,e)=>{failed=true;report.AppendLine("FAIL media open timeout");dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background);};timeout.Start();
        System.Windows.Threading.Dispatcher.Run();timeout.Stop();foreach(var p in players)p.Close();
        bool ok=opened==2&&!failed;report.AppendLine(ok?"MEDIA_DECODE_PASSED":"MEDIA_DECODE_FAILED");File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"media-checks.txt"),report.ToString());return ok?0:1;
    }
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(initialized)Render(e.Graphics);}
    void Render(Graphics graphics)
    {
        graphics.SmoothingMode=SmoothingMode.AntiAlias;graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        PreviewPainter.World(graphics,ClientSize.Width,ClientSize.Height);U.GUI.graphics=graphics;U.GUI.enabled=true;U.GUI.color=new U.Color(1,1,1);Call(game,"OnGUI");U.GUI.graphics=null;
    }
    static U.KeyCode? Map(Keys key)
    {
        if(key==Keys.Enter)return U.KeyCode.Return;if(key==Keys.Escape)return U.KeyCode.Escape;if(key==Keys.Left)return U.KeyCode.LeftArrow;if(key==Keys.Right)return U.KeyCode.RightArrow;if(key==Keys.Space)return U.KeyCode.Space;if(key==Keys.ShiftKey||key==Keys.LShiftKey||key==Keys.RShiftKey)return U.KeyCode.LeftShift;
        U.KeyCode result;return Enum.TryParse(key.ToString(),out result)?(U.KeyCode?)result:null;
    }
}
