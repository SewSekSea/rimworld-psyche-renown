using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Renown
{
    public partial class GameComponent_Renown
    {
        public bool PilgrimRecruitEligible(PurposeVisit visit,Pawn guest)
        {
            if(RenownMod.Settings?.Pilgrims!=true || RenownMod.Settings?.Recruits!=true || visit==null || visit.Kind!="pilgrim" || visit.Closed>=0 || visit.Map==null || !visit.Map.IsPlayerHome || visit.Collected<1 || !Person(visit.Subject) || visit.Subject.Map!=visit.Map || !visit.Subject.IsFreeColonist)return false;
            if(!Person(guest) || !guest.Spawned || guest.Map!=visit.Map || guest.Downed || guest.InMentalState || guest.IsPrisoner || guest.IsSlave || guest.Faction==null || guest.Faction!=visit.Faction || guest.Faction.IsPlayer || guest.Faction.HostileTo(Faction.OfPlayer) || guest.Faction.leader==guest || !visit.Guests.Contains(guest) || visit.Returned.Contains(guest) || guest.questTags?.Count>0)return false;
            int now=Find.TickManager.TicksGame;
            if(now-visit.Started<Policy.Day || now-visit.Started>=7*Policy.Day || guest.ageTracker.AgeBiologicalYearsFloat<18 || guest.needs.mood?.CurLevel<.5f || (guest.relations?.OpinionOf(visit.Subject)??-100)<20)return false;
            var account=Knows(guest,visit.Subject);var appraisal=AssessAccount(account);
            return Evidence(account)!=null && appraisal.Admiration>=10 && appraisal.Distrust==0 && appraisal.Threat==0;
        }
        public bool TryOfferPilgrimRecruit(PurposeVisit visit)
        {
            int now=Find.TickManager.TicksGame;
            if(visit==null || visit.JoinOffered>=0 || visit.JoinOutcome!="none" || lastRecruitAttempt>=0 && now-lastRecruitAttempt<30*Policy.Day)return false;
            var guest=visit.Guests.FirstOrDefault(p=>PilgrimRecruitEligible(visit,p));if(guest==null)return false;
            var def=DefDatabase<LetterDef>.GetNamedSilentFail("RN_PilgrimJoin");if(def==null)return false;
            var letter=LetterMaker.MakeLetter("Renown: pilgrim asks to stay",guest.LabelShort+" has spent a day here, learned a sourced account of "+visit.Subject.LabelShort+", and asks to join. Accepting recruits this visitor using native recruitment. Their skills, beliefs and relationships are unchanged; loyalty is not guaranteed. You have one day to decide. Declining leaves the visitor with their party. The offer spends the shared thirty-day recruitment budget.",def,guest) as PilgrimJoinLetter;
            if(letter==null)return false;
            visit.JoinCandidate=guest;visit.JoinOffered=now;visit.JoinOutcome="offered";lastRecruitAttempt=now;
            letter.Candidate=guest;letter.VisitStarted=visit.Started;Find.LetterStack.ReceiveLetter(letter);return true;
        }
        public PurposeVisit PilgrimOffer(Pawn guest,int started)=>PurposeVisits.FirstOrDefault(v=>v.JoinCandidate==guest && v.Started==started && v.JoinOutcome=="offered");
        public bool ResolvePilgrimOffer(Pawn guest,int started,bool accept)
        {
            var visit=PilgrimOffer(guest,started);if(visit==null)return false;
            if(!accept){visit.JoinOutcome="declined";return true;}
            if(Find.TickManager.TicksGame-visit.JoinOffered>=Policy.Day || !PilgrimRecruitEligible(visit,guest)){visit.JoinOutcome="no longer eligible";return false;}
            visit.JoinOutcome="accepted";
            RecruitUtility.Recruit(guest,Faction.OfPlayer);
            bool joined=guest.Faction==Faction.OfPlayer;
            visit.JoinOutcome=joined?"joined":"recruitment failed";
            if(joined){lastRecruitSuccess=Find.TickManager.TicksGame;DisplayBridge.Record(guest.LabelShort+" chose to stay after a pilgrimage.","Player accepted this actual visitor's offer. Native recruitment preserves pawn identity. No automatic admiration, fabricated deed or guaranteed future loyalty.",guest,visit.Subject,visit.Map);}
            return joined;
        }
    }
    public sealed class PilgrimJoinLetter : ChoiceLetter
    {
        public Pawn Candidate;
        public int VisitStarted;
        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                var ledger=GameComponent_Renown.Current;
                if(ArchivedOnly || ledger?.PilgrimOffer(Candidate,VisitStarted)==null){yield return Option_Close;yield break;}
                var accept=new DiaOption("Accept pilgrim"){resolveTree=true,action=()=>{ledger.ResolvePilgrimOffer(Candidate,VisitStarted,true);Find.LetterStack.RemoveLetter(this);}};
                var visit=ledger.PilgrimOffer(Candidate,VisitStarted);
                if(Find.TickManager.TicksGame-visit.JoinOffered>=Policy.Day || !ledger.PilgrimRecruitEligible(visit,Candidate))accept.Disable("This offer is no longer eligible.");
                yield return accept;
                yield return new DiaOption("Decline"){resolveTree=true,action=()=>{ledger.ResolvePilgrimOffer(Candidate,VisitStarted,false);Find.LetterStack.RemoveLetter(this);}};
                yield return Option_Postpone;
            }
        }
        public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref Candidate,"rnPilgrimCandidate");Scribe_Values.Look(ref VisitStarted,"rnPilgrimVisitStarted");}
    }
}
