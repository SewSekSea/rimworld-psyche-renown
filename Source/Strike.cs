using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Renown
{
    public partial class GameComponent_Renown
    {
        public bool IsStriking(Pawn pawn)
        {
            if(RenownMod.Settings?.CrewStrikes!=true || RenownMod.Settings.CrewProtests!=true || !Attentive(pawn) || !pawn.IsFreeColonist || pawn.Drafted)return false;
            int now=Find.TickManager.TicksGame;
            return Requests.Any(r=>r!=null && r.StrikeAnnounced && r.Outcome=="unmet" && r.ProtestWarnedAt>=0 &&
                r.ProtestStarted>=r.ProtestWarnedAt+Policy.Day && r.ProtestClosed<0 && now>=r.ProtestStarted && now-r.ProtestStarted<Policy.Day &&
                r.Members.Contains(pawn) && ProtestGroupValid(r) && RequestNeeds.Average(r.Members,r.Kind)<.6f);
        }
        public bool RefusesWork(Pawn pawn,WorkGiver giver,bool emergency)
        {
            if(emergency || giver?.GetType()!=typeof(WorkGiver_DoBill))return false;
            string name=giver.def?.defName;
            if(name!="DoBillsSculpt" && name!="DoBillsMakeApparel" && name!="DoBillsMakeWeapons")return false;
            return IsStriking(pawn);
        }
    }
    // Native normal-work loop continues to other work givers after this false result.
    // No job cancellation, timetable edits, work-priority edits, or reenabling native-ineligible work.
    [HarmonyPatch(typeof(JobGiver_Work),"PawnCanUseWorkGiver")]
    public static class StrikeWorkPatch
    {
        static bool warned;
        [HarmonyPriority(Priority.Last)]
        static void Postfix(JobGiver_Work __instance,Pawn pawn,WorkGiver giver,ref bool __result,bool __runOriginal)
        {
            if(!__runOriginal || !__result)return;
            try{if(GameComponent_Renown.Current?.RefusesWork(pawn,giver,__instance.emergency)==true)__result=false;}
            catch(Exception e){if(!warned)Log.Warning("[Renown] Strike work adapter unavailable: "+e.GetType().Name);warned=true;}
        }
    }
}
