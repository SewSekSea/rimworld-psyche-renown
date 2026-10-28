using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Renown
{
    public static class ObservationGuard
    {
        static readonly HashSet<string> Warned=new HashSet<string>();
        public static void Run(string hook,Action action)
        {try{action();}catch(Exception e){if(Warned.Add(hook))Log.Warning("[Renown] "+hook+" observation unavailable: "+e.GetType().Name);}}
    }
    public sealed class FactionKnowledge : IExposable
    {
        public Faction Faction;
        public Pawn Maker,Carrier;
        public int DeedId,Tick;
        public void ExposeData()
        {
            Scribe_References.Look(ref Faction,"faction");Scribe_References.Look(ref Maker,"maker");Scribe_References.Look(ref Carrier,"carrier");
            Scribe_Values.Look(ref DeedId,"deed");Scribe_Values.Look(ref Tick,"tick");
        }
    }
    public static class SocialContext
    {
        static readonly Type Crew=AccessTools.TypeByName("Crew.GameComponent_Crews");
        static bool warned;
        public static bool Trait(Pawn p,string name)=>p?.story?.traits?.allTraits.Any(t=>t.def.defName==name)==true;
        public static bool SameCrew(Pawn a,Pawn b)
        {
            if(Crew==null)return false;
            try { return (bool)AccessTools.Method(Crew,"SameCrew").Invoke(null,new object[]{a,b}); }
            catch(Exception e) { if(!warned)Log.Warning("[Renown] Crew context unavailable: "+e.GetType().Name);warned=true;return false; }
        }
        public static float StoryWeight(Pawn p)=>Trait(p,"NR_Narcissist")?.55f:Trait(p,"MAC_Machiavellian")?.45f:.3f;
        public static int Influence(Pawn speaker,Pawn listener,int fame,bool known)
        {
            if(!known || speaker==listener || speaker==null || listener==null || speaker.HostileTo(listener))return 0;
            int opinion=listener.relations?.OpinionOf(speaker)??-100;
            if(opinion<0)return 0;
            int raw=5+Math.Min(20,opinion/5)+Math.Min(20,fame/5)+(SameCrew(speaker,listener)?10:0);
            if(Trait(listener,"SP_Sociopath") || listener.story?.traits?.HasTrait(TraitDefOf.Psychopath)==true)raw/=2;
            return Math.Min(60,raw);
        }
    }
    public partial class GameComponent_Renown
    {
        public List<FactionKnowledge> Abroad=new List<FactionKnowledge>();
        public bool RecordDeed(string kind,Pawn actor,Pawn subject)
        {
            if(!Person(actor) || !actor.IsFreeColonist || subject==null || actor==subject || actor.Map!=subject.Map)return false;
            if(kind!="rescue" && kind!="combat" && kind!="medical")return false;
            if(kind=="medical" && (!Person(subject) || subject.HostileTo(actor) || subject.IsPrisoner))return false;
            if(kind=="rescue" && (subject.Dead || subject.HostileTo(actor) || subject.IsPrisoner))return false;
            if(kind=="combat" && (!subject.Downed || !subject.HostileTo(actor) || subject.IsPrisoner || !subject.RaceProps.Humanlike))return false;
            int now=Find.TickManager.TicksGame;
            // One subject/actor/kind per day prevents repeated rescue or downing farms.
            if(Deeds.Any(d=>d.Kind==kind && d.Maker==actor && d.Subject==subject && now-d.Tick<Policy.Day))return false;
            var deed=new Deed {Id=nextId++,Tick=now,Kind=kind,Maker=actor,Subject=subject,Map=actor.Map,ProductId=kind+":"+subject.ThingID+":"+now,
                ProductLabel=kind=="rescue"?"rescuing "+subject.LabelShort:kind=="medical"?"tending bleeding injuries on "+subject.LabelShort:"downing hostile "+subject.LabelShort,
                Witnesses=actor.Map.mapPawns.AllPawnsSpawned.Where(p=>Sees(p,actor) && p.Position.DistanceTo(subject.Position)<=12 && GenSight.LineOfSight(p.Position,subject.Position,actor.Map)).ToList()};
            Deeds.Add(deed);
            foreach(var person in deed.Witnesses)
            {
                var old=Known.FirstOrDefault(k=>k.Witness==person && k.Maker==actor);
                if(old==null)Known.Add(new Knowledge{Witness=person,Maker=actor,DeedId=deed.Id,LearnedAt=now});
                else {Archive(old);old.DeedId=deed.Id;old.Source=null;old.Hops=0;old.LearnedAt=now;}
                DisplayBridge.Record(person.LabelShort+" witnessed "+actor.LabelShort+" "+deed.ProductLabel+".","Direct witness. Recognition is separate from admiration. This is evidence, not a universal opinion bonus.",person,actor,actor.Map);
            }
            Prune();return true;
        }
        public static bool Conversation(Pawn a,Pawn b)=>a!=b && Attentive(a) && Attentive(b) && a.Map==b.Map && !a.HostileTo(b) &&
            a.Position.DistanceTo(b.Position)<=6 && GenSight.LineOfSight(a.Position,b.Position,a.Map);
        public Knowledge Story(Pawn speaker,Pawn listener)=>StoryAbout(speaker,listener,null);
        public Knowledge StoryAbout(Pawn speaker,Pawn listener,Pawn subject)
        {
            if(!Conversation(speaker,listener) || (listener.relations?.OpinionOf(speaker)??-100)<0)return null;
            int now=Find.TickManager.TicksGame;
            return Known.Where(k=>k.Witness==speaker && (subject==null || k.Maker==subject) && k.Maker!=listener && k.Maker!=speaker && k.Hops<3 && Evidence(k)!=null &&
                now-k.LearnedAt>=2500 && (k.LastShared<0 || now-k.LastShared>=2500) &&
                !Accounts(listener,k.Maker).Any(other=>other.DeedId==k.DeedId))
                .OrderBy(k=>k.Hops).ThenByDescending(k=>k.LearnedAt).FirstOrDefault(k=>
                !Known.Any(old=>old.Witness==listener && old.Maker==k.Maker && old.Hops==0 && Evidence(old)!=null));
        }
        public bool Share(Pawn speaker,Pawn listener)=>ShareAbout(speaker,listener,null);
        public bool ShareAbout(Pawn speaker,Pawn listener,Pawn subject)
        {
            var source=StoryAbout(speaker,listener,subject);if(source==null)return false;
            int now=Find.TickManager.TicksGame;
            var old=Known.FirstOrDefault(k=>k.Witness==listener && k.Maker==source.Maker);
            if(old==null) {old=new Knowledge{Witness=listener,Maker=source.Maker};Known.Add(old);}
            Archive(old);old.DeedId=source.DeedId;old.Source=speaker;old.Hops=source.Hops+1;old.LearnedAt=now;source.LastShared=now;
            var deed=Evidence(source);
            DisplayBridge.Record(speaker.LabelShort+" told "+listener.LabelShort+" about "+source.Maker.LabelShort+".",
                "Account: "+deed.ProductLabel+". Source: "+speaker.LabelShort+"; transmission depth "+old.Hops+" of 3. Secondhand knowledge grants no automatic respect.",speaker,listener,speaker.Map);
            Prune();return true;
        }
        public int Fame(Pawn p)=>Math.Min(100,AccountsAbout(p).Count*5);
        public int Influence(Pawn speaker,Pawn listener)=>SocialContext.Influence(speaker,listener,Fame(speaker),Knows(listener,speaker)!=null);
        public string ReputationDetails(Pawn p)
        {
            var stories=AccountsHeld(p).Where(k=>k.Hops>0).Take(3);
            return string.Concat(stories.Select(k=>"\nHeard about "+k.Maker.LabelShort+" from "+k.Source.LabelShort+" ("+k.Hops+" transmissions)."))+
                "\nFaction reports: "+Abroad.Count(k=>k.Maker==p)+". Fame is awareness, not admiration."+
                string.Concat(Requests.Where(r=>r.Leader==p && r.Closed<0).Select(r=>"\nCrew request: average "+r.Kind+" 60% by tick "+(r.Started+3*Policy.Day)+"."));
        }
        public int Export(Pawn carrier,Faction faction)
        {
            if(carrier==null || carrier.Dead || faction==null || carrier.Faction!=faction || faction==Faction.OfPlayer || faction.defeated || faction.IsPlayer)return 0;
            int added=0;
            foreach(var k in Known.Where(k=>k.Witness==carrier && Evidence(k)!=null).ToArray())
            {
                var old=Abroad.FirstOrDefault(a=>a.Faction==faction && a.Maker==k.Maker);
                if(old!=null && old.DeedId==k.DeedId)continue;
                if(old==null){old=new FactionKnowledge{Faction=faction,Maker=k.Maker};Abroad.Add(old);}
                old.Carrier=carrier;old.DeedId=k.DeedId;old.Tick=Find.TickManager.TicksGame;added++;
                DisplayBridge.Record(carrier.LabelShort+" carried news of "+k.Maker.LabelShort+" to "+faction.Name+".",
                    "A departing faction member held a witnessed or sourced account. This records faction awareness, not automatic goodwill or extra raids.",carrier,k.Maker,carrier.MapHeld);
            }
            PruneWorld();return added;
        }
        void PruneWorld()
        {
            if(Abroad==null)Abroad=new List<FactionKnowledge>();
            Abroad.RemoveAll(a=>a==null || a.Faction==null || a.Maker==null || a.Faction.defeated || !Policy.Fresh(a.Tick,Find.TickManager.TicksGame) || !Deeds.Any(d=>d.Id==a.DeedId));
            if(Abroad.Count>128)Abroad.RemoveRange(0,Abroad.Count-128);
        }
        void ExposeWorld()
        {
            Scribe_Collections.Look(ref Abroad,"renownFactionKnowledge",LookMode.Deep);
            if(Scribe.mode==LoadSaveMode.PostLoadInit && Abroad==null)Abroad=new List<FactionKnowledge>();
        }
    }
    public class InteractionWorker_Share : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator,Pawn recipient)=>GameComponent_Renown.Current?.Story(initiator,recipient)!=null?SocialContext.StoryWeight(initiator):0;
        public override void Interacted(Pawn initiator,Pawn recipient,List<RulePackDef> packs,out string letterText,out string letterLabel,out LetterDef letterDef,out LookTargets lookTargets)
        {letterText=null;letterLabel=null;letterDef=null;lookTargets=null;GameComponent_Renown.Current?.Share(initiator,recipient);}
    }
    [HarmonyPatch(typeof(Pawn_RelationsTracker),nameof(Pawn_RelationsTracker.Notify_RescuedBy))]
    public static class RescuePatch
    {
        static void Postfix(Pawn ___pawn,Pawn rescuer,bool __runOriginal)
        {if(__runOriginal)ObservationGuard.Run("rescue",()=>{if(___pawn.InBed())GameComponent_Renown.Current?.RecordDeed("rescue",rescuer,___pawn);});}
    }
    [HarmonyPatch(typeof(Pawn_HealthTracker),"MakeDowned")]
    public static class CombatPatch
    {
        static void Prefix(Pawn ___pawn,out bool __state){__state=___pawn.Downed;}
        static void Postfix(Pawn ___pawn,DamageInfo? dinfo,bool __state,bool __runOriginal)
        {if(__runOriginal && !__state)ObservationGuard.Run("combat",()=>{if(___pawn.Downed && dinfo?.Instigator is Pawn actor)GameComponent_Renown.Current?.RecordDeed("combat",actor,___pawn);});}
    }
    [HarmonyPatch(typeof(Faction),nameof(Faction.Notify_MemberExitedMap))]
    public static class DeparturePatch
    {
        static void Postfix(Faction __instance,Pawn member,bool __runOriginal)
        {if(__runOriginal)ObservationGuard.Run("departure",()=>{if(member.Faction==__instance){GameComponent_Renown.Current?.Export(member,__instance);GameComponent_Renown.Current?.HostingDeparture(member);GameComponent_Renown.Current?.PurposeDeparture(member);}});}
    }
}
