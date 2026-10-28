using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Renown
{
    [StaticConstructorOnStartup]
    public static class Bootstrap
    {
        static Bootstrap() { new Harmony("tb.renown").PatchAll();Log.Message("[Renown] Loaded. Witnessed craftsmanship and local recognition."); }
    }
    public static class DisplayBridge
    {
        static readonly Type UI=AccessTools.TypeByName("PsycheUI.GameComponent_PsycheUI");
        static bool warned;
        public static bool Available=>UI!=null;
        public static void Record(string text,string detail,Pawn actor,Pawn other,Map map)
        {
            if(UI==null)return;
            try
            {
                var ledger=AccessTools.Property(UI,"Current")?.GetValue(null,null);
                if(ledger!=null)AccessTools.Method(UI,"Record",new[]{typeof(string),typeof(string),typeof(string),typeof(Pawn),typeof(Pawn),typeof(Map),typeof(Pawn)})
                    .Invoke(ledger,new object[]{"Renown",text,detail,actor,other,map,null});
            }
            catch(Exception e) { if(!warned)Log.Warning("[Renown] Optional UI display unavailable: "+e.GetType().Name);warned=true; }
        }
    }
    public partial class GameComponent_Renown : GameComponent
    {
        public List<Deed> Deeds=new List<Deed>();
        public List<Knowledge> Known=new List<Knowledge>();
        int nextId=1,nextPrune;
        public GameComponent_Renown(Game game) { }
        public static GameComponent_Renown Current=>Verse.Current.Game?.GetComponent<GameComponent_Renown>();
        public static bool Person(Pawn p)=>p!=null && !p.Dead && p.Spawned && p.RaceProps.Humanlike && !p.DevelopmentalStage.Juvenile() && p.needs?.mood!=null;
        public static bool Attentive(Pawn p)=>Person(p) && !p.Downed && p.Awake() && !p.InMentalState;
        public static bool Sees(Pawn witness,Pawn maker)=>witness!=maker && Attentive(witness) && Person(maker) && witness.Map==maker.Map &&
            witness.Position.DistanceTo(maker.Position)<=12 && GenSight.LineOfSight(witness.Position,maker.Position,maker.Map);
        public Deed Evidence(Knowledge k)=>k==null?null:Deeds.FirstOrDefault(d=>d.Id==k.DeedId && d.Maker==k.Maker &&
            (k.Hops==0?d.Witnesses.Contains(k.Witness):k.Hops>0 && k.Hops<=3 && k.Source!=null) && Policy.Fresh(d.Tick,Find.TickManager.TicksGame));
        public Knowledge Knows(Pawn witness,Pawn maker)=>Known.FirstOrDefault(k=>k.Witness==witness && k.Maker==maker && Evidence(k)!=null)??Accounts(witness,maker).FirstOrDefault();
        public bool Observe(Thing product,RecipeDef recipe,Pawn maker)
        {
            if(recipe?.workSkill==null)return false;
            return ObserveFinishedProduct(product,maker);
        }
        internal bool ObserveFinishedProduct(Thing product,Pawn maker)
        {
            if(!Person(maker) || !maker.IsFreeColonist || product==null)return false;
            // Minified furniture keeps its quality on the inner item.
            var item=product is MinifiedThing minified?minified.InnerThing:product;
            if(item==null || !item.TryGetQuality(out var quality) || quality<QualityCategory.Masterwork)return false;
            Prune();
            if(Deeds.Any(d=>d.ProductId==item.ThingID))return false;
            var deed=new Deed { Id=nextId++,Tick=Find.TickManager.TicksGame,Quality=(int)quality,ProductId=item.ThingID,
                ProductLabel=item.LabelNoCount,Maker=maker,Map=maker.Map,Witnesses=maker.Map.mapPawns.AllPawnsSpawned.Where(p=>Sees(p,maker)).ToList() };
            Deeds.Add(deed);
            foreach(var witness in deed.Witnesses)
            {
                var prior=Known.FirstOrDefault(k=>k.Witness==witness && k.Maker==maker);
                if(prior==null)Known.Add(new Knowledge { Witness=witness,Maker=maker,DeedId=deed.Id,LearnedAt=deed.Tick });
                else { Archive(prior);prior.DeedId=deed.Id;prior.Hops=0;prior.Source=null;prior.LearnedAt=deed.Tick; }
                DisplayBridge.Record(witness.LabelShort+" witnessed "+maker.LabelShort+" finish "+deed.ProductLabel+".",
                    "Direct witness: awake, capable and within 12 cells with line of sight. Knowledge of the maker, not automatic admiration or worldwide fame.",witness,maker,maker.Map);
            }
            if(deed.Witnesses.Count==0)DisplayBridge.Record(maker.LabelShort+" finished "+deed.ProductLabel+" without witnesses.","No one else gained knowledge of this deed.",maker,null,maker.Map);
            Prune();return true;
        }
        public bool CanRecognize(Pawn witness,Pawn maker)
        {
            var k=Knows(witness,maker);var d=Evidence(k);
            return d!=null && Attentive(witness) && Attentive(maker) && witness!=maker && witness.Map==maker.Map &&
                !witness.HostileTo(maker) && witness.Position.DistanceTo(maker.Position)<=6 &&
                GenSight.LineOfSight(witness.Position,maker.Position,maker.Map) && Policy.CanRecognize(d.Tick,k.LastRecognition,Find.TickManager.TicksGame);
        }
        public bool Recognize(Pawn witness,Pawn maker)
        {
            if(!CanRecognize(witness,maker))return false;
            var k=Knows(witness,maker);var d=Evidence(k);
            bool admire=k.Hops==0 && AssessAccount(k).Admiration>0;
            k.LastRecognition=Find.TickManager.TicksGame;
            if(admire)witness.needs.mood.thoughts.memories.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed(d.Kind=="craft"?"RN_RespectedCraft":"RN_RespectedDeed"),maker);
            DisplayBridge.Record(witness.LabelShort+" recognized "+maker.LabelShort+" for "+d.ProductLabel+".",
                admire?"Directly witnessed the work. Existing opinion was neutral or positive: a small, temporary respect memory was offered.":
                    d.Kind=="execution"?"Acknowledging a witnessed execution does not grant approval or a respect memory.":"Secondhand knowledge or negative existing opinion: recognition did not grant admiration.",witness,maker,maker.Map);
            return true;
        }
        public string Inspect(Pawn p)
        {
            var witnesses=AccountsAbout(p).Select(k=>k.Witness.LabelShort+(k.Hops==0?" (witness)":" (heard)")).ToArray();
            var count=AccountsHeld(p).Count;
            if(witnesses.Length==0 && count==0)return null;
            return "Renown - recent deeds known to: "+(witnesses.Length==0?"no living witnesses":string.Join(", ",witnesses.Take(5))+(witnesses.Length>5?" and "+(witnesses.Length-5)+" more":""))+
                "\nKnown people: "+count+". Local fame: "+Fame(p)+"/100."+ReputationDetails(p);
        }
        public void Prune()
        {
            int now=Find.TickManager.TicksGame;
            Deeds.RemoveAll(d=>d==null || d.Maker==null || !Policy.Fresh(d.Tick,now));
            if(Deeds.Count>Policy.DeedLimit)Deeds.RemoveRange(0,Deeds.Count-Policy.DeedLimit);
            var ids=new HashSet<int>(Deeds.Select(d=>d.Id));
            Known.RemoveAll(k=>k==null || k.Witness==null || k.Maker==null || k.Witness.Dead || !ids.Contains(k.DeedId));
            if(Known.Count>Policy.KnowledgeLimit)Known=Known.OrderByDescending(k=>k.DeedId).Take(Policy.KnowledgeLimit).ToList();
            PruneWorld();
            PruneHistory();
        }
        public override void GameComponentTick()
        {
            RefreshSparring();
            if(Find.TickManager.TicksGame>=nextPrune)RefreshTargetedThreats();
            if(Find.TickManager.TicksGame<nextPrune)return;nextPrune=Find.TickManager.TicksGame+2500;Prune();RefreshRequests();RefreshSabotage();RefreshFractures();RefreshVisits();RefreshRivalries();RefreshDelegations();RefreshRecruits();RefreshHosting();RefreshContracts();RefreshMercenaryContracts();RefreshPurposeVisitors();RefreshPersonalWealth();
        }
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref Deeds,"renownDeeds",LookMode.Deep);Scribe_Collections.Look(ref Known,"renownKnowledge",LookMode.Deep);
            Scribe_Values.Look(ref nextId,"renownNextId",1);Scribe_Values.Look(ref nextPrune,"renownNextPrune");
            ExposeWorld();
            ExposePersonalWealth();
            ExposeAddresses();
            ExposeSparring();
            ExposeRequests();
            ExposeVisits();
            ExposeDelegations();
            ExposeHosting();
            ExposeContracts();
            ExposeMercenaryContracts();
            ExposePurposeVisitors();
            ExposeTargetedThreats();
            ExposePersuasions();
            ExposeSabotage();ExposeFractures();
            ExposeEmissaries();
            ExposeRecruits();
            ExposeHistory();
            ExposeRivalries();
            if(Scribe.mode==LoadSaveMode.PostLoadInit)
            {
                Deeds=Deeds??new List<Deed>();Known=Known??new List<Knowledge>();Prune();
                nextId=Math.Max(nextId,Deeds.Count==0?1:Deeds.Max(d=>d.Id)+1);
            }
        }
    }
    [HarmonyPatch(typeof(GenRecipe),"PostProcessProduct")]
    public static class CraftPatch
    {
        static bool warned;
        [HarmonyPriority(Priority.Last)]
        static void Postfix(Thing __result,RecipeDef recipeDef,Pawn worker,bool __runOriginal)
        {
            if(!__runOriginal)return;
            try { GameComponent_Renown.Current?.Observe(__result,recipeDef,worker); }
            catch(Exception e) { if(!warned)Log.Warning("[Renown] Craft observation unavailable: "+e.GetType().Name);warned=true; }
        }
    }
    public class InteractionWorker_Recognize : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator,Pawn recipient)=>GameComponent_Renown.Current?.CanRecognize(initiator,recipient)==true?.25f:0f;
        public override void Interacted(Pawn initiator,Pawn recipient,List<RulePackDef> extraSentencePacks,out string letterText,out string letterLabel,out LetterDef letterDef,out LookTargets lookTargets)
        {
            letterText=null;letterLabel=null;letterDef=null;lookTargets=null;GameComponent_Renown.Current?.Recognize(initiator,recipient);
        }
    }
    [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetInspectString))]
    public static class InspectPatch
    {
        static void Postfix(Pawn __instance,ref string __result)
        {
            var text=GameComponent_Renown.Current?.Inspect(__instance);if(text!=null)__result+=(__result.NullOrEmpty()?"":"\n")+text;
        }
    }
}
