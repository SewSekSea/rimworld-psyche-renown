using System.Collections.Generic;
using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Renown
{
    public class TargetedRaidStrategy : RaidStrategyWorker_ImmediateAttack
    {
        public override float SelectionWeight(Map map,float points)=>0f;
        public override List<Pawn> SpawnThreats(IncidentParms parms)
        {
            if(float.IsNaN(parms.points) || float.IsInfinity(parms.points) || parms.points<=0 || MinimumPoints(parms.faction,PawnGroupKindDefOf.Combat)*1.05f>350f)throw new TargetedBudgetException();
            parms.points=Math.Min(350f,parms.points);
            return base.SpawnThreats(parms);
        }
        protected override LordJob MakeLordJob(IncidentParms parms,Map map,List<Pawn> pawns,int groupID=0)
        {
            var target=parms.attackTargets?.OfType<Pawn>().SingleOrDefault();
            return new TargetedAssaultJob(parms.faction,target);
        }
    }
    public sealed class TargetedBudgetException : Exception {}
    public sealed class TargetedAssaultJob : LordJob_AssaultThings
    {
        Pawn target;
        public TargetedAssaultJob(){}
        public TargetedAssaultJob(Faction faction,Pawn target):base(faction,target==null?new List<Thing>():new List<Thing>{target}){this.target=target;}
        public override StateGraph CreateGraph()
        {
            if(target==null){var empty=new StateGraph();empty.AddToil(new LordToil_ExitMapAndDefendSelf());return empty;}
            var graph=base.CreateGraph();
            var exit=graph.lordToils.OfType<LordToil_ExitMapAndDefendSelf>().Single();
            var retreat=new Transition(graph.StartingToil,exit);
            retreat.AddTrigger(new Trigger_TicksPassed(17999));retreat.AddTrigger(new Trigger_Memo("RN_TargetGone"));graph.AddTransition(retreat);
            return graph;
        }
        public override void LordJobTick()
        {
            base.LordJobTick();
            if(target==null || target.Dead || !target.Spawned || target.Map!=Map || !target.IsFreeColonist || lord.faction==null || !lord.faction.HostileTo(Faction.OfPlayer))lord.ReceiveMemo("RN_TargetGone");
        }
        public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref target,"renownTarget");}
    }
    public sealed class TargetedThreat : IExposable
    {
        public Pawn Target;
        public Faction Faction;
        public Map Map;
        public int DeedId,Warned,Closed=-1;
        public bool Bounty;
        public int BountySilver;
        public string Outcome="warning";
        public void ExposeData()
        {
            Scribe_Values.Look(ref Bounty,"bounty",false);Scribe_Values.Look(ref BountySilver,"bountySilver",0);
            Scribe_References.Look(ref Target,"target");Scribe_References.Look(ref Faction,"faction");Scribe_References.Look(ref Map,"map");
            Scribe_Values.Look(ref DeedId,"deedId");Scribe_Values.Look(ref Warned,"warned");Scribe_Values.Look(ref Closed,"closed",-1);Scribe_Values.Look(ref Outcome,"outcome","warning");
        }
    }
    public partial class GameComponent_Renown
    {
        public List<TargetedThreat> TargetedThreats=new List<TargetedThreat>();
        int lastThreatWarning=-1;
        public int LastThreatWarning=>lastThreatWarning;
        public string TargetedThreatDetails(Map map,Pawn focus)
        {
            int now=Find.TickManager.TicksGame;
            return string.Join("\n",TargetedThreats.Where(t=>t.Map==map && (focus==null || t.Target==focus)).Select(t=>
            {
                string state=t.Outcome;
                if(t.Closed<0)
                {
                    int elapsed=now-t.Warned;
                    state=elapsed<2*Policy.Day?"warning: "+(Math.Max(0,2*Policy.Day-elapsed)/(float)Policy.Day).ToString("0.0")+" days until dispatch window":
                        elapsed<3*Policy.Day?"dispatch window: "+((3*Policy.Day-elapsed)/(float)Policy.Day).ToString("0.0")+" days remaining":"dispatch window expired; awaiting refresh";
                }
                return (t.Target?.LabelShort??"unavailable pawn")+" | "+(t.Faction?.Name??"unavailable faction")+" | "+(t.Bounty?"enemy bounty offer "+t.BountySilver+" silver | ":"")+state+
                    (t.Closed<0?". Move the target off this home map, end hostility, or disable "+(t.Bounty?"bounty hunts":"targeted attacks")+" to cancel. Changed qualifying evidence also cancels.":t.Outcome=="dispatched"?". Dispatch record, not current attacker status. "+(t.Bounty?"Hunting ends after 18,000 ticks, target loss or peace. A carried target must be released before retreat; failed release is retried while waiting.":"Native attackers retreat after 18,000 ticks or loss of their target.")+" They may defend themselves while leaving.":"")+(t.Bounty?" Hunters may carry this pawn away if downed. The offer is not a player payment or guaranteed capture. Captured pawns enter native captivity; a ransom or rescue is not guaranteed. Release does not prevent further combat.":"");
            }));
        }
        public bool ThreatEvidenceEligible(FactionKnowledge report,Map map)
        {
            if(report==null || map==null || !map.IsPlayerHome || !Person(report.Maker) || !report.Maker.IsFreeColonist || report.Maker.Map!=map || Fame(report.Maker)<60)return false;
            var faction=report.Faction;int now=Find.TickManager.TicksGame;
            return faction!=null && !faction.IsPlayer && !faction.Hidden && !faction.defeated && !faction.deactivated && faction.def.humanlikeFaction && faction.HostileTo(Faction.OfPlayer) &&
                Policy.Fresh(report.Tick,now) && Deeds.Any(d=>d.Id==report.DeedId && d.Maker==report.Maker && Policy.Fresh(d.Tick,now)) &&
                faction.def.pawnGroupMakers?.Any(g=>g.kindDef==PawnGroupKindDefOf.Combat)==true;
        }
        public TargetedThreat TryWarnTargetedThreat(Map map,bool enabled,bool bounty=false)
        {
            int now=Find.TickManager.TicksGame;
            if(!enabled || now<14*Policy.Day || lastThreatWarning>=0 && now-lastThreatWarning<45*Policy.Day || TargetedThreats.Any(t=>t.Closed<0) || CaravanWarnings.Any(t=>t.Closed<0))return null;
            var report=Abroad.Where(r=>ThreatEvidenceEligible(r,map)).OrderByDescending(r=>Fame(r.Maker)).ThenBy(r=>r.Maker.thingIDNumber).ThenBy(r=>r.Faction.loadID).FirstOrDefault();
            if(report==null)return null;
            lastThreatWarning=now;
            var warning=new TargetedThreat{Target=report.Maker,Faction=report.Faction,Map=map,DeedId=report.DeedId,Warned=now,Bounty=bounty,BountySilver=bounty?Math.Min(2000,Fame(report.Maker)*20):0};TargetedThreats.Add(warning);
            while(TargetedThreats.Count>16)TargetedThreats.RemoveAt(0);
            string intent=bounty?" offers "+warning.BountySilver+" silver for the capture of "+report.Maker.LabelShort+" and is preparing a bounty hunt. This is an enemy offer, not a player payment. Hunters may fight and carry the named pawn away if downed; capture is not guaranteed.":" has a sourced account of "+report.Maker.LabelShort+" and is preparing a targeted attack.";
            Find.LetterStack.ReceiveLetter(bounty?"Renown: bounty warning":"Renown: targeted threat",report.Faction.Name+intent+" There are two days to prepare. Moving the target away from this home map, ending hostility, losing the qualifying evidence or disabling "+(bounty?"bounty hunts":"targeted attacks")+" cancels dispatch. A warning spends the shared forty-five-day budget even if cancelled. No instant harm or forced surrender.",LetterDefOf.ThreatBig,report.Maker);
            return warning;
        }
        public bool TargetedThreatReady(TargetedThreat threat,bool enabled)
        {
            if(!enabled || threat==null || threat.Closed>=0 || threat.Outcome!="warning")return false;
            int elapsed=Find.TickManager.TicksGame-threat.Warned;
            return elapsed>=2*Policy.Day && elapsed<3*Policy.Day && Abroad.Any(r=>r.Maker==threat.Target && r.Faction==threat.Faction && r.DeedId==threat.DeedId && ThreatEvidenceEligible(r,threat.Map));
        }
        public bool TryDispatchTargetedThreat(TargetedThreat threat,bool enabled)
        {
            if(!TargetedThreatReady(threat,enabled) || GenHostility.AnyHostileActiveThreatToPlayer(threat.Map))return false;
            var strategy=DefDatabase<RaidStrategyDef>.GetNamedSilentFail(threat.Bounty?"RN_BountyAttack":"RN_TargetedAttack");if(strategy==null)return false;
            threat.Closed=Find.TickManager.TicksGame;threat.Outcome="dispatch failed";
            try
            {
            var worker=IncidentDefOf.RaidEnemy.Worker;
            var parms=StorytellerUtility.DefaultParmsNow(IncidentDefOf.RaidEnemy.category,threat.Map);
            parms.faction=threat.Faction;parms.points=Math.Min(350f,Math.Max(100f,parms.points*0.35f));parms.forced=false;
            parms.raidStrategy=strategy;parms.raidArrivalMode=PawnsArrivalModeDefOf.EdgeWalkIn;parms.attackTargets=new List<Thing>{threat.Target};
            parms.canKidnap=false;parms.canSteal=false;parms.dontUseSingleUseRocketLaunchers=true;
            parms.customLetterLabel=threat.Bounty?"Renown: bounty hunters":"Renown: targeted attack";parms.customLetterText=threat.Faction.Name+" has sent attackers after "+threat.Target.LabelShort+". Their targeted assault is limited to 18,000 ticks before retreat. They can still defend themselves while leaving."+(threat.Bounty?" The enemy's "+threat.BountySilver+" silver offer is for this pawn's capture. Hunters can physically carry the named pawn away if downed. Other downed pawns are not bounty targets.":"");
                if(!worker.CanFireNow(parms) || !worker.TryExecute(parms))return false;
                threat.Outcome="dispatched";return true;
            }
            catch(TargetedBudgetException){threat.Outcome="faction exceeds attack budget";return false;}
            catch(Exception e){Log.Warning("[Renown] Targeted dispatch interrupted: "+e.GetType().Name);threat.Outcome="dispatch interrupted";return false;}
        }
        public void RefreshTargetedWarnings(bool enabled,bool bounties=false)
        {
            int now=Find.TickManager.TicksGame;
            foreach(var threat in TargetedThreats.Where(t=>t.Closed<0))
            {
                string reason=!(threat.Bounty?bounties:enabled)?"disabled":!Abroad.Any(r=>r.Maker==threat.Target && r.Faction==threat.Faction && r.DeedId==threat.DeedId && ThreatEvidenceEligible(r,threat.Map))?"target or evidence changed":now-threat.Warned>=3*Policy.Day?"dispatch window expired":"";
                if(reason!=""){threat.Outcome=reason;threat.Closed=now;}
            }
        }
        void ExposeTargetedThreats()
        {
            Scribe_Values.Look(ref lastThreatWarning,"renownLastThreatWarning",-1);
            Scribe_Collections.Look(ref TargetedThreats,"renownTargetedThreats",LookMode.Deep);
            Scribe_Collections.Look(ref CaravanWarnings,"renownCaravanWarnings",LookMode.Deep);
            if(Scribe.mode==LoadSaveMode.PostLoadInit && CaravanWarnings==null)CaravanWarnings=new List<CaravanWarning>();
            if(Scribe.mode==LoadSaveMode.PostLoadInit && TargetedThreats==null)TargetedThreats=new List<TargetedThreat>();
        }
        void RefreshTargetedThreats()
        {
            bool enabled=RenownMod.Settings?.TargetedAttacks==true;
            bool bounties=RenownMod.Settings?.BountyHunts==true;
            bool caravans=RenownMod.Settings?.CaravanThreats==true;
            RefreshTargetedWarnings(enabled,bounties);RefreshCaravanWarnings(caravans);
            foreach(var warning in CaravanWarnings.Where(w=>w.Closed<0).ToArray())TryDispatchCaravanWarning(warning,caravans);
            if(!enabled && !bounties && !caravans)return;
            foreach(var threat in TargetedThreats.Where(t=>t.Closed<0).ToArray())TryDispatchTargetedThreat(threat,true);
            bool bounty=bounties && (!enabled || TargetedThreats.LastOrDefault()?.Bounty!=true);
            bool caravanFirst=(CaravanWarnings.LastOrDefault()?.Warned??-1)<=(TargetedThreats.LastOrDefault()?.Warned??-1);
            if(caravanFirst)TryWarnCaravan(caravans);
            if(enabled || bounties)foreach(var map in Find.Maps.Where(m=>m.IsPlayerHome))if(TryWarnTargetedThreat(map,true,bounty)!=null)break;
            if(!caravanFirst)TryWarnCaravan(caravans);
        }
    }
}
