using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Renown
{
    public sealed class BountyRaidStrategy : TargetedRaidStrategy
    {
        protected override LordJob MakeLordJob(IncidentParms parms,Map map,System.Collections.Generic.List<Pawn> pawns,int groupID=0)=>new BountyHuntJob(parms.attackTargets?.OfType<Pawn>().SingleOrDefault());
    }
    public sealed class BountyHuntJob : LordJob
    {
        public Pawn Target;
        int started;
        bool ended;
        bool releasing;
        int lastReleaseAttempt=-1;
        public bool Active=>!ended && !releasing;
        public BountyHuntJob(){}
        public BountyHuntJob(Pawn target){Target=target;started=Find.TickManager.TicksGame;}
        public override StateGraph CreateGraph()
        {
            var graph=new StateGraph();var exit=new LordToil_ExitMapAndDefendSelf();
            var release=new BountyReleaseToil();var hunt=new BountyHuntToil(Target);
            graph.AddToil(hunt);graph.AddToil(release);
            var stop=new Transition(hunt,release);stop.AddTrigger(new Trigger_Memo("RN_EndBounty"));graph.AddTransition(stop);
            graph.AddToil(exit);var leave=new Transition(release,exit);leave.AddTrigger(new Trigger_Memo("RN_BountyReleased"));graph.AddTransition(leave);return graph;
        }
        public override void LordJobTick()
        {
            if(ended)return;
            bool carried=Target!=null && lord.ownedPawns.Any(p=>p.carryTracker?.CarriedThing==Target);
            if(!releasing && (Target==null || Target.Dead || !Target.IsFreeColonist || !carried && (!Target.Spawned || Target.Map!=Map) || lord.faction==null || !lord.faction.HostileTo(Faction.OfPlayer) || Find.TickManager.TicksGame-started>=18000))
            {
                releasing=true;lord.ReceiveMemo("RN_EndBounty");
                foreach(var pawn in lord.ownedPawns.ToArray())
                {
                    pawn.pather.StopDead();
                    if(pawn.CurJob?.def==JobDefOf.Kidnap && pawn.CurJob.targetA.Pawn==Target)pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }
            if(!releasing || lastReleaseAttempt>=0 && Find.TickManager.TicksGame-lastReleaseAttempt<60)return;
            lastReleaseAttempt=Find.TickManager.TicksGame;
            foreach(var pawn in lord.ownedPawns.ToArray())
                if(Target!=null && pawn.carryTracker?.CarriedThing==Target)pawn.carryTracker.TryDropCarriedThing(pawn.Position,ThingPlaceMode.Near,out _);
            if(Target!=null && lord.ownedPawns.Any(p=>p.carryTracker?.CarriedThing==Target))return;
            ended=true;lord.ReceiveMemo("RN_BountyReleased");
        }
        public override void ExposeData(){Scribe_References.Look(ref Target,"renownBountyTarget");Scribe_Values.Look(ref started,"renownBountyStarted");Scribe_Values.Look(ref ended,"renownBountyEnded");Scribe_Values.Look(ref releasing,"renownBountyReleasing");Scribe_Values.Look(ref lastReleaseAttempt,"renownBountyReleaseAttempt",-1);}
    }
    public sealed class BountyReleaseToil : LordToil
    {
        public override void UpdateAllDuties(){foreach(var pawn in lord.ownedPawns)pawn.mindState.duty=new PawnDuty(DefDatabase<DutyDef>.GetNamed("RN_BountyRelease"));}
    }
    public sealed class JobGiver_BountyRelease : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn){var job=JobMaker.MakeJob(JobDefOf.Wait);job.expiryInterval=60;job.checkOverrideOnExpire=true;return job;}
    }
    public sealed class BountyHuntToil : LordToil
    {
        readonly Pawn target;
        public BountyHuntToil(Pawn target){this.target=target;}
        public override void UpdateAllDuties()
        {
            var duty=DefDatabase<DutyDef>.GetNamed("RN_BountyHunt");
            foreach(var pawn in lord.ownedPawns)pawn.mindState.duty=new PawnDuty(duty,target);
        }
    }
    public sealed class JobGiver_BountyKidnap : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)=>pawn.GetLord()?.LordJob is BountyHuntJob hunt && hunt.Active?BountyKidnap.TryJob(pawn,hunt.Target):null;
    }
    public static class BountyKidnap
    {
        public static Job TryJob(Pawn hunter,Pawn target)
        {
            if(hunter==null || !hunter.Spawned || hunter.Dead || hunter.Downed || !hunter.RaceProps.Humanlike || hunter.Faction==null || !hunter.Faction.HostileTo(Faction.OfPlayer) ||
                !GameComponent_Renown.Person(target) || !target.IsFreeColonist || !target.Downed || target.Map!=hunter.Map || !hunter.HostileTo(target) || hunter.carryTracker?.CarriedThing!=null || GenAI.InDangerousCombat(hunter))return null;
            if(!RCellFinder.TryFindBestExitSpot(hunter,out var exit,TraverseMode.ByPawn,true))return null;
            var excluded=hunter.Map.mapPawns.AllPawnsSpawned.Where(p=>p!=target).Cast<Thing>().ToList();
            if(!KidnapAIUtility.TryFindGoodKidnapVictim(hunter,18f,out var victim,excluded) || victim!=target)return null;
            var job=JobMaker.MakeJob(JobDefOf.Kidnap,target,exit);job.count=1;return job;
        }
    }
}
