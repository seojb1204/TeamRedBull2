// Test-only boundary. This is not group-aware Cold Start or a runtime difficulty policy.
using System;
using ReversalTower;
public static class TutorialAssessmentLabAdapter
{
    public static bool TryProfile(TutorialAssessment value,out string labProfile)
    {
        labProfile=null;
        if(value==null||value.schemaVersion!=1||!value.completed||value.skipped||!value.valid)return false;
        if(value.parryAttempts<0||value.dodgeAttempts<0||value.parrySuccesses<0||value.dodgeSuccesses<0||
           value.parrySuccesses>value.parryAttempts||value.dodgeSuccesses>value.dodgeAttempts||value.damageHits<0||value.retryCount<0||
           !Finite(value.score)||!Finite(value.confidence)||!Finite(value.activeDurationSeconds)||value.activeDurationSeconds<0||
           value.score<0||value.score>1||value.confidence<0||value.confidence>1)return false;
        // Product uses "normal"; the existing experimental rule agent uses "balanced".
        // Mapping names does not calibrate these agents as equivalent human skill levels.
        switch(value.group){case "novice":labProfile="novice";return true;case "normal":labProfile="balanced";return true;case "expert":labProfile="expert";return true;default:return false;}
    }
    static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
}
