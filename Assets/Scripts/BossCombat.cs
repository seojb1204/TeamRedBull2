using UnityEngine;
namespace ReversalTower
{
    // Compatibility facade while player combat and UI are migrated incrementally.
    // Pattern execution and its mutable state are owned by BossPatternSystem.
    public sealed partial class TowerGame
    {
        BossPatternSystem bossPatterns;
        public BossPatternSystem BossPatterns => bossPatterns ?? (bossPatterns = new BossPatternSystem(this));
        public Pattern current { get => BossPatterns.current; set => BossPatterns.current=value; }
        public float patternAge { get => BossPatterns.patternAge; set => BossPatterns.patternAge=value; }
        public float windup { get => BossPatterns.windup; set => BossPatterns.windup=value; }
        public float recovery { get => BossPatterns.recovery; set => BossPatterns.recovery=value; }
        public float rest { get => BossPatterns.rest; set => BossPatterns.rest=value; }
        public float patternDamage { get => BossPatterns.patternDamage; set => BossPatterns.patternDamage=value; }
        public float targetX { get => BossPatterns.targetX; set => BossPatterns.targetX=value; }
        public float safeSide { get => BossPatterns.safeSide; set => BossPatterns.safeSide=value; }
        public float nextHit { get => BossPatterns.nextHit; set => BossPatterns.nextHit=value; }
        public float bossStagger { get => BossPatterns.bossStagger; set => BossPatterns.bossStagger=value; }
        public float barrierHp { get => BossPatterns.barrierHp; set => BossPatterns.barrierHp=value; }
        public float barrierTime { get => BossPatterns.barrierTime; set => BossPatterns.barrierTime=value; }
        public int patternCount { get => BossPatterns.patternCount; set => BossPatterns.patternCount=value; }
        public int attackStep { get => BossPatterns.attackStep; set => BossPatterns.attackStep=value; }
        public Vector2 origin { get => BossPatterns.origin; set => BossPatterns.origin=value; }
        public bool phaseTwo { get => BossPatterns.phaseTwo; set => BossPatterns.phaseTwo=value; }
        public bool thirdPhase { get => BossPatterns.thirdPhase; set => BossPatterns.thirdPhase=value; }
        public bool pillarLeft { get => BossPatterns.pillarLeft; set => BossPatterns.pillarLeft=value; }
        public bool pillarRight { get => BossPatterns.pillarRight; set => BossPatterns.pillarRight=value; }
        public bool barrier { get => BossPatterns.barrier; set => BossPatterns.barrier=value; }
        void TickBoss(float dt) => BossPatterns.TickBoss(dt);
        void BeginPattern(Pattern pattern) => BossPatterns.BeginPattern(pattern);
        void ExecutePattern() => BossPatterns.ExecutePattern();
        void TickShots(float dt) => BossPatterns.TickShots(dt);
        void TickHazards(float dt) => BossPatterns.TickHazards(dt);
        void TickMinions(float dt) => BossPatterns.TickMinions(dt);
        void FireShot(Vector2 pos, Vector2 velocity, float damage, bool parry, string pattern, bool friendly=false)
            => BossPatterns.FireShot(pos, velocity, damage, parry, pattern, friendly);
        public bool BattleEnded => ended;
        public void PlaySound(string name) => sound.Play(name);
    }
}
