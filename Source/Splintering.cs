using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Renown
{
    public sealed class CrewFracture : IExposable
    {
        public Pawn Leader;
        public List<Pawn> Members=new List<Pawn>(),Dissenters=new List<Pawn>();
        public string Kind,Outcome="warned";
        public int Warned,Closed=-1;
        public void ExposeData()
        {
            Scribe_References.Look(ref Leader,"leader");Scribe_Collections.Look(ref Members,"members",LookMode.Reference);Scribe_Collections.Look(ref Dissenters,"dissenters",LookMode.Reference);
            Scribe_Values.Look(ref Kind,"kind");Scribe_Values.Look(ref Outcome,"outcome","warned");Scribe_Values.Look(ref Warned,"warned");Scribe_Values.Look(ref Closed,"closed",-1);
            if(Scribe.mode==LoadSaveMode.PostLoadInit){Members=Members??new List<Pawn>();Dissenters=Dissenters??new List<Pawn>();}
        }
    }
    public partial class GameComponent_Renown
    {
        public List<CrewFracture> Fractures=new List<CrewFracture>();
        int lastFractureWarning=-1;
        public int LastFractureWarning=>lastFractureWarning;
        public bool FractureValid(CrewFracture f)
        {
            if(f==null || f.Closed>=0 || RenownMod.Settings?.CrewSplintering!=true || !RenownMod.Settings.CrewProtests || f.Leader==null || f.Members.Count<6 || f.Dissenters.Count!=3)return false;
            var current=CrewContext.Members(f.Leader);float need=RequestNeeds.Average(f.Members,f.Kind);
            return current.Length==f.Members.Count && !current.Except(f.Members).Any() && need>=0 && need<.6f && f.Dissenters.All(p=>p!=null && f.Members.Contains(p) && p!=f.Leader && p.relations.OpinionOf(f.Leader)<50);
        }
        public bool OpenFracture(CrewRequest request)
        {
            int now=Find.TickManager.TicksGame;
            if(RenownMod.Settings?.CrewSplintering!=true || !RenownMod.Settings.CrewProtests || request==null || !request.SplinterAnnounced || request.ProtestOutcome!="time limit reached" || request.ProtestClosed<0 || now<request.ProtestClosed || now-request.ProtestClosed>=Policy.Day || !ProtestGroupValid(request) || request.Members.Count<6 || Fractures.Any(f=>f.Closed<0) || lastFractureWarning>=0 && now-lastFractureWarning<30*Policy.Day)return false;
            var dissenters=request.Members.Where(p=>p!=request.Leader && p.relations.OpinionOf(request.Leader)<50).OrderBy(p=>p.relations.OpinionOf(request.Leader)).ThenBy(p=>p.thingIDNumber).Take(3).ToList();
            var fracture=new CrewFracture{Leader=request.Leader,Members=request.Members.ToList(),Dissenters=dissenters,Kind=request.Kind,Warned=now};
            if(!FractureValid(fracture))return false;
            Fractures.Add(fracture);lastFractureWarning=now;
            string text=request.Leader.LabelShort+"'s crew risks a split in two days over unresolved "+request.Kind+". Dissenters: "+string.Join(", ",dissenters.Select(p=>p.LabelShort))+".";
            Find.LetterStack.ReceiveLetter("Renown: crew fracture warning",text+" Restore average needs to 60%, rebuild any dissenter's opinion of the leader to 50, or disable splintering/protests. Otherwise cross-group opinion falls 12 for five days. Crew determines any resulting split; nobody leaves the colony or changes faction.",LetterDefOf.NegativeEvent,request.Leader);
            DisplayBridge.Record(text,"Separately enabled consequence, announced before the protest. Two-day grace, one warning per thirty days. Changed membership also cancels. No assigned leader or forced membership.",request.Leader,null,request.Leader.Map);return true;
        }
        public void RefreshFractures()
        {
            int now=Find.TickManager.TicksGame;
            foreach(var f in Fractures.Where(f=>f.Closed<0))
            {
                if(!FractureValid(f)){f.Closed=now;f.Outcome="cancelled";continue;}
                if(now-f.Warned<2*Policy.Day)continue;
                var loyal=f.Members.Except(f.Dissenters).ToArray();var thought=DefDatabase<ThoughtDef>.GetNamed("RN_CrewFracture");
                foreach(var dissenter in f.Dissenters)foreach(var supporter in loyal)
                {
                    dissenter.needs.mood.thoughts.memories.TryGainMemory(thought,supporter);
                    supporter.needs.mood.thoughts.memories.TryGainMemory(thought,dissenter);
                }
                f.Closed=now;f.Outcome="relationship fracture";
                DisplayBridge.Record(f.Leader.LabelShort+"'s crew fractured over unresolved "+f.Kind+".","Dissenters and remaining members received mutual -12 opinion for five days. Crew alone recalculates groups from these relationships. Strong bonds may keep the group together. No pawn changed faction or was ordered to leave.",f.Leader,null,f.Leader.Map);
            }
            Fractures.RemoveAll(f=>f.Closed>=0 && now-f.Closed>=14*Policy.Day);
            if(Fractures.Count>16)Fractures.RemoveRange(0,Fractures.Count-16);
            foreach(var request in Requests)if(OpenFracture(request))break;
        }
        public string FractureDetails(Pawn focus)=>string.Join("\n",Fractures.Where(f=>f.Members.Contains(focus)).Select(f=>(f.Leader?.LabelShort??"missing leader")+": "+(f.Closed>=0?f.Outcome:"warning, "+(System.Math.Max(0,2*Policy.Day-(Find.TickManager.TicksGame-f.Warned))/(float)Policy.Day).ToString("0.0")+" days remaining")+". Restore average "+f.Kind+" to 60%, improve any dissenter's opinion of the leader to 50, or disable splintering/protests. Cross-group -12 opinion lasts five days; no forced membership."));
        void ExposeFractures()
        {
            Scribe_Values.Look(ref lastFractureWarning,"renownLastFractureWarning",-1);Scribe_Collections.Look(ref Fractures,"renownFractures",LookMode.Deep);
            if(Scribe.mode==LoadSaveMode.PostLoadInit)Fractures=(Fractures??new List<CrewFracture>()).Where(f=>f!=null).Take(16).ToList();
        }
    }
}
