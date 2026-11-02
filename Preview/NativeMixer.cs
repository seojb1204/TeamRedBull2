using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using U=UnityEngine;

namespace ReversalTower
{
    // Uses the standard managed Windows MediaPlayer. No custom native audio calls.
    public static class NativeMixer
    {
        static readonly List<MediaPlayer> effects=new List<MediaPlayer>();
        static MediaPlayer music;static bool battle,failed;
        public static bool Available=>!failed;
        public static bool TryPlay(string name)
        {
            string path=Path.Combine(ExternalAssets.Root,"Audio",name+".wav");if(!File.Exists(path))return false;
            try
            {
                if(effects.Count>=12){effects[0].Close();effects.RemoveAt(0);}
                var player=new MediaPlayer();player.Volume=U.AudioListener.volume*.55;
                player.Open(new Uri(Path.GetFullPath(path)));player.Play();effects.Add(player);return true;
            }
            catch(Exception e){failed=true;U.Debug.LogWarning("Media playback: "+e.Message);return false;}
        }
        public static void SetBattle(bool active)
        {
            if(failed)return;
            try
            {
                string path=Path.Combine(ExternalAssets.Root,"Audio","battle.wav");
                if(music==null&&active&&U.AudioListener.volume>.01f&&File.Exists(path))
                {music=new MediaPlayer();music.Open(new Uri(Path.GetFullPath(path)));music.MediaEnded+=(s,e)=>{music.Position=TimeSpan.Zero;if(battle)music.Play();};}
                if(music==null)return;music.Volume=U.AudioListener.volume*.3;
                if(active!=battle){battle=active;if(active)music.Play();else music.Pause();}
            }
            catch(Exception e){failed=true;U.Debug.LogWarning("Music playback: "+e.Message);}
        }
        public static void Stop(){foreach(var player in effects)player.Close();effects.Clear();if(music!=null)music.Close();}
    }
}
