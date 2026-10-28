using System.Collections.Generic;
using Verse;
using RimWorld;

namespace Renown
{
    public static class Policy
    {
        public const int KnowledgeDays=30, Day=60000, DeedLimit=256, KnowledgeLimit=2048;
        public static bool Fresh(int tick,int now)=>now>=tick && now-tick<KnowledgeDays*Day;
        public static bool CanRecognize(int deedTick,int lastRecognition,int now)=>Fresh(deedTick,now) &&
            now-deedTick>=2500 && (lastRecognition<0 || now-lastRecognition>=Day);
        public static bool Admiration(int opinion,bool hostile)=>!hostile && opinion>=0;
    }
    public sealed class Deed : IExposable
    {
        public int Id,Tick,Quality;
        public string ProductId,ProductLabel;
        public string Kind="craft";
        public Pawn Subject;
        public Faction SubjectFaction;
        public Pawn Maker;
        public Map Map;
        public List<Pawn> Witnesses=new List<Pawn>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref Id,"id");Scribe_Values.Look(ref Tick,"tick");Scribe_Values.Look(ref Quality,"quality");
            Scribe_Values.Look(ref ProductId,"productId");Scribe_Values.Look(ref ProductLabel,"productLabel");
            Scribe_References.Look(ref Maker,"maker");Scribe_References.Look(ref Map,"map");
            Scribe_Values.Look(ref Kind,"kind","craft");Scribe_References.Look(ref Subject,"subject",saveDestroyedThings:Kind=="execution" && Subject?.Corpse!=null && (Subject.Corpse.Spawned || Subject.Corpse.ParentHolder!=null));
            Scribe_References.Look(ref SubjectFaction,"subjectFaction");
            Scribe_Collections.Look(ref Witnesses,"witnesses",LookMode.Reference);
            if(Scribe.mode==LoadSaveMode.PostLoadInit && Witnesses==null)Witnesses=new List<Pawn>();
        }
    }
    public sealed class Knowledge : IExposable
    {
        public Pawn Witness,Maker;
        public int DeedId,LastRecognition=-1;
        public Pawn Source;
        public int Hops,LearnedAt,LastShared=-1,LastPersuaded=-1;
        public void ExposeData()
        {
            Scribe_References.Look(ref Witness,"witness");Scribe_References.Look(ref Maker,"maker");
            Scribe_Values.Look(ref DeedId,"deedId");Scribe_Values.Look(ref LastRecognition,"lastRecognition",-1);
            Scribe_References.Look(ref Source,"source");Scribe_Values.Look(ref Hops,"hops");
            Scribe_Values.Look(ref LearnedAt,"learnedAt");Scribe_Values.Look(ref LastShared,"lastShared",-1);
            Scribe_Values.Look(ref LastPersuaded,"lastPersuaded",-1);
        }
    }
}
