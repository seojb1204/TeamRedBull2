using System;
using ReversalTower;
public static class CombatSoundDesignTests{
 public static void Main(){int n=0;foreach(int rate in new[]{8000,44100,48000,96000})foreach(string key in new[]{"cue","clang","parry","attack","dash","hit","heal","death","confirm","thud","boss_0_ShieldThrow","boss_0_landing","boss_0_Slash_sweep_low","boss_1_Volley_orb","boss_1_Barrier_barrier","boss_2_Slash_scythe","boss_3_Rain_thunder","boss_4_Echo_echo"}){var a=CombatSoundDesign.Render(key,rate);var b=CombatSoundDesign.Render(key,rate);double energy=0;for(int i=0;i<a.Length;i++){if(float.IsNaN(a[i])||float.IsInfinity(a[i])||Math.Abs(a[i])>.7f||a[i]!=b[i])throw new Exception(key);energy+=a[i]*a[i];}if(energy<.001||a[0]!=0||Math.Abs(a[a.Length-1])>.01)throw new Exception("envelope "+key);n++;}Console.WriteLine("COMBAT_SOUND_TESTS_PASSED "+n);}
}
