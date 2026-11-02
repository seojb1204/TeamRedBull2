#if !HEADLESS && !DESKTOP_PREVIEW
using UnityEngine;

namespace ReversalTower
{
    [CreateAssetMenu(menuName="Reversal Tower/Boss Definition")]
    public sealed class BossDefinition : ScriptableObject
    {
        public BossSpec template;

        public BossSpec CreateRuntime()
        {
            // DDA and battle state must never mutate the Inspector asset or its nested patterns.
            return JsonUtility.FromJson<BossSpec>(JsonUtility.ToJson(template));
        }
    }
}
#endif
