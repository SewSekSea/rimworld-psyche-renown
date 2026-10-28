using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Renown
{
    public sealed class ExecutionObservation
    {
        public Map Map;
        public List<Pawn> Witnesses;
    }
    [HarmonyPatch(typeof(ExecutionUtility),nameof(ExecutionUtility.DoExecutionByCut))]
    public static class ExecutionPatch
    {
        static void Prefix(Pawn executioner,Pawn victim,out ExecutionObservation __state)
        {
            ExecutionObservation observation=null;
            ObservationGuard.Run("execution capture",()=>
            {
                if(!GameComponent_Renown.Person(executioner) || !executioner.IsFreeColonist || victim==null || victim.Dead || victim.RaceProps?.Humanlike!=true || victim==executioner || victim.Map!=executioner.Map)return;
                observation=new ExecutionObservation{Map=executioner.Map,Witnesses=executioner.Map.mapPawns.AllPawnsSpawned.Where(p=>GameComponent_Renown.Sees(p,executioner) && p.Position.DistanceTo(victim.Position)<=12 && GenSight.LineOfSight(p.Position,victim.Position,victim.Map)).ToList()};
            });
            __state=observation;
        }
        [HarmonyPriority(Priority.Last)]
        static void Postfix(Pawn executioner,Pawn victim,ExecutionObservation __state,bool __runOriginal)
        {
            if(__runOriginal && __state!=null && victim.Dead)ObservationGuard.Run("execution",()=>GameComponent_Renown.Current?.RecordExecution(executioner,victim,__state));
        }
    }
    public partial class GameComponent_Renown
    {
        public bool RecordExecution(Pawn actor,Pawn victim,ExecutionObservation observation)
        {
            if(observation==null || !Person(actor) || !actor.IsFreeColonist || actor.Map!=observation.Map || victim==null || !victim.Dead || victim.RaceProps?.Humanlike!=true || victim.Corpse?.Map!=observation.Map || Deeds.Any(d=>d.Kind=="execution" && d.Subject==victim))return false;
            int now=Find.TickManager.TicksGame;
            var deed=new Deed{Id=nextId++,Tick=now,Kind="execution",Maker=actor,Subject=victim,SubjectFaction=victim.Faction,Map=observation.Map,ProductId="execution:"+victim.ThingID,ProductLabel="executing "+victim.LabelShort,Witnesses=observation.Witnesses.Where(p=>Person(p) && p!=victim).Distinct().ToList()};
            Deeds.Add(deed);
            foreach(var witness in deed.Witnesses)
            {
                var old=Known.FirstOrDefault(k=>k.Witness==witness && k.Maker==actor);
                if(old==null)Known.Add(new Knowledge{Witness=witness,Maker=actor,DeedId=deed.Id,LearnedAt=now});
                else{Archive(old);old.DeedId=deed.Id;old.Source=null;old.Hops=0;old.LearnedAt=now;}
                DisplayBridge.Record(witness.LabelShort+" witnessed "+actor.LabelShort+" "+deed.ProductLabel+".","An observed human execution, not a job order. Being known is not being admired. No automatic respect memory or invented testimony.",witness,actor,actor.Map);
            }
            Prune();return true;
        }
    }
}
