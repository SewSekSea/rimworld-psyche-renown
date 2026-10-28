using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Renown
{
    public sealed class EmissaryVisit : IExposable
    {
        public Faction Faction;
        public Settlement TradeSettlement;
        public int TradeUntil;
        public Pawn Envoy;
        public int Tick,DeedId,Requested,Actual;
        public float Chance,Roll;
        public string Outcome="pending";
        public void ExposeData()
        {
            Scribe_References.Look(ref Faction,"faction");Scribe_References.Look(ref Envoy,"envoy",true);
            Scribe_References.Look(ref TradeSettlement,"tradeSettlement");Scribe_Values.Look(ref TradeUntil,"tradeUntil");
            Scribe_Values.Look(ref Tick,"tick");Scribe_Values.Look(ref DeedId,"deed");Scribe_Values.Look(ref Requested,"requested");Scribe_Values.Look(ref Actual,"actual");
            Scribe_Values.Look(ref Chance,"chance");Scribe_Values.Look(ref Roll,"roll");Scribe_Values.Look(ref Outcome,"outcome","pending");
        }
    }
    public static class EmissaryPolicy
    {
        public const int Cooldown=20*Policy.Day;
        public static float Chance(int fame,int social,int influence)=>Mathf.Clamp(.25f+Mathf.Clamp(fame,0,100)*.003f+Mathf.Clamp(social,0,20)*.01f+Mathf.Clamp(influence,0,60)*.002f,.25f,.8f);
    }
    public partial class GameComponent_Renown
    {
        public List<EmissaryVisit> EmissaryVisits=new List<EmissaryVisit>();
        public int EmissaryWait(Faction faction)
        {
            var last=EmissaryVisits.Where(v=>v!=null && v.Faction==faction).OrderByDescending(v=>v.Tick).FirstOrDefault();
            return last==null?0:(int)Math.Min(int.MaxValue,Math.Max(0L,(long)last.Tick+EmissaryPolicy.Cooldown-Find.TickManager.TicksGame));
        }
        public bool CanSendEmissary(Pawn envoy,Caravan caravan,Settlement settlement)=>RenownMod.Settings?.Emissaries==true && EmissaryEligible(envoy,caravan,settlement) && EmissaryWait(settlement.Faction)==0;
        FactionKnowledge EmissaryReport(Pawn envoy,Faction faction)
        {
            int now=Find.TickManager.TicksGame;
            return Abroad.Where(r=>r!=null && r.Maker==envoy && r.Faction==faction && Policy.Fresh(r.Tick,now) && Deeds.Any(d=>d.Id==r.DeedId && d.Maker==envoy && Policy.Fresh(d.Tick,now))).OrderByDescending(r=>r.Tick).ThenByDescending(r=>r.DeedId).FirstOrDefault();
        }
        public float EmissaryChance(Pawn envoy,Faction faction)
        {
            var report=EmissaryReport(envoy,faction);if(report==null)return 0;
            var deed=Deeds.First(d=>d.Id==report.DeedId && d.Maker==envoy);
            if(deed.Kind=="combat" && deed.Subject?.Faction==faction || deed.Kind=="execution" && deed.SubjectFaction==faction)return 0;
            var social=envoy.skills?.GetSkill(SkillDefOf.Social);
            return EmissaryPolicy.Chance(Fame(envoy),social==null || social.TotallyDisabled?0:social.Level,Influence(envoy,faction.leader));
        }
        public EmissaryVisit SendEmissary(Pawn envoy,Caravan caravan,Settlement settlement)
        {
            if(!CanSendEmissary(envoy,caravan,settlement))return null;
            var faction=settlement.Faction;var report=EmissaryReport(envoy,faction);
            var visit=new EmissaryVisit{Faction=faction,Envoy=envoy,Tick=Find.TickManager.TicksGame,DeedId=report.DeedId,Chance=EmissaryChance(envoy,faction)};
            EmissaryVisits.RemoveAll(v=>v==null || v.Faction==faction);EmissaryVisits.Add(visit);
            visit.Roll=Rand.Value;visit.Requested=visit.Roll<visit.Chance?3:0;
            int before=faction.GoodwillWith(Faction.OfPlayer);
            try
            {
                while(visit.Requested>0 && Faction.OfPlayer.CalculateAdjustedGoodwillChange(faction,visit.Requested)>3)visit.Requested--;
                if(visit.Requested>0)Faction.OfPlayer.TryAffectGoodwillWith(faction,visit.Requested,false,false);
                visit.Actual=faction.GoodwillWith(Faction.OfPlayer)-before;
                visit.Outcome=visit.Actual>0?"well received":visit.Requested>0?"no goodwill change":"politely declined";
                if(visit.Requested>0 && RenownMod.Settings.EmissaryTrading && visit.Tick<=int.MaxValue-EmissaryTradingPolicy.Duration){visit.TradeSettlement=settlement;visit.TradeUntil=visit.Tick+EmissaryTradingPolicy.Duration;}
            }
            catch(Exception e){visit.Actual=faction.GoodwillWith(Faction.OfPlayer)-before;visit.Outcome="native diplomacy interrupted";Log.Warning("[Renown] Emissary outcome interrupted: "+e.GetType().Name);}
            string detail=envoy.LabelShort+" visited "+settlement.Label+": "+visit.Outcome+". Goodwill change "+visit.Actual.ToString("+0;-0;0")+". Chance "+visit.Chance.ToString("P0")+"; draw "+visit.Roll.ToString("F4")+". Another visit to this faction is available in twenty days. "+(visit.TradeUntil>0?"Two-day goods discount earned here for this envoy; native price floors apply.":"No trade discount earned.")+" No military aid granted.";
            Messages.Message(detail,caravan,visit.Actual>0?MessageTypeDefOf.PositiveEvent:MessageTypeDefOf.NeutralEvent);
            return visit;
        }
        public Dialog_MessageBox EmissaryConfirmation(Pawn envoy,Caravan caravan,Settlement settlement)
        {
            return Dialog_MessageBox.CreateConfirmation("Send "+envoy.LabelShort+" as an emissary to "+settlement.Label+"?\n\nCurrent chance: "+EmissaryChance(envoy,settlement.Faction).ToString("P0")+". A well-received visit requests +3 goodwill, capped by native rules; otherwise no change. Either result uses this faction's twenty-day cooldown. "+(RenownMod.Settings.EmissaryTrading?"Success also earns up to 5% off goods bought at this settlement by this envoy for two days. Silver, pawns and selling prices are excluded; native floors apply. ":"Trade discounts are disabled. ")+"No automatic travel or military aid. Eligibility, settings and chance are checked again when you confirm.",()=>{if(SendEmissary(envoy,caravan,settlement)==null)Messages.Message("The emissary visit is no longer available. No attempt was spent.",MessageTypeDefOf.RejectInput,false);},title:"Renown emissary");
        }
        public Command_Action EmissaryCommand(Caravan caravan)
        {
            if(RenownMod.Settings?.Emissaries!=true || caravan==null || !caravan.Spawned || caravan.Faction!=Faction.OfPlayer)return null;
            var settlement=Find.WorldObjects.SettlementBases.Where(s=>s.Spawned && s.Tile==caravan.Tile && s.Faction!=Faction.OfPlayer).OrderBy(s=>s.ID).FirstOrDefault();
            if(settlement==null)return null;
            var command=new Command_Action{defaultLabel="Emissary visit",defaultDesc="Choose an eligible caravan member for a reputation-backed diplomatic visit. One attempt per faction every twenty days.",icon=BaseContent.BadTex};
            var last=EmissaryVisits.FirstOrDefault(v=>v!=null && v.Faction==settlement.Faction);
            if(last!=null)command.defaultDesc+="\n\nLast visit: "+(last.Envoy?.LabelShort??"former envoy")+" - "+last.Outcome+". Actual goodwill "+last.Actual.ToString("+0;-0;0")+". Chance "+last.Chance.ToString("P0")+"; draw "+last.Roll.ToString("F4")+".";
            if(last?.TradeSettlement==settlement && last.TradeUntil>Find.TickManager.TicksGame)command.defaultDesc+="\nEarned goods discount: "+((last.TradeUntil-Find.TickManager.TicksGame)/(float)Policy.Day).ToString("0.0")+" days left for this envoy here. Requires both emissary settings enabled.";
            command.icon=ContentFinder<Texture2D>.Get("UI/Commands/Trade",false)??BaseContent.WhiteTex;
            command.action=()=>
            {
                var options=caravan.PawnsListForReading.Where(p=>CanSendEmissary(p,caravan,settlement)).OrderBy(p=>p.thingIDNumber).Select(p=>new FloatMenuOption(p.LabelShort+" | "+EmissaryChance(p,settlement.Faction).ToString("P0"),()=>Find.WindowStack.Add(EmissaryConfirmation(p,caravan,settlement)))).ToList();
                if(options.Count==0){Messages.Message("No eligible envoy: check arrival, Fame30, fresh faction evidence and the twenty-day cooldown.",MessageTypeDefOf.RejectInput,false);return;}
                Find.WindowStack.Add(new FloatMenu(options));
            };
            int wait=EmissaryWait(settlement.Faction);
            if(wait>0)command.Disable("This faction will receive another emissary in "+(wait/(float)Policy.Day).ToString("0.0")+" days.");
            else if(!caravan.PawnsListForReading.Any(p=>CanSendEmissary(p,caravan,settlement)))command.Disable("Stop at this non-hostile settlement. Requires an able adult free colonist with Fame30 and fresh evidence already held by this faction.");
            return command;
        }
        void ExposeEmissaries()
        {
            Scribe_Collections.Look(ref EmissaryVisits,"renownEmissaryVisits",LookMode.Deep);
            if(Scribe.mode==LoadSaveMode.PostLoadInit){EmissaryVisits=EmissaryVisits??new List<EmissaryVisit>();EmissaryVisits=EmissaryVisits.Where(v=>v!=null && v.Faction!=null).GroupBy(v=>v.Faction).Select(g=>g.OrderByDescending(v=>v.Tick).First()).ToList();}
        }
    }
    [HarmonyPatch(typeof(Caravan),nameof(Caravan.GetGizmos))]
    public static class EmissaryGizmos
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Caravan __instance)
        {
            foreach(var gizmo in __result)yield return gizmo;
            var command=GameComponent_Renown.Current?.EmissaryCommand(__instance);if(command!=null)yield return command;
            var aid=GameComponent_Renown.Current?.MilitaryAidCommand(__instance);if(aid!=null)yield return aid;
        }
    }
}
