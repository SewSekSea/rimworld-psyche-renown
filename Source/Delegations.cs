using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Renown
{
    public partial class GameComponent_Renown
    {
        int lastDelegationAttempt=-1,lastDelegationSuccess=-1;
        public int LastDelegationAttempt=>lastDelegationAttempt;
        public int LastDelegationSuccess=>lastDelegationSuccess;
        public bool DelegationEligible(FactionKnowledge report,Map map)
        {
            if(report==null || map==null || !map.IsPlayerHome || !Person(report.Maker) || !report.Maker.IsFreeColonist || report.Maker.Map!=map || Fame(report.Maker)<30)return false;
            var faction=report.Faction;int now=Find.TickManager.TicksGame;
            return faction!=null && !faction.IsPlayer && !faction.defeated && !faction.Hidden && !faction.HostileTo(Faction.OfPlayer) &&
                Policy.Fresh(report.Tick,now) && Deeds.Any(d=>d.Id==report.DeedId && d.Maker==report.Maker && Policy.Fresh(d.Tick,now)) &&
                faction.def.pawnGroupMakers?.Any(g=>g.kindDef==PawnGroupKindDefOf.Peaceful)==true;
        }
        public bool TryDelegation(Map map)
        {
            int now=Find.TickManager.TicksGame;
            if(RenownMod.Settings?.Delegations!=true || now<7*Policy.Day || lastDelegationAttempt>=0 && now-lastDelegationAttempt<20*Policy.Day)return false;
            var report=Abroad.FirstOrDefault(a=>DelegationEligible(a,map));if(report==null)return false;
            lastDelegationAttempt=now; // Failure consumes the same global budget as success.
            var incident=DefDatabase<IncidentDef>.GetNamedSilentFail("VisitorGroup");if(incident==null)return false;
            var parms=StorytellerUtility.DefaultParmsNow(incident.category,map);
            parms.faction=report.Faction;parms.forced=false;parms.points=100f;
            parms.storeGeneratedNeutralPawns=new List<Pawn>();
            if(!(incident.Worker is IncidentWorker_NeutralGroup worker) || !worker.FactionCanBeGroupSource(report.Faction,parms) || !worker.CanFireNow(parms) || !worker.TryExecute(parms))return false;
            lastDelegationSuccess=now;
            TrackHosting(report.Faction,map,report.Maker,parms.storeGeneratedNeutralPawns);
            DisplayBridge.Record("A visiting delegation from "+report.Faction.Name+" arrived after reports of "+report.Maker.LabelShort+".",
                "Optional reputation visit using native peaceful visitors (100 incident points). "+parms.storeGeneratedNeutralPawns.Count+
                " generated visitors. They use normal visit/leave behaviour and may trade under native rules. Faction awareness does not give every visitor personal knowledge, compel recruitment or create a treaty. Global twenty-day attempt cooldown; hosting reports require their separate option.",report.Maker,null,map);
            return true;
        }
        void RefreshDelegations()
        {
            if(RenownMod.Settings?.Delegations!=true)return;
            foreach(var map in Find.Maps.Where(m=>m.IsPlayerHome))if(TryDelegation(map))break;
        }
        void ExposeDelegations()
        {
            Scribe_Values.Look(ref lastDelegationAttempt,"renownLastDelegationAttempt",-1);
            Scribe_Values.Look(ref lastDelegationSuccess,"renownLastDelegationSuccess",-1);
        }
    }
}
