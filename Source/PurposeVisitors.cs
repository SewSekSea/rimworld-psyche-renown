using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Renown
{
    public sealed class PurposeVisit : IExposable
    {
        public string Kind,Outcome="active";
        public Pawn Subject;
        public Faction Faction;
        public Map Map;
        public List<Pawn> Guests=new List<Pawn>(),Returned=new List<Pawn>();
        public int Started,Closed=-1,LastInterview=-1,Collected;
        public Pawn JoinCandidate;
        public int JoinOffered=-1;
        public string JoinOutcome="none";
        public string NewsOutcome="none",NewsChanges="";
        public void ExposeData()
        {
            Scribe_Values.Look(ref Kind,"kind");Scribe_Values.Look(ref Outcome,"outcome","active");
            Scribe_References.Look(ref Subject,"subject");Scribe_References.Look(ref Faction,"faction");Scribe_References.Look(ref Map,"map");
            Scribe_Collections.Look(ref Guests,"guests",LookMode.Reference);Scribe_Collections.Look(ref Returned,"returned",LookMode.Reference);
            Scribe_Values.Look(ref Started,"started");Scribe_Values.Look(ref Closed,"closed",-1);Scribe_Values.Look(ref LastInterview,"lastInterview",-1);Scribe_Values.Look(ref Collected,"collected");
            Scribe_References.Look(ref JoinCandidate,"joinCandidate");Scribe_Values.Look(ref JoinOffered,"joinOffered",-1);Scribe_Values.Look(ref JoinOutcome,"joinOutcome","none");
            Scribe_Values.Look(ref NewsOutcome,"newsOutcome","none");Scribe_Values.Look(ref NewsChanges,"newsChanges","");
            if(Scribe.mode==LoadSaveMode.PostLoadInit){Guests=Guests??new List<Pawn>();Returned=Returned??new List<Pawn>();}
        }
    }
    public partial class GameComponent_Renown
    {
        public List<PurposeVisit> PurposeVisits=new List<PurposeVisit>();
        int lastPurposeAttempt=-1;
        int lastReporterNews=-1;
        public int LastReporterNews=>lastReporterNews;
        public int LastPurposeAttempt=>lastPurposeAttempt;
        bool PurposeEnabled(string kind)=>kind=="pilgrim"?RenownMod.Settings?.Pilgrims==true:kind=="reporter" && RenownMod.Settings?.Reporters==true;
        public PurposeVisit TrackPurposeVisit(string kind,Faction faction,Map map,Pawn subject,IEnumerable<Pawn> guests)
        {
            if(!PurposeEnabled(kind) || faction==null || faction.IsPlayer || faction.HostileTo(Faction.OfPlayer) || map==null || !map.IsPlayerHome || !Person(subject) || subject.Map!=map)return null;
            var party=guests?.Where(p=>Person(p) && p.Faction==faction && p.Map==map && !p.IsPrisoner).Distinct().ToList();
            if(party==null || party.Count==0 || PurposeVisits.Any(v=>v.Closed<0 && v.Guests.Intersect(party).Any()))return null;
            if(PurposeVisits.Count>=16){var old=PurposeVisits.FirstOrDefault(v=>v.Closed>=0);if(old==null)return null;PurposeVisits.Remove(old);}
            var visit=new PurposeVisit{Kind=kind,Faction=faction,Map=map,Subject=subject,Guests=party,Started=Find.TickManager.TicksGame};PurposeVisits.Add(visit);return visit;
        }
        public bool TryPurposeVisit(Map map,string kind)
        {
            int now=Find.TickManager.TicksGame;
            if(!PurposeEnabled(kind) || now<7*Policy.Day || lastPurposeAttempt>=0 && now-lastPurposeAttempt<30*Policy.Day || PurposeVisits.Any(v=>v.Closed<0))return false;
            var report=Abroad.FirstOrDefault(r=>DelegationEligible(r,map) && Fame(r.Maker)>=50);if(report==null)return false;
            lastPurposeAttempt=now;
            var incident=DefDatabase<IncidentDef>.GetNamedSilentFail("VisitorGroup");if(incident==null)return false;
            var parms=StorytellerUtility.DefaultParmsNow(incident.category,map);parms.faction=report.Faction;parms.points=100f;parms.forced=false;parms.storeGeneratedNeutralPawns=new List<Pawn>();
            if(!(incident.Worker is IncidentWorker_NeutralGroup worker) || !worker.FactionCanBeGroupSource(report.Faction,parms) || !worker.CanFireNow(parms) || !worker.TryExecute(parms))return false;
            var visit=TrackPurposeVisit(kind,report.Faction,map,report.Maker,parms.storeGeneratedNeutralPawns);if(visit==null)return false;
            TrackHosting(report.Faction,map,report.Maker,visit.Guests);
            DisplayBridge.Record(kind=="pilgrim"?"Pilgrims arrived seeking accounts of "+report.Maker.LabelShort+".":"Reporters arrived to collect local accounts.","Native peaceful visitors, not guaranteed recruits or admirers. In-person conversations require an actual knowledgeable source. Up to "+(kind=="pilgrim"?1:3)+" sourced accounts per party, one per game hour. Departing visitors use ordinary faction reporting. No invented deeds or automatic goodwill. Shared thirty-day visitor attempt limit.",report.Maker,null,map);
            return true;
        }
        PurposeVisit InterviewVisit(Pawn visitor,Pawn source)
        {
            if(source?.IsFreeColonist!=true || visitor==null || visitor.IsPrisoner || visitor.IsSlave || !Conversation(source,visitor))return null;
            int now=Find.TickManager.TicksGame;
            return PurposeVisits.FirstOrDefault(v=>v.Closed<0 && PurposeEnabled(v.Kind) && v.Guests.Contains(visitor) && !v.Returned.Contains(visitor) && visitor.Faction==v.Faction && visitor.Map==v.Map &&
                v.Collected<(v.Kind=="pilgrim"?1:3) && (v.LastInterview<0 || now-v.LastInterview>=2500) && StoryAbout(source,visitor,v.Kind=="pilgrim"?v.Subject:null)!=null);
        }
        public bool CanInterview(Pawn visitor,Pawn source)=>InterviewVisit(visitor,source)!=null;
        public bool Interview(Pawn visitor,Pawn source)
        {
            var visit=InterviewVisit(visitor,source);if(visit==null || !ShareAbout(source,visitor,visit.Kind=="pilgrim"?visit.Subject:null))return false;
            visit.Collected++;visit.LastInterview=Find.TickManager.TicksGame;
            DisplayBridge.Record(visitor.LabelShort+" collected a sourced account from "+source.LabelShort+".","A "+visit.Kind+" conversation, not direct witnessing. Existing opinion, proximity, evidence, transmission and repetition limits apply. Accounts can reach the visitor's own faction only through ordinary departure reporting.",visitor,source,visit.Map);return true;
        }
        public bool TryPurposeConversation()
        {
            var interaction=DefDatabase<InteractionDef>.GetNamedSilentFail("RN_Interview");if(interaction==null)return false;
            foreach(var visit in PurposeVisits.Where(v=>v.Closed<0 && v.Map!=null && PurposeEnabled(v.Kind)))
                foreach(var visitor in visit.Guests.Where(p=>Attentive(p) && p.Map==visit.Map && !visit.Returned.Contains(p)).Take(8))
                    foreach(var source in visit.Map.mapPawns.FreeColonistsSpawned.Where(p=>p.Position.DistanceTo(visitor.Position)<=6).OrderBy(p=>p.thingIDNumber).Take(16))
                        if(CanInterview(visitor,source) && visitor.interactions?.TryInteractWith(source,interaction)==true)return true;
            return false;
        }
        public void PurposeDeparture(Pawn pawn)
        {
            if(pawn==null || pawn.Dead || pawn.IsPrisoner)return;
            foreach(var visit in PurposeVisits.Where(v=>v.Closed<0 && v.Guests.Contains(pawn) && pawn.Faction==v.Faction))if(!visit.Returned.Contains(pawn))visit.Returned.Add(pawn);
            RefreshPurposeRecords();
            foreach(var visit in PurposeVisits.Where(v=>v.Kind=="reporter" && v.Returned.Contains(pawn)))TryPublishReporterNews(visit);
        }
        public void RefreshPurposeRecords()
        {
            int now=Find.TickManager.TicksGame;
            foreach(var v in PurposeVisits.Where(v=>v.Closed<0))
            {
                string end=!PurposeEnabled(v.Kind)?"tracking disabled":v.Faction==null || v.Faction.defeated || v.Faction.HostileTo(Faction.OfPlayer) || v.Map==null?"visit interrupted":now-v.Started>=7*Policy.Day?"observation expired":!v.Guests.Any(p=>Person(p) && p.Map==v.Map && !v.Returned.Contains(p))?"party departed":"";
                if(end!=""){v.Outcome=end;v.Closed=now;}
            }
        }
        void RefreshPurposeVisitors()
        {
            RefreshPurposeRecords();
            TryPurposeConversation();
            foreach(var visit in PurposeVisits)if(TryOfferPilgrimRecruit(visit))break;
            string kind=RenownMod.Settings?.Pilgrims==true && (RenownMod.Settings?.Reporters!=true || Rand.Bool)?"pilgrim":"reporter";
            foreach(var map in Find.Maps.Where(m=>m.IsPlayerHome))if(TryPurposeVisit(map,kind))break;
        }
        public string PurposeDetails(Map map)=>string.Join("\n",PurposeVisits.Where(v=>v.Map==map).Take(16).Select(v=>v.Kind+" | "+(v.Faction?.Name??"unavailable")+" | "+v.Outcome+" | sourced accounts "+v.Collected+" | returned "+v.Returned.Count+"/"+v.Guests.Count+(v.JoinOutcome!="none"?" | recruitment: "+v.JoinOutcome:"")+(v.NewsOutcome!="none"?" | bulletin: "+v.NewsOutcome+" | "+v.NewsChanges:"")));
        void ExposePurposeVisitors()
        {
            Scribe_Values.Look(ref lastPurposeAttempt,"renownLastPurposeAttempt",-1);Scribe_Collections.Look(ref PurposeVisits,"renownPurposeVisits",LookMode.Deep);
            Scribe_Values.Look(ref lastReporterNews,"renownLastReporterNews",-1);
            if(Scribe.mode==LoadSaveMode.PostLoadInit)PurposeVisits=(PurposeVisits??new List<PurposeVisit>()).Where(v=>v!=null).Take(16).ToList();
        }
    }
    public sealed class InteractionWorker_Interview : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator,Pawn recipient)=>GameComponent_Renown.Current?.CanInterview(initiator,recipient)==true?.6f:0;
        public override void Interacted(Pawn initiator,Pawn recipient,List<RulePackDef> packs,out string letterText,out string letterLabel,out LetterDef letterDef,out LookTargets lookTargets)
        {letterText=null;letterLabel=null;letterDef=null;lookTargets=null;GameComponent_Renown.Current?.Interview(initiator,recipient);}
    }
}
