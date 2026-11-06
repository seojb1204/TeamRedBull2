#if !HEADLESS && !DESKTOP_PREVIEW
using UnityEngine;

namespace ReversalTower
{
    // Runs independently of whether F8 is visible. No Scene/Prefab edits or network access.
    public sealed class DdaHistoryRuntime : MonoBehaviour
    {
        static DdaHistoryRuntime instance;
        DdaHistoryRecorder recorder;
        float retry;
        string reported;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            if (instance && instance.recorder != null) instance.recorder.Dispose();
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance) return;
            var obj = new GameObject("DDA Difficulty History");
            DontDestroyOnLoad(obj);
            instance = obj.AddComponent<DdaHistoryRuntime>();
        }

        void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this;
            recorder = new DdaHistoryRecorder(() => {
                var game = FindAnyObjectByType<TowerGame>();
                return game ? game.spec : null;
            }, () => SaveStore.Root);
        }

        public static DdaDifficultyHistoryStore Store => instance && instance.recorder != null ? instance.recorder.Store : null;
        public static string Error => instance && instance.recorder != null ? instance.recorder.CaptureError : null;

        void Update()
        {
            if (recorder == null) return;
            retry -= Time.unscaledDeltaTime;
            if (retry > 0) return;
            retry = 5;
            recorder.Flush();
            string error = recorder.CaptureError ?? recorder.Store.LastError;
            if (!string.IsNullOrEmpty(error) && error != reported) Debug.LogError("[DDA History] " + error);
            reported = error;
        }

        void OnApplicationQuit() { if (recorder != null) recorder.Flush(); }
        void OnDestroy()
        {
            if (recorder != null) recorder.Dispose();
            if (instance == this) instance = null;
        }
    }
}
#endif
