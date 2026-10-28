using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Renown
{
    public static class EmissaryTradingPolicy
    {
        public const int Duration=2*Policy.Day;
        public static float Price(float buy,float sell)
        {
            if(float.IsNaN(buy) || float.IsInfinity(buy) || float.IsNaN(sell) || float.IsInfinity(sell) || buy<=0 || sell>=buy)return buy;
            return Math.Min(buy,Math.Max(.5f,Math.Max(sell,buy*.95f)));
        }
    }
    public partial class GameComponent_Renown
    {
        public EmissaryVisit TradingPrivilege(Pawn envoy,Settlement settlement)
        {
            if(RenownMod.Settings?.Emissaries!=true || !RenownMod.Settings.EmissaryTrading || envoy==null || envoy.Dead || envoy.Downed || envoy.InMentalState || !envoy.IsFreeColonist || envoy.IsPrisoner || envoy.IsSlave || envoy.DevelopmentalStage.Juvenile())return null;
            var caravan=envoy.GetCaravan();
            if(caravan==null || !caravan.Spawned || caravan.pather.Moving || caravan.Faction!=Faction.OfPlayer || settlement==null || !settlement.Spawned || !settlement.Visitable || settlement.Faction==null || settlement.Faction.defeated || settlement.Faction.HostileTo(Faction.OfPlayer) || caravan.Tile!=settlement.Tile)return null;
            int now=Find.TickManager.TicksGame;
            return EmissaryVisits.FirstOrDefault(v=>v!=null && v.Envoy==envoy && v.TradeSettlement==settlement && v.Faction==settlement.Faction && v.Tick<=now && v.TradeUntil>now && (long)v.TradeUntil-v.Tick==EmissaryTradingPolicy.Duration);
        }
        public bool DiscountApplies(Tradeable item,TradeAction action)
        {
            if(action!=TradeAction.PlayerBuys || item?.AnyThing==null || item.AnyThing is Pawn || item.ThingDef==ThingDefOf.Silver || TradeSession.giftMode || !(TradeSession.trader is Settlement settlement) || settlement.TradeCurrency!=TradeCurrency.Silver)return false;
            return TradingPrivilege(TradeSession.playerNegotiator,settlement)!=null;
        }
    }
    [HarmonyPatch(typeof(Tradeable),nameof(Tradeable.GetPriceFor))]
    public static class EmissaryPrice
    {
        static void Postfix(Tradeable __instance,TradeAction action,ref float __result,bool __runOriginal)
        {
            if(__runOriginal && GameComponent_Renown.Current?.DiscountApplies(__instance,action)==true)__result=EmissaryTradingPolicy.Price(__result,__instance.GetPriceFor(TradeAction.PlayerSells));
        }
    }
    [HarmonyPatch(typeof(Tradeable),nameof(Tradeable.GetPriceTooltip))]
    public static class EmissaryPriceExplanation
    {
        static void Postfix(Tradeable __instance,TradeAction action,ref string __result,bool __runOriginal)
        {
            if(__runOriginal && GameComponent_Renown.Current?.DiscountApplies(__instance,action)==true)__result+="\nRenown emissary terms: up to 5% off goods bought here by this envoy. Minimum price and selling-price floor apply. Selling prices are unchanged. Ends two days after the successful visit.";
        }
    }
}
