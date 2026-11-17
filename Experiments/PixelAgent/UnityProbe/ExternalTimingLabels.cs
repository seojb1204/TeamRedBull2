using System;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;
namespace ReversalTower {
// Evaluation-only ground truth. Never referenced by an action policy.
public sealed class ExternalTimingLabels:MonoBehaviour {
 readonly StringBuilder rows=new StringBuilder("qpc100ns,gameSeconds,cueEta,melee,glow,hits,hp,shots\n");
 [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool QueryPerformanceCounter(out long value);
 [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool QueryPerformanceFrequency(out long value);
 TowerGame game;int count;string path;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void Attach(){
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-towerTimingLabels")<0)return;
  if(!AutoRunArgs.ExternalInput||!SaveStore.IsIsolated)throw new InvalidOperationException("labels require isolated external mode");
  var o=new GameObject("Evaluation-only timing labels");o.AddComponent<ExternalTimingLabels>();
 }
 void LateUpdate(){
  if(!AutoRunArgs.ExternalInput||!SaveStore.IsIsolated||count>=1500)return;
  if(game==null)game=FindAnyObjectByType<TowerGame>();if(game==null)return;
  if(path==null)path=Path.Combine(AutoRunArgs.DataRoot,"evaluation-only-timing.csv");
  var bp=game.BossPatterns;float eta=bp.ParryCueTime(out bool melee);
  if(!QueryPerformanceCounter(out long tick)||!QueryPerformanceFrequency(out long frequency)||frequency<=0)throw new InvalidOperationException("Native QPC unavailable");
  long stamp=(long)(tick*(10000000.0/frequency));
  var ci=CultureInfo.InvariantCulture;
  rows.Append(stamp).Append(',').Append(game.seconds.ToString("F6",ci)).Append(',').Append(eta.ToString("F6",ci)).Append(',').Append(melee?1:0).Append(',').Append(bp.parryGlow.ToString("F6",ci)).Append(',').Append(game.hits).Append(',').Append(game.playerHp.ToString("F3",ci)).Append(',').Append(game.shots.Count).Append('\n');count++;
 }
 void OnApplicationQuit(){if(path!=null)File.WriteAllText(path,rows.ToString(),new UTF8Encoding(false));}
}
}
