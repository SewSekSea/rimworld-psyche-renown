using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Renown
{
    public sealed class SabotagePlot : IExposable
    {
        public Pawn Leader,Actor;
        public List<Pawn> Members=new List<Pawn>();
        public Thing Target;
        public string Kind,Outcome="warned";
        public int Warned,Closed=-1;
        public void ExposeData()
        {
            Scribe_References.Look(ref Leader,"leader");Scribe_References.Look(ref Actor,"actor");Scribe_References.Look(ref Target,"target");
            Scribe_Collections.Look(ref Members,"members",LookMode.Reference);Scribe_Values.Look(ref Kind,"kind");Scribe_Values.Look(ref Outcome,"outcome","warned");Scribe_Values.Look(ref Warned,"warned");Scribe_Values.Look(ref Closed,"closed",-1);
            if(Scribe.mode==LoadSaveMode.PostLoadInit)Members=Members??new List<Pawn>();
        }
    }
    public partial class GameComponent_Renown
    {
        public List<SabotagePlot> SabotagePlots=new List<SabotagePlot>();
        int lastSabotageWarning=-1;
        public int LastSabotageWarning=>lastSabotageWarning;
        public static bool SabotageTarget(Thing t)=>t!=null && t.Spawned && t.Faction==Faction.OfPlayer && t.TryGetComp<CompFlickable>()?.SwitchIsOn!=false &&
            (t.def.defName=="ElectricTailoringBench" || t.def.defName=="ElectricSmithy" || t.def.defName=="TableMachining") && t.TryGetComp<CompBreakdownable>()!=null && !t.TryGetComp<CompBreakdownable>().BrokenDown;
        public bool SabotageGrievance(SabotagePlot p)
        {
            if(p==null || RenownMod.Settings?.CrewSabotage!=true || RenownMod.Settings.CrewProtests!=true || p.Leader==null || p.Closed>=0)return false;
            var current=CrewContext.Members(p.Leader);
            float need=RequestNeeds.Average(p.Members,p.Kind);
            return current.Length>=3 && current.Length==p.Members.Count && !current.Except(p.Members).Any() && need>=0 && need<.6f;
        }
        public bool OpenSabotage(CrewRequest request)
        {
            int now=Find.TickManager.TicksGame;
            if(RenownMod.Settings?.CrewSabotage!=true || RenownMod.Settings.CrewProtests!=true || request==null || !request.SabotageAnnounced || request.ProtestOutcome!="time limit reached" || request.ProtestClosed<0 || now<request.ProtestClosed || now-request.ProtestClosed>=Policy.Day || !ProtestGroupValid(request) || RequestNeeds.Average(request.Members,request.Kind)>=.6f || lastSabotageWarning>=0 && now-lastSabotageWarning<30*Policy.Day || SabotagePlots.Any(p=>p.Closed<0))return false;
            var target=request.Leader.Map.listerBuildings.allBuildingsColonist.Where(SabotageTarget).OrderBy(t=>t.thingIDNumber).FirstOrDefault();if(target==null)return false;
            lastSabotageWarning=now;
            var plot=new SabotagePlot{Leader=request.Leader,Members=request.Members.ToList(),Kind=request.Kind,Target=target,Warned=now};SabotagePlots.Add(plot);
            string text=request.Leader.LabelShort+"'s crew threatens to disable "+target.LabelShort+" after one day unless average "+request.Kind+" reaches 60%.";
            Messages.Message("Renown: "+text,target,MessageTypeDefOf.ThreatSmall);
            DisplayBridge.Record(text,"Separate opt-in escalation after an unresolved protest. One repairable production-bench breakdown at most, with a one-day action window after the warning. Needs improvement, changed crew, removed target or disabling sabotage/protests cancels it. No essential systems, damage or fire. Shared thirty-day warning budget.",request.Leader,null,target.Map);return true;
        }
        public void RefreshSabotage()
        {
            int now=Find.TickManager.TicksGame;
            foreach(var plot in SabotagePlots.Where(p=>p.Closed<0))
                if(!SabotageGrievance(plot) || !SabotageTarget(plot.Target) || plot.Target.Map!=plot.Leader.Map || now-plot.Warned>=2*Policy.Day)
                {plot.Closed=now;plot.Outcome=now-plot.Warned>=2*Policy.Day?"window expired":"cancelled";DisplayBridge.Record("Crew sabotage threat ended: "+plot.Outcome+".","No breakdown committed by this threat.",plot.Leader,null,plot.Leader?.Map);}
            SabotagePlots.RemoveAll(p=>p.Closed>=0 && now-p.Closed>=14*Policy.Day);
            if(SabotagePlots.Count>32)SabotagePlots.RemoveRange(0,SabotagePlots.Count-32);
            foreach(var request in Requests)if(OpenSabotage(request))break;
        }
        public SabotagePlot SabotageFor(Pawn pawn,Thing target=null)
        {
            if(!Attentive(pawn) || !pawn.IsFreeColonist || pawn.Drafted || pawn.InMentalState || pawn.CurJob?.playerForced==true)return null;
            int now=Find.TickManager.TicksGame;
            return SabotagePlots.FirstOrDefault(p=>SabotageGrievance(p) && p.Members.Contains(pawn) && (target==null || p.Target==target) && SabotageTarget(p.Target) && p.Target.Map==pawn.Map && now-p.Warned>=Policy.Day && now-p.Warned<2*Policy.Day && !p.Target.IsForbidden(pawn) && pawn.CanReach(p.Target,PathEndMode.Touch,Danger.None));
        }
        public bool CommitSabotage(Pawn pawn,Thing target)
        {
            var plot=SabotageFor(pawn,target);if(plot==null || !pawn.Position.AdjacentTo8WayOrInside(target))return false;
            plot.Closed=Find.TickManager.TicksGame;plot.Actor=pawn;plot.Outcome="bench disabled";
            target.TryGetComp<CompBreakdownable>().DoBreakdown();
            DisplayBridge.Record(pawn.LabelShort+" disabled "+target.LabelShort+" after the warned grievance remained unresolved.","One native repairable breakdown. No hit-point damage, fire or further sabotage from this threat. Native component repair restores operation.",pawn,plot.Leader,pawn.Map);return true;
        }
        void ExposeSabotage()
        {
            Scribe_Values.Look(ref lastSabotageWarning,"renownLastSabotageWarning",-1);Scribe_Collections.Look(ref SabotagePlots,"renownSabotagePlots",LookMode.Deep);
            if(Scribe.mode==LoadSaveMode.PostLoadInit)SabotagePlots=(SabotagePlots??new List<SabotagePlot>()).Where(p=>p!=null).Take(32).ToList();
        }
        public string SabotageDetails(Pawn focus)
        {
            int now=Find.TickManager.TicksGame;
            return string.Join("\n",SabotagePlots.Where(p=>p.Leader==focus || p.Members.Contains(focus)).Select(p=>
                (p.Target?.LabelShort??"missing bench")+": "+(p.Closed>=0?p.Outcome:now-p.Warned<Policy.Day?"warning, "+((Policy.Day-(now-p.Warned))/(float)Policy.Day).ToString("0.0")+" days remaining":"action window, "+(System.Math.Max(0,2*Policy.Day-(now-p.Warned))/(float)Policy.Day).ToString("0.0")+" days remaining")+
                ". Resolve average "+p.Kind+" to60%, switch off/remove the bench, or disable sabotage/protests. At most one repairable breakdown."));
        }
    }
    public sealed class JobGiver_Sabotage : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var plot=GameComponent_Renown.Current?.SabotageFor(pawn);return plot!=null && pawn.CanReserve(plot.Target)?JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("RN_SabotageBench"),plot.Target):null;
        }
    }
    public sealed class JobDriver_Sabotage : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetA,job,1,-1,null,errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(()=>GameComponent_Renown.Current?.SabotageFor(pawn,job.targetA.Thing)==null);
            yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.Touch);
            yield return Toils_General.Wait(300,TargetIndex.A);
            yield return Toils_General.Do(()=>GameComponent_Renown.Current?.CommitSabotage(pawn,job.targetA.Thing));
        }
    }
}
