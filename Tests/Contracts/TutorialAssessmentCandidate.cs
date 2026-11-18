// Historical proposal superseded by ReversalTower.TutorialAssessment in PR #50.
// "Balanced" here is not the product group "normal". No runtime use.
// Test-only contract proposal. Not a SaveData/DB schema or runtime integration.
using System;
namespace TutorialContractCandidate {
 public enum Proficiency { Novice, Balanced, Expert }
 public enum EvaluationKind { Initial, Reassessment }
 public sealed class Assessment {
  public string version,assessedAt;public Proficiency? proficiency;public EvaluationKind kind;
  public bool completed;public int parries,dodges,hits,opportunities,retries;public double practiceSeconds;
  public bool ExcludedFromDdaLearning => true;
  public void Validate() {
   DateTimeOffset time;
   if(string.IsNullOrWhiteSpace(version)||version.Length>64||!DateTimeOffset.TryParse(assessedAt,out time))throw new ArgumentException("Version/time required");
   if(!Enum.IsDefined(typeof(EvaluationKind),kind)||(proficiency.HasValue&&!Enum.IsDefined(typeof(Proficiency),proficiency.Value)))throw new ArgumentException("Unknown assessment category");
   if(completed&&!proficiency.HasValue)throw new ArgumentException("Completed evaluation needs a team-supplied category");
   if(parries<0||dodges<0||hits<0||opportunities<0||retries<0||(long)parries+dodges+hits!=opportunities||double.IsNaN(practiceSeconds)||double.IsInfinity(practiceSeconds)||practiceSeconds<0)throw new ArgumentException("Invalid raw observations");
  }
 }
}
