using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Renown
{
    public sealed class CaravanWarning : IExposable
    {
        public Pawn Target;
        public Map EncounterMap;
        public Faction Faction;
        public int DeedId,Warned,Closed=-1;
        public string Outcome="warning";
        public void ExposeData()
        {
            Scribe_References.Look(ref Target,"target");Scribe_References.Look(ref Faction,"faction");
            Scribe_References.Look(ref EncounterMap,"encounterMap");
            Scribe_Values.Look(ref DeedId,"deedId");Scribe_Values.Look(ref Warned,"warned");Scribe_Values.Look(ref Closed,"closed",-1);Scribe_Values.Look(ref Outcome,"outcome","warning");
        }
    }
    public partial class GameComponent_Renown
    {
        public List<CaravanWarning> CaravanWarnings=new List<CaravanWarning>();
        public string CaravanThreatDetails(Pawn focus)
        {
            int now=Find.TickManager.TicksGame;
            return string.Join("\n",CaravanWarnings.Where(w=>focus==null || w.Target==focus).Select(w=>
            {
                string state=w.Outcome;
                if(w.Closed<0){int elapsed=now-w.Warned;state=elapsed<2*Policy.Day?"warning: "+(System.Math.Max(0,2*Policy.Day-elapsed)/(float)Policy.Day).ToString("0.0")+" days until dispatch window":elapsed<3*Policy.Day?"dispatch window open":"dispatch window expired; awaiting refresh";}
                return (w.Target?.LabelShort??"unavailable pawn")+" | "+(w.Faction?.Name??"unavailable faction")+" | "+state+(w.Closed<0?". Bring the pawn onto a map, resolve hostility or disable caravan threats to cancel. Lost qualifying evidence also cancels.":". Dispatch history, not current attacker or pawn status.");
            }));
        }
        public bool CaravanEvidenceEligible(FactionKnowledge report)
        {
            var pawn=report?.Maker;var caravan=pawn?.GetCaravan();var faction=report?.Faction;
            if(pawn==null || pawn.Dead || !pawn.RaceProps.Humanlike || pawn.DevelopmentalStage.Juvenile() || pawn.Faction!=Faction.OfPlayer || pawn.IsPrisoner || pawn.IsSlave || caravan==null || !caravan.Spawned || caravan.Faction!=Faction.OfPlayer || !caravan.PawnsListForReading.Contains(pawn) || Fame(pawn)<60)return false;
            int now=Find.TickManager.TicksGame;
            return faction!=null && !faction.IsPlayer && !faction.Hidden && !faction.defeated && !faction.deactivated && faction.def.humanlikeFaction && faction.HostileTo(Faction.OfPlayer) && Policy.Fresh(report.Tick,now) && Deeds.Any(d=>d.Id==report.DeedId && d.Maker==pawn && Policy.Fresh(d.Tick,now)) && faction.def.pawnGroupMakers?.Any(g=>g.kindDef==PawnGroupKindDefOf.Combat)==true;
        }
        public CaravanWarning TryWarnCaravan(bool enabled)
        {
            int now=Find.TickManager.TicksGame;
            if(!enabled || now<14*Policy.Day || lastThreatWarning>=0 && now-lastThreatWarning<45*Policy.Day || TargetedThreats.Any(t=>t.Closed<0) || CaravanWarnings.Any(t=>t.Closed<0))return null;
            var report=Abroad.Where(CaravanEvidenceEligible).OrderByDescending(r=>Fame(r.Maker)).ThenBy(r=>r.Maker.thingIDNumber).ThenBy(r=>r.Faction.loadID).FirstOrDefault();
            if(report==null)return null;
            var warning=new CaravanWarning{Target=report.Maker,Faction=report.Faction,DeedId=report.DeedId,Warned=now};
            lastThreatWarning=now;CaravanWarnings.Add(warning);while(CaravanWarnings.Count>16)CaravanWarnings.RemoveAt(0);
            Find.LetterStack.ReceiveLetter("Renown: caravan warning",report.Faction.Name+" is seeking "+report.Maker.LabelShort+" on the road. There are two days to prepare. Bringing this pawn onto a map, ending hostility, losing qualifying evidence or disabling caravan threats cancels the pending encounter. Splitting or merging caravans does not hide the named pawn. This spends the shared forty-five-day threat budget even if cancelled. No automatic surrender or kidnapping.",LetterDefOf.ThreatBig,report.Maker.GetCaravan());
            return warning;
        }
        public bool CaravanWarningReady(CaravanWarning warning,bool enabled)
        {
            if(!enabled || warning==null || warning.Closed>=0 || warning.Outcome!="warning")return false;
            int elapsed=Find.TickManager.TicksGame-warning.Warned;
            return elapsed>=2*Policy.Day && elapsed<3*Policy.Day && Abroad.Any(r=>r.Maker==warning.Target && r.Faction==warning.Faction && r.DeedId==warning.DeedId && CaravanEvidenceEligible(r));
        }
        public void RefreshCaravanWarnings(bool enabled)
        {
            int now=Find.TickManager.TicksGame;
            foreach(var warning in CaravanWarnings.Where(w=>w.Closed<0))
            {
                string reason=!enabled?"disabled":!Abroad.Any(r=>r.Maker==warning.Target && r.Faction==warning.Faction && r.DeedId==warning.DeedId && CaravanEvidenceEligible(r))?"target or evidence changed":now-warning.Warned>=3*Policy.Day?"dispatch window expired":"";
                if(reason.Length>0){warning.Closed=now;warning.Outcome=reason;}
            }
        }
        public bool TryDispatchCaravanWarning(CaravanWarning warning,bool enabled)
        {
            if(!CaravanWarningReady(warning,enabled))return false;
            warning.Closed=Find.TickManager.TicksGame;warning.Outcome="dispatch refused";
            var def=DefDatabase<IncidentDef>.GetNamedSilentFail("RN_CaravanThreat");
            var worker=def?.Worker as CaravanThreatWorker;
            if(worker==null){warning.Outcome="incident unavailable";return false;}
            var parms=new IncidentParms{target=warning.Target.GetCaravan(),faction=warning.Faction,points=350,attackTargets=new List<Thing>{warning.Target}};
            return worker.QueueWarning(warning,parms);
        }
    }
}
