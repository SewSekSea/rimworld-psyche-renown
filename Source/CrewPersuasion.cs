using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Renown
{
    public sealed class PersuasionRecord : IExposable
    {
        public Pawn Listener,Subject;
        public int Tick,Offset;
        public void ExposeData(){Scribe_References.Look(ref Listener,"listener");Scribe_References.Look(ref Subject,"subject");Scribe_Values.Look(ref Tick,"tick");Scribe_Values.Look(ref Offset,"offset");}
    }
    public partial class GameComponent_Renown
    {
        public List<PersuasionRecord> Persuasions=new List<PersuasionRecord>();
        public bool ApplyCampaignView(Pawn listener,Pawn subject)
        {
            if(RenownMod.Settings?.CrewPersuasion!=true || !Person(listener) || !listener.IsFreeColonist || !Person(subject) || listener==subject || Knows(listener,subject)==null)return false;
            int now=Find.TickManager.TicksGame;
            Persuasions.RemoveAll(p=>p.Listener==null || p.Subject==null || now-p.Tick>=5*Policy.Day);
            if(Persuasions.Count>=128 || Persuasions.Any(p=>p.Listener==listener && p.Subject==subject))return false;
            var view=Assess(listener,subject);int score=view.Admiration-view.Distrust-view.Threat;
            int offset=score>=10?4:score<=-10?-4:0;if(offset==0)return false;
            listener.needs.mood.thoughts.memories.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed(offset>0?"RN_CampaignRespect":"RN_CampaignDoubt"),subject);
            Persuasions.Add(new PersuasionRecord{Listener=listener,Subject=subject,Tick=now,Offset=offset});
            DisplayBridge.Record(listener.LabelShort+(offset>0?" reconsidered favourably ":" became more doubtful of ")+subject.LabelShort+" after a sourced campaign.","Temporary "+(offset>0?"+4":"-4")+" opinion for three days, once per listener/subject per five days. The listener's own appraisal decides the direction. Crew retains its ordinary relationship-based grouping; no membership or leadership is assigned.",listener,subject,listener.Map);return true;
        }
        public Pawn OutreachLeader(Pawn speaker,Pawn listener)
        {
            if(RenownMod.Settings?.CrewPersuasion!=true || !Conversation(speaker,listener) || !speaker.IsFreeColonist || !listener.IsFreeColonist)return null;
            return speaker.Map.mapPawns.FreeColonistsSpawned.OrderBy(p=>p.thingIDNumber).FirstOrDefault(leader=>
            {
                var members=CrewContext.Members(leader);
                return members.Length>=3 && members.Contains(speaker) && !members.Contains(listener) && Fame(leader)>=30 &&
                    !Persuasions.Any(p=>p.Listener==listener && p.Subject==leader && Find.TickManager.TicksGame-p.Tick<5*Policy.Day) && StoryAbout(speaker,listener,leader)!=null;
            });
        }
        public bool Outreach(Pawn speaker,Pawn listener)
        {
            var leader=OutreachLeader(speaker,listener);if(leader==null || !ShareAbout(speaker,listener,leader))return false;
            ApplyCampaignView(listener,leader);return true;
        }
        public string PersuasionDetails(Pawn focus)
        {
            int now=Find.TickManager.TicksGame;
            return string.Join("\n",Persuasions.Where(p=>p.Listener!=null && p.Subject!=null && (p.Listener==focus || p.Subject==focus) && now-p.Tick<5*Policy.Day)
                .Select(p=>p.Listener.LabelShort+" toward "+p.Subject.LabelShort+": "+(p.Offset>0?"+":"")+p.Offset+" opinion granted; cooldown "+(System.Math.Max(0,5*Policy.Day-(now-p.Tick))/(float)Policy.Day).ToString("0.0")+" days. Memory lasts at most three days; no assigned membership."));
        }
        void ExposePersuasions()
        {
            Scribe_Collections.Look(ref Persuasions,"renownPersuasions",LookMode.Deep);
            if(Scribe.mode==LoadSaveMode.PostLoadInit)Persuasions=(Persuasions??new List<PersuasionRecord>()).Where(p=>p!=null).Take(128).ToList();
        }
    }
    public sealed class InteractionWorker_Outreach : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator,Pawn recipient)=>GameComponent_Renown.Current?.OutreachLeader(initiator,recipient)!=null?.15f:0;
        public override void Interacted(Pawn initiator,Pawn recipient,List<RulePackDef> packs,out string letterText,out string letterLabel,out LetterDef letterDef,out LookTargets lookTargets)
        {letterText=null;letterLabel=null;letterDef=null;lookTargets=null;GameComponent_Renown.Current?.Outreach(initiator,recipient);}
    }
}
