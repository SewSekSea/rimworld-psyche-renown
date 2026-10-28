using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Renown
{
    public class SparringBout:IExposable
    {
        public int Id,Started,Seed,Elapsed,Rounds,ScoreA,ScoreB;
        public Pawn A,B,Winner;
        public bool Engaged,Ended;
        public string Outcome="assembling";
        public Dictionary<Pawn,int> Attendance=new Dictionary<Pawn,int>();
        List<Pawn> attendanceKeys;
        List<int> attendanceValues;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Id,"id");Scribe_Values.Look(ref Started,"started");Scribe_Values.Look(ref Seed,"seed");
            Scribe_Values.Look(ref Elapsed,"elapsed");Scribe_Values.Look(ref Rounds,"rounds");Scribe_Values.Look(ref ScoreA,"scoreA");Scribe_Values.Look(ref ScoreB,"scoreB");
            Scribe_References.Look(ref A,"a");Scribe_References.Look(ref B,"b");Scribe_References.Look(ref Winner,"winner");
            Scribe_Values.Look(ref Engaged,"engaged");Scribe_Values.Look(ref Ended,"ended");Scribe_Values.Look(ref Outcome,"outcome","assembling");
            Scribe_Collections.Look(ref Attendance,"attendance",LookMode.Reference,LookMode.Value,ref attendanceKeys,ref attendanceValues);
            if(Scribe.mode==LoadSaveMode.PostLoadInit)Attendance=Attendance??new Dictionary<Pawn,int>();
        }
    }
    public partial class GameComponent_Renown
    {
        public List<SparringBout> SparringBouts=new List<SparringBout>();
        int nextBout=1;
        public static bool SparParticipant(Pawn p)=>Attentive(p) && p.IsFreeColonist && !p.Drafted && p.ageTracker.AgeBiologicalYearsFloat>=18 && !p.WorkTagIsDisabled(WorkTags.Violent) && p.Map.IsPlayerHome;
        public bool CanSpar(Pawn a,Pawn b)
        {
            int now=Find.TickManager.TicksGame;
            return a!=b && SparParticipant(a) && SparParticipant(b) && a.Map==b.Map && !a.HostileTo(b) &&
                !SparringBouts.Any(v=>(!v.Ended || now-v.Started<7*Policy.Day) && (v.A==a || v.B==a || v.A==b || v.B==b)) &&
                SparringBouts.Count(v=>!v.Ended || now-v.Started<7*Policy.Day)<128 && a.CanReach(b,PathEndMode.Touch,Danger.None);
        }
        public bool StartSpar(Pawn a,Pawn b)
        {
            if(!CanSpar(a,b))return false;
            int now=Find.TickManager.TicksGame;
            SparringBouts.RemoveAll(v=>v.Ended && now-v.Started>=7*Policy.Day);
            var bout=new SparringBout{Id=nextBout++,A=a,B=b,Started=now,Seed=Rand.Int};SparringBouts.Add(bout);
            var first=JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("RN_Sparring"),b);first.count=bout.Id;
            var second=JobMaker.MakeJob(first.def,a);second.count=bout.Id;
            if(!b.jobs.TryTakeOrderedJob(second) || !a.jobs.TryTakeOrderedJob(first)){EndSpar(bout,false,"orders unavailable");return false;}
            DisplayBridge.Record(a.LabelShort+" and "+b.LabelShort+" are preparing to spar.","Player-issued orders accepted. No victory or reputation credit until a completed, witnessed contest.",a,b,a.Map);
            return true;
        }
        public SparringBout FindSpar(int id)=>SparringBouts.FirstOrDefault(v=>v.Id==id);
        static bool MatchesSpar(Pawn p,SparringBout bout)=>p?.CurJob?.def.defName=="RN_Sparring" && p.CurJob.count==bout.Id && p.CurJob.targetA.Pawn==(p==bout.A?bout.B:bout.A);
        void RefreshSparring()
        {
            foreach(var bout in SparringBouts)
            {
                if(bout.Ended)continue;
                if(!SparParticipant(bout.A) || !SparParticipant(bout.B) || bout.A.Map!=bout.B.Map || bout.A.HostileTo(bout.B)){EndSpar(bout,false,"participant unavailable");continue;}
                bool jobs=MatchesSpar(bout.A,bout) && MatchesSpar(bout.B,bout);
                bool ready=(bout.A.jobs.curDriver as JobDriver_Sparring)?.Ready==true && (bout.B.jobs.curDriver as JobDriver_Sparring)?.Ready==true;
                bool adjacent=bout.A.Position.AdjacentTo8WayOrInside(bout.B.Position) && GenSight.LineOfSight(bout.A.Position,bout.B.Position,bout.A.Map);
                if(!jobs || !ready || !adjacent)
                {
                    if(bout.Engaged || Find.TickManager.TicksGame-bout.Started>2500)EndSpar(bout,false,"interrupted or unable to assemble");
                    continue;
                }
                bout.Engaged=true;bout.Outcome="sparring";bout.Elapsed++;
                if(bout.Elapsed%30==0)
                    foreach(var observer in bout.A.Map.mapPawns.AllPawnsSpawned.Where(p=>(Sees(p,bout.A) || Sees(p,bout.B)) && !p.HostileTo(bout.A) && !p.HostileTo(bout.B)).Take(256))
                    {
                        if(!bout.Attendance.ContainsKey(observer) && bout.Attendance.Count>=256)continue;
                        bout.Attendance.TryGetValue(observer,out int time);bout.Attendance[observer]=time+30;
                    }
                if(bout.Elapsed%300==0 && bout.Rounds<5)
                {
                    float a=1+(bout.A.skills?.GetSkill(SkillDefOf.Melee)?.Level??0),b=1+(bout.B.skills?.GetSkill(SkillDefOf.Melee)?.Level??0);
                    Rand.PushState(bout.Seed+bout.Rounds);
                    bool aWins;
                    try{aWins=Rand.Chance(a/(a+b));if(aWins)bout.ScoreA++;else bout.ScoreB++;}
                    finally{Rand.PopState();}
                    (aWins?bout.A:bout.B).Drawer?.Notify_MeleeAttackOn(aWins?bout.B:bout.A);
                    bout.Rounds++;
                }
                if(bout.Rounds==5)EndSpar(bout,true,"completed");
            }
        }
        void EndSpar(SparringBout bout,bool completed,string outcome)
        {
            if(bout.Ended)return;
            bout.Ended=true;bout.Outcome=outcome;
            if(completed)
            {
                bout.Winner=bout.ScoreA>bout.ScoreB?bout.A:bout.B;
                var loser=bout.Winner==bout.A?bout.B:bout.A;
                var witnesses=bout.Attendance.Where(p=>p.Value>=750 && Sees(p.Key,bout.Winner) && !p.Key.HostileTo(bout.Winner)).Select(p=>p.Key).ToList();
                if(witnesses.Count>0)
                {
                    int now=Find.TickManager.TicksGame;
                    var deed=new Deed{Id=nextId++,Kind="duel",Maker=bout.Winner,Subject=loser,Map=bout.Winner.Map,Tick=now,ProductId="spar:"+bout.Id,ProductLabel="a friendly sparring victory",Witnesses=witnesses};Deeds.Add(deed);
                    foreach(var observer in witnesses)
                    {
                        var known=Known.FirstOrDefault(k=>k.Witness==observer && k.Maker==bout.Winner);
                        if(known==null)Known.Add(new Knowledge{Witness=observer,Maker=bout.Winner,DeedId=deed.Id,LearnedAt=now});
                        else{Archive(known);known.DeedId=deed.Id;known.Source=null;known.Hops=0;known.LearnedAt=now;}
                        DisplayBridge.Record(observer.LabelShort+" witnessed "+bout.Winner.LabelShort+" win a friendly sparring contest.","Five non-injuring rounds scored using Melee skill. Witness present for at least half and at the finish. This is exhibition skill, not a combat kill or proof of battlefield success.",observer,bout.Winner,bout.Winner.Map);
                    }
                    Prune();
                }
                Messages.Message(bout.Winner.LabelShort+" won the friendly sparring contest ("+System.Math.Max(bout.ScoreA,bout.ScoreB)+"-"+System.Math.Min(bout.ScoreA,bout.ScoreB)+").",bout.Winner,MessageTypeDefOf.PositiveEvent);
            }
            else DisplayBridge.Record("Friendly sparring cancelled.","Reason: "+outcome+". No victory deed was awarded. The attempt cooldown remains in effect.",bout.A,bout.B,bout.A?.Map);
            foreach(var pawn in new[]{bout.A,bout.B})
            {
                if(pawn?.jobs==null)continue;
                foreach(var queued in pawn.jobs.jobQueue.Where(q=>q.job.def.defName=="RN_Sparring" && q.job.count==bout.Id).ToArray())pawn.jobs.jobQueue.Extract(queued.job)?.Cleanup(pawn,true);
                if(MatchesSpar(pawn,bout))pawn.jobs.EndCurrentJob(completed?JobCondition.Succeeded:JobCondition.InterruptForced);
            }
        }
        void ExposeSparring()
        {
            Scribe_Collections.Look(ref SparringBouts,"renownSparringBouts",LookMode.Deep);Scribe_Values.Look(ref nextBout,"renownNextBout",1);
            if(Scribe.mode==LoadSaveMode.PostLoadInit)SparringBouts=SparringBouts??new List<SparringBout>();
        }
    }
    public class JobDriver_Sparring:JobDriver
    {
        bool ready;
        public bool Ready=>ready;
        SparringBout Bout=>GameComponent_Renown.Current?.FindSpar(job.count);
        public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref ready,"renownSparringReady");}
        public override bool TryMakePreToilReservations(bool errorOnFailed)=>Bout!=null && !Bout.Ended;
        public override string GetReport()=>Bout==null?"Sparring.":"Friendly sparring: "+Bout.ScoreA+"-"+Bout.ScoreB+" ("+Bout.Rounds+"/5 rounds).";
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(()=>Bout==null || Bout.Ended || !GameComponent_Renown.SparParticipant(pawn));
            if(Bout?.A==pawn)yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.Touch);
            var spar=ToilMaker.MakeToil("Sparring");spar.defaultCompleteMode=ToilCompleteMode.Never;
            spar.initAction=()=>{pawn.pather.StopDead();ready=true;};
            spar.tickAction=()=>{if(job.targetA.Pawn?.Spawned==true)pawn.rotationTracker.FaceTarget(job.targetA);};
            yield return spar;
        }
    }
    [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetGizmos))]
    public static class SparringGizmos
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Pawn __instance)
        {
            foreach(var gizmo in __result)yield return gizmo;
            if(!__instance.IsFreeColonist || !__instance.Spawned)yield break;
            var command=new Command_Action{icon=TexCommand.Draft,defaultLabel="Friendly spar",defaultDesc="Choose another adult colonist for a five-round, non-injuring exhibition scored using Melee skill. Both must be awake, undrafted and capable of violence at home. Orders interrupt both pawns' work. One attempt per participant every seven days. No DLC, damage or combat-kill credit.",action=()=>
            {
                if(__instance.Map==null)return;
                var ledger=GameComponent_Renown.Current;
                var options=__instance.Map.mapPawns.FreeColonistsSpawned.Where(p=>ledger?.CanSpar(__instance,p)==true).Select(p=>new FloatMenuOption(p.LabelShort,()=>ledger.StartSpar(__instance,p))).ToList();
                if(options.Count==0)options.Add(new FloatMenuOption("No eligible partner",null));Find.WindowStack.Add(new FloatMenu(options));
            }};
            if(!GameComponent_Renown.SparParticipant(__instance))command.Disable("Requires an awake, adult, undrafted colonist capable of violence on a home map.");
            yield return command;
        }
    }
}
