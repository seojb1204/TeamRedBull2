// Editor 전용 검증 리포터. 게임 동작과 밸런스를 바꾸지 않고 현재 상태만 기록한다.
// Assets/Editor 안에 있으므로 런타임 빌드에 포함되지 않는다.
// 배치모드 진입점: TowerVerify.ReportBaseline / TowerVerify.BuildWindows
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class TowerVerify
{
    const string Marker = "TOWER_VERIFY";

    static string OutDir
    {
        get
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../_verify"));
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    static void Line(StringBuilder sb, string key, string value)
    {
        sb.Append(key).Append('\t').Append(value).Append('\n');
        Debug.Log(Marker + " " + key + " = " + value);
    }

    static string LowLowPath(string company, string product)
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", company, product);
    }

    /// 현재 프로젝트 기준 상태를 기록한다. 설정을 바꾸지 않는다.
    public static void ReportBaseline()
    {
        int exit = 0;
        var sb = new StringBuilder();
        try
        {
            Line(sb, "reportedAtUtc", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            Line(sb, "unityVersion", Application.unityVersion);
            Line(sb, "platform", Application.platform.ToString());
            Line(sb, "isBatchMode", Application.isBatchMode.ToString());
            Line(sb, "projectPath", Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
            Line(sb, "dataPath", Application.dataPath);

            // 에디터가 실제로 쓰는 저장 경로. 빌드판 경로와 다를 수 있다.
            Line(sb, "editor.persistentDataPath", Application.persistentDataPath);
            Line(sb, "playerSettings.companyName", PlayerSettings.companyName);
            Line(sb, "playerSettings.productName", PlayerSettings.productName);
            Line(sb, "expected.playerSavePath", LowLowPath(PlayerSettings.companyName, PlayerSettings.productName));
            Line(sb, "savePathsMatch",
                string.Equals(
                    Path.GetFullPath(Application.persistentDataPath).TrimEnd('\\', '/'),
                    Path.GetFullPath(LowLowPath(PlayerSettings.companyName, PlayerSettings.productName)).TrimEnd('\\', '/'),
                    StringComparison.OrdinalIgnoreCase) ? "true" : "false");

            Line(sb, "playerSettings.bundleVersion", PlayerSettings.bundleVersion);
            Line(sb, "playerSettings.colorSpace", PlayerSettings.colorSpace.ToString());
            Line(sb, "playerSettings.defaultScreen",
                PlayerSettings.defaultScreenWidth + "x" + PlayerSettings.defaultScreenHeight);
            Line(sb, "playerSettings.fullScreenMode", PlayerSettings.fullScreenMode.ToString());
            Line(sb, "playerSettings.runInBackground", PlayerSettings.runInBackground.ToString());

            Line(sb, "quality.vSyncCount", QualitySettings.vSyncCount.ToString());
            Line(sb, "application.targetFrameRate", Application.targetFrameRate.ToString());
            Line(sb, "graphicsDeviceType", SystemInfo.graphicsDeviceType.ToString());
            Line(sb, "renderPipelineAsset",
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline == null
                    ? "none (Built-in)"
                    : UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.ToString());

            Line(sb, "activeBuildTarget", EditorUserBuildSettings.activeBuildTarget.ToString());

            var scenes = EditorBuildSettings.scenes;
            Line(sb, "buildScenes.count", scenes.Length.ToString());
            for (int i = 0; i < scenes.Length; i++)
                Line(sb, "buildScenes[" + i + "]", scenes[i].path + " enabled=" + scenes[i].enabled);

            string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts" });
            Line(sb, "assets.scripts.count", scriptGuids.Length.ToString());
            Line(sb, "assets.metaFileCount",
                Directory.GetFiles(Application.dataPath, "*.meta", SearchOption.AllDirectories).Length.ToString());

            // 실제 Unity 런타임에서 보스 카탈로그 / LinUCB / 직렬화 점검
            try
            {
                BuildProject.RunChecks();
                Line(sb, "towerChecks", "PASS");
            }
            catch (Exception e)
            {
                Line(sb, "towerChecks", "FAIL " + e.Message);
                exit = 1;
            }

            Line(sb, "result", exit == 0 ? "BASELINE_OK" : "BASELINE_FAIL");
        }
        catch (Exception e)
        {
            sb.Append("fatal\t").Append(e).Append('\n');
            Debug.LogError(Marker + " fatal " + e);
            exit = 1;
        }

        File.WriteAllText(Path.Combine(OutDir, "baseline.txt"), sb.ToString(), new UTF8Encoding(false));
        Debug.Log(Marker + " WROTE " + Path.Combine(OutDir, "baseline.txt"));
        if (Application.isBatchMode) EditorApplication.Exit(exit);
    }

    /// Windows 빌드.
    /// 주의: BuildProject.Build() 는 배치모드에서 스스로 EditorApplication.Exit 를 호출한다.
    /// 그 뒤의 코드는 실행되지 않으므로 기록은 호출 "전"에 끝낸다.
    /// 빌드 성공 여부는 03_build.log 의 TOWER_BUILD_SUCCESS 마커와 산출물 존재로 판정한다.
    public static void BuildWindows()
    {
        var sb = new StringBuilder();
        try
        {
            string company = "IndependentTower", product = "Reversal Tower";
            Line(sb, "preBuild.startedUtc", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            Line(sb, "preBuild.companyName", company);
            Line(sb, "preBuild.productName", product);
            Line(sb, "preBuild.editorPersistentDataPath", Application.persistentDataPath);
            Line(sb, "expected.playerSavePath", LowLowPath(company, product));
            Line(sb, "expected.outputPath",
                Path.GetFullPath(Path.Combine(Application.dataPath, "../../Windows/ReversalTower.exe")));
            Line(sb, "note", "성공 여부는 03_build.log 의 TOWER_BUILD_SUCCESS 마커로 확인");
            File.WriteAllText(Path.Combine(OutDir, "build.txt"), sb.ToString(), new UTF8Encoding(false));
        }
        catch (Exception e) { Debug.LogError(Marker + " preBuild 기록 실패 " + e); }

        BuildProject.Build();   // 여기서부터는 돌아오지 않는다
    }
}
