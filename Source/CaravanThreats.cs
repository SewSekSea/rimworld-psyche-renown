using System.Collections.Generic;
using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace Renown
{
    public sealed class CaravanThreatWorker : IncidentWorker_Ambush
    {
        public bool QueueWarning(CaravanWarning warning,IncidentParms parms)
        {
            var execute=AccessTools.DeclaredMethod(typeof(IncidentWorker_Ambush),"DoExecute",new[]{typeof(IncidentParms),typeof(List<Pawn>),typeof(IntVec3)});
            if(execute==null || execute.ReturnType!=typeof(bool) || !CanFireNow(parms))return false;
            var enemies=new List<Pawn>();
            try
            {
                enemies=GeneratePawns(parms);
                if(enemies.Count==0)return false;
                warning.Outcome="queued";
                LongEventHandler.QueueLongEvent(()=>
                {
                    try
                    {
                        if(!Eligible(parms)){warning.Outcome="cancelled before map generation";RetainFailedGeneration(enemies,parms.faction);return;}
                        bool arrived=(bool)execute.Invoke(this,new object[]{parms,enemies,IntVec3.Invalid});
                        warning.EncounterMap=NamedTarget(parms)?.Map;
                        warning.Outcome=arrived && warning.EncounterMap!=null && enemies.Any(p=>p.Spawned && p.Map==warning.EncounterMap && p.GetLord()?.LordJob is TargetedAssaultJob)?"arrived":"arrival not confirmed";
                        if(warning.Outcome!="arrived")RetainFailedGeneration(enemies,parms.faction);
                    }
                    catch(Exception error)
                    {
                        warning.EncounterMap=NamedTarget(parms)?.Map;warning.Outcome="encounter interrupted";
                        RetainFailedGeneration(enemies,parms.faction);
                        Log.Warning("[Renown] Caravan encounter interrupted: "+(error.InnerException??error).GetType().Name);
                    }
                },"GeneratingMapForNewEncounter",false,null);
                return true;
            }
            catch(Exception error)
            {
                warning.Outcome="generation interrupted";RetainFailedGeneration(enemies,parms.faction);
                Log.Warning("[Renown] Caravan generation interrupted: "+error.GetType().Name);return false;
            }
        }
        static void RetainFailedGeneration(List<Pawn> enemies,Faction faction)
        {
            foreach(var pawn in enemies.Where(p=>p!=null && !p.Destroyed && !p.Dead))
            {
                if(pawn.Spawned){if(pawn.GetLord()==null)LordMaker.MakeNewLord(faction,new TargetedAssaultJob(faction,null),pawn.Map,new[]{pawn});}
                else if(pawn.ParentHolder==null && !Find.WorldPawns.Contains(pawn))Find.WorldPawns.PassToWorld(pawn,PawnDiscardDecideMode.KeepForever);
            }
        }
        public static Pawn NamedTarget(IncidentParms parms)=>parms?.attackTargets?.Count==1?parms.attackTargets[0] as Pawn:null;
        public static bool Eligible(IncidentParms parms)
        {
            var target=NamedTarget(parms);var caravan=parms?.target as Caravan;var faction=parms?.faction;
            return caravan!=null && caravan.Spawned && caravan.Faction==Faction.OfPlayer && target!=null && !target.Dead && target.RaceProps.Humanlike && !target.DevelopmentalStage.Juvenile() && target.Faction==Faction.OfPlayer && !target.IsPrisoner && !target.IsSlave && target.GetCaravan()==caravan && caravan.PawnsListForReading.Contains(target) && faction!=null && !faction.defeated && !faction.deactivated && !faction.Hidden && faction.def.humanlikeFaction && faction.HostileTo(Faction.OfPlayer) && !float.IsNaN(parms.points) && !float.IsInfinity(parms.points) && parms.points>=100 && parms.points<=350 && new TargetedRaidStrategy().MinimumPoints(faction,PawnGroupKindDefOf.Combat)*1.05f<=parms.points;
        }
        protected override bool CanFireNowSub(IncidentParms parms)=>Eligible(parms) && base.CanFireNowSub(parms);
        protected override bool TryExecuteWorker(IncidentParms parms)=>CanFireNowSub(parms) && base.TryExecuteWorker(parms);
        protected override List<Pawn> GeneratePawns(IncidentParms parms)
        {
            if(!Eligible(parms))return new List<Pawn>();
            var group=IncidentParmsUtility.GetDefaultPawnGroupMakerParms(PawnGroupKindDefOf.Combat,parms,false);
            group.generateFightersOnly=true;group.dontUseSingleUseRocketLaunchers=true;
            var generated=new List<Pawn>();
            try{foreach(var pawn in PawnGroupMakerUtility.GeneratePawns(group,true))generated.Add(pawn);return generated;}
            catch{RetainFailedGeneration(generated,parms.faction);throw;}
        }
        protected override LordJob CreateLordJob(List<Pawn> pawns,IncidentParms parms)=>new TargetedAssaultJob(parms.faction,NamedTarget(parms));
        protected override string GetLetterLabel(Pawn pawn,IncidentParms parms)=>"Renown: caravan targeted";
        protected override string GetLetterText(Pawn pawn,IncidentParms parms)=>parms.faction.Name+" has intercepted this caravan seeking "+(NamedTarget(parms)?.LabelShort??"the warned colonist")+". The targeted attack ends after 18,000 ticks, loss of the target or peace. Attackers may defend themselves while retreating. No automatic surrender or kidnapping.";
    }
}
