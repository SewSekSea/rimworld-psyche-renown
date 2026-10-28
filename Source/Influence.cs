using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Renown
{
    public sealed class CrewRequest : IExposable
    {
        public Pawn Leader;
        public List<Pawn> Members=new List<Pawn>();
        public int Started,Closed=-1;
        public string Outcome="open";
        public string Kind="recreation";
        public bool Warned;
        public int Extension;
        public Pawn Mediator;
        public int ProtestWarnedAt=-1,ProtestStarted=-1,ProtestClosed=-1;
        public string ProtestOutcome="";
        public bool StrikeAnnounced;
        public bool SabotageAnnounced;
        public bool SplinterAnnounced;
        public void ExposeData()
        {
            Scribe_References.Look(ref Leader,"leader");Scribe_Collections.Look(ref Members,"members",LookMode.Reference);
            Scribe_Values.Look(ref Started,"started");Scribe_Values.Look(ref Closed,"closed",-1);Scribe_Values.Look(ref Outcome,"outcome","open");
            Scribe_Values.Look(ref Kind,"kind","recreation");Scribe_Values.Look(ref Warned,"warned",false);
            Scribe_Values.Look(ref Extension,"extension",0);Scribe_References.Look(ref Mediator,"mediator");
            Scribe_Values.Look(ref ProtestWarnedAt,"protestWarnedAt",-1);Scribe_Values.Look(ref ProtestStarted,"protestStarted",-1);
            Scribe_Values.Look(ref ProtestClosed,"protestClosed",-1);Scribe_Values.Look(ref ProtestOutcome,"protestOutcome","");
            Scribe_Values.Look(ref StrikeAnnounced,"strikeAnnounced",false);
            Scribe_Values.Look(ref SabotageAnnounced,"sabotageAnnounced",false);
            Scribe_Values.Look(ref SplinterAnnounced,"splinterAnnounced",false);
            if(Scribe.mode==LoadSaveMode.PostLoadInit && Members==null)Members=new List<Pawn>();
        }
    }
    public static class RequestNeeds
    {
        public static Need NeedFor(Pawn pawn,string kind)=>kind=="food"?(Need)pawn?.needs?.food:kind=="rest"?pawn?.needs?.rest:kind=="recreation"?pawn?.needs?.joy:null;
        public static float Average(IEnumerable<Pawn> members,string kind)
        {
            var needs=members.Select(p=>NeedFor(p,kind)).ToArray();
            return needs.Length==0 || needs.Any(n=>n==null)?-1:needs.Average(n=>n.CurLevelPercentage);
        }
        public static string Select(IEnumerable<Pawn> members)
        {
            foreach(var kind in new[]{"food","rest","recreation"})
            {float average=Average(members,kind),threshold=kind=="food"?.25f:kind=="rest"?.2f:.3f;if(average>=0 && average<threshold)return kind;}
            return null;
        }
    }
    public static class CrewContext
    {
        static readonly Type Type=AccessTools.TypeByName("Crew.GameComponent_Crews");
        public static bool Available=>Type!=null;
        static bool warned;
        public static Pawn[] Members(Pawn leader)
        {
            if(Type==null || leader==null)return new Pawn[0];
            try
            {
                var group=AccessTools.Method(Type,"CrewOf").Invoke(null,new object[]{leader});
                if(group==null || (Pawn)AccessTools.Field(group.GetType(),"leader").GetValue(group)!=leader)return new Pawn[0];
                return ((IEnumerable)AccessTools.Field(group.GetType(),"members").GetValue(group)).Cast<Pawn>().Where(p=>GameComponent_Renown.Person(p) && p.IsFreeColonist && p.Map==leader.Map).ToArray();
            }
            catch(Exception e){if(!warned)Log.Warning("[Renown] Crew request adapter unavailable: "+e.GetType().Name);warned=true;return new Pawn[0];}
        }
    }
    public partial class GameComponent_Renown
    {
        public List<CrewRequest> Requests=new List<CrewRequest>();
        public int RequestDeadline(CrewRequest request)=>request.Started+3*Policy.Day+Math.Max(0,Math.Min(Policy.Day,request.Extension));
        public bool CanMediate(Pawn mediator,Pawn leader)
        {
            if(!Conversation(mediator,leader) || !mediator.IsFreeColonist || !leader.IsFreeColonist ||
                (leader.relations?.OpinionOf(mediator)??-100)<20 || (mediator.skills?.GetSkill(SkillDefOf.Social)?.Level??0)<6)return false;
            int now=Find.TickManager.TicksGame;
            var request=Requests.FirstOrDefault(r=>r.Leader==leader && r.Closed<0 && r.Warned && r.Extension==0 && now<RequestDeadline(r));
            if(request==null || RequestNeeds.Average(request.Members,request.Kind)<0 || RequestNeeds.Average(request.Members,request.Kind)>=.6f)return false;
            var current=CrewContext.Members(leader);
            return current.Length==request.Members.Count && current.Length>=3 && !current.Except(request.Members).Any();
        }
        public bool Mediate(Pawn mediator,Pawn leader)
        {
            if(!CanMediate(mediator,leader))return false;
            var request=Requests.First(r=>r.Leader==leader && r.Closed<0 && r.Warned && r.Extension==0);
            request.Extension=Policy.Day;request.Mediator=mediator;
            string text=mediator.LabelShort+" helped "+leader.LabelShort+"'s crew allow one more day for "+request.Kind+".";
            string detail="A trusted colonist with Social 6 or higher negotiated time after the warning. One extension only. The 60% needs target is unchanged; conversation does not fulfil the request or grant a reward.";
            DisplayBridge.Record(text,detail,mediator,leader,leader.Map);
            Messages.Message("Renown: "+text,leader,MessageTypeDefOf.NeutralEvent);return true;
        }
        public bool CanPersuade(Pawn speaker,Pawn listener)
        {
            var k=Knows(listener,speaker);int now=Find.TickManager.TicksGame;
            return k!=null && Conversation(speaker,listener) && speaker.IsFreeColonist && listener.IsFreeColonist &&
                now-k.LearnedAt>=2500 && Influence(speaker,listener)>=15 &&
                (k.LastPersuaded<0 || now-k.LastPersuaded>=Policy.Day);
        }
        public bool Persuade(Pawn speaker,Pawn listener)
        {
            if(!CanPersuade(speaker,listener))return false;
            var k=Knows(listener,speaker);k.LastPersuaded=Find.TickManager.TicksGame;
            listener.needs.mood.thoughts.memories.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("RN_PersuasiveStanding"),speaker);
            DisplayBridge.Record(listener.LabelShort+" listened to "+speaker.LabelShort+"'s case.",
                "Existing knowledge and trust supported a small, temporary +2 opinion memory. Contextual influence: "+Influence(speaker,listener)+"/60. No work orders, election, faction change or forced obedience.",speaker,listener,speaker.Map);
            return true;
        }
        public bool OpenRequest(Pawn leader)
        {
            if(!Attentive(leader) || Fame(leader)<30)return false;
            var members=CrewContext.Members(leader);
            int now=Find.TickManager.TicksGame;
            if(members.Length<3 || members.Count(p=>p!=leader && Knows(p,leader)!=null)<members.Length/2+1)return false;
            string kind=RequestNeeds.Select(members);if(kind==null)return false;
            if(Requests.Any(r=>r.Leader==leader && now-r.Started<7*Policy.Day || r.Closed<0 && r.Members.Intersect(members).Any()))return false;
            Requests.Add(new CrewRequest{Leader=leader,Members=members.ToList(),Started=now,Kind=kind});
            string detail="Raise average "+kind+" to 60% within three days. Members get +2 mood if met, -2 if unmet; two-day memories. No schedules, jobs or food restrictions are changed.";
            DisplayBridge.Record(leader.LabelShort+"'s crew asked for "+kind+".",detail,leader,null,leader.Map);
            Messages.Message("Renown: "+leader.LabelShort+"'s crew asks for "+kind+". "+detail,leader,MessageTypeDefOf.NeutralEvent);
            return true;
        }
        public void RefreshRequests()
        {
            int now=Find.TickManager.TicksGame;
            foreach(var r in Requests.Where(r=>r.Closed<0).ToArray())
            {
                var current=CrewContext.Members(r.Leader);
                if(r.Leader==null || current.Length!=r.Members.Count || current.Except(r.Members).Any() || r.Members.Any(p=>!Person(p)) || RequestNeeds.Average(r.Members,r.Kind)<0)
                {r.Closed=now;r.Outcome="withdrawn: group changed";continue;}
                bool met=RequestNeeds.Average(r.Members,r.Kind)>=.6f;
                if(!met && now<RequestDeadline(r))
                {
                    if(!r.Warned && now-r.Started>=2*Policy.Day)
                    {
                        r.Warned=true;
                        string warning=r.Leader.LabelShort+"'s crew still needs "+r.Kind+". Less than one day remains to reach 60% average.";
                        Messages.Message("Renown: "+warning,r.Leader,MessageTypeDefOf.NeutralEvent);
                        DisplayBridge.Record(warning,"One reminder per request. The original deadline is unchanged.",r.Leader,null,r.Leader.Map);
                    }
                    continue;
                }
                r.Closed=now;r.Outcome=met?"met":"unmet";
                foreach(var p in r.Members)p.needs.mood.thoughts.memories.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed(met?"RN_RequestMet":"RN_RequestUnmet"));
                DisplayBridge.Record(r.Leader.LabelShort+"'s "+r.Kind+" request was "+r.Outcome+".","Outcome measured from current crew needs; limited mood consequences only.",r.Leader,null,r.Leader.Map);
            }
            RefreshProtests();
            Requests.RemoveAll(r=>r==null || r.Leader==null || now-r.Started>=7*Policy.Day && (r.ProtestWarnedAt<0 || r.ProtestClosed>=0));
            if(Requests.Count>64)Requests.RemoveRange(0,Requests.Count-64);
            if(CrewContext.Available)
                foreach(var map in Find.Maps)foreach(var p in map.mapPawns.FreeColonistsSpawned)OpenRequest(p);
        }
        void ExposeRequests()
        {
            Scribe_Collections.Look(ref Requests,"renownCrewRequests",LookMode.Deep);
            if(Scribe.mode==LoadSaveMode.PostLoadInit && Requests==null)Requests=new List<CrewRequest>();
        }
    }
    public class InteractionWorker_Mediate : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator,Pawn recipient)=>GameComponent_Renown.Current?.CanMediate(initiator,recipient)==true?.5f:0;
        public override void Interacted(Pawn initiator,Pawn recipient,List<RulePackDef> packs,out string letterText,out string letterLabel,out LetterDef letterDef,out LookTargets lookTargets)
        {letterText=null;letterLabel=null;letterDef=null;lookTargets=null;GameComponent_Renown.Current?.Mediate(initiator,recipient);}
    }
    public class InteractionWorker_Persuade : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator,Pawn recipient)=>GameComponent_Renown.Current?.CanPersuade(initiator,recipient)==true?
            GameComponent_Renown.Current.Influence(initiator,recipient)/100f*SocialContext.StoryWeight(initiator):0;
        public override void Interacted(Pawn initiator,Pawn recipient,List<RulePackDef> packs,out string letterText,out string letterLabel,out LetterDef letterDef,out LookTargets lookTargets)
        {letterText=null;letterLabel=null;letterDef=null;lookTargets=null;GameComponent_Renown.Current?.Persuade(initiator,recipient);}
    }
}
