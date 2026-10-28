using System.Linq;
using RimWorld;
using Verse;

namespace Renown
{
    public partial class GameComponent_Renown
    {
        int lastRecruitAttempt=-1,lastRecruitSuccess=-1;
        public int LastRecruitAttempt=>lastRecruitAttempt;
        public int LastRecruitSuccess=>lastRecruitSuccess;
        public bool RecruitEligible(FactionKnowledge report,Map map)=>DelegationEligible(report,map) && Fame(report.Maker)>=50;
        public bool TryReputationRecruit(Map map)
        {
            int now=Find.TickManager.TicksGame;
            if(RenownMod.Settings?.Recruits!=true || now<7*Policy.Day || lastRecruitAttempt>=0 && now-lastRecruitAttempt<30*Policy.Day)return false;
            var report=Abroad.FirstOrDefault(a=>RecruitEligible(a,map));if(report==null)return false;
            lastRecruitAttempt=now;
            var incident=DefDatabase<IncidentDef>.GetNamedSilentFail("WandererJoin");if(incident==null)return false;
            var parms=StorytellerUtility.DefaultParmsNow(incident.category,map);parms.forced=false;
            if(!(incident.Worker is IncidentWorker_GiveQuest) || !incident.Worker.CanFireNow(parms) || !incident.Worker.TryExecute(parms))return false;
            lastRecruitSuccess=now;
            DisplayBridge.Record("The colony's reputation attracted a wanderer's offer to join.",
                "Fresh reports about "+report.Maker.LabelShort+" reached "+report.Faction.Name+"; local fame was at least 50. The native WandererJoin quest provides an accept/reject letter and chooses an eligible home map. Native generation/entry rules apply, but this is an additional Renown offer, not a storyteller population roll. No faction member is transferred, no special skill or loyalty guaranteed, and no personal account invented. Global thirty-day attempt limit includes failures and declined offers.",report.Maker,null,map);
            return true;
        }
        void RefreshRecruits()
        {
            if(RenownMod.Settings?.Recruits!=true)return;
            foreach(var map in Find.Maps.Where(m=>m.IsPlayerHome))if(TryReputationRecruit(map))break;
        }
        void ExposeRecruits()
        {Scribe_Values.Look(ref lastRecruitAttempt,"renownLastRecruitAttempt",-1);Scribe_Values.Look(ref lastRecruitSuccess,"renownLastRecruitSuccess",-1);}
    }
}
