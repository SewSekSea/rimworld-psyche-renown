using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Renown
{
    public partial class GameComponent_Renown
    {
        public bool ContractEvidenceEligible(FactionKnowledge report,Map map)
        {
            if(report==null || map==null || !map.IsPlayerHome || !Person(report.Maker) || !report.Maker.IsFreeColonist || report.Maker.Map!=map || Fame(report.Maker)<50)return false;
            int now=Find.TickManager.TicksGame;var faction=report.Faction;
            return faction!=null && !faction.IsPlayer && !faction.Hidden && !faction.defeated && !faction.HostileTo(Faction.OfPlayer) &&
                Policy.Fresh(report.Tick,now) && Deeds.Any(d=>d.Id==report.DeedId && d.Maker==report.Maker && Policy.Fresh(d.Tick,now));
        }
        public bool ContractSettlementEligible(FactionKnowledge report,Map map,Settlement settlement)
        {
            if(!ContractEvidenceEligible(report,map) || settlement==null || !settlement.Spawned || settlement.Faction!=report.Faction || !settlement.Visitable || settlement.Faction.leader==null)return false;
            if(!map.Tile.Valid || !settlement.Tile.Valid || map.Tile.Layer!=settlement.Tile.Layer || !map.Tile.LayerDef.SurfaceTiles)return false;
            var request=settlement.GetComponent<TradeRequestComp>();
            if(request==null || request.ActiveRequest || Find.WorldGrid.ApproxDistanceInTiles(map.Tile,settlement.Tile)>=32 || !Find.WorldReachability.CanReach(map.Tile,settlement.Tile))return false;
            return !Find.QuestManager.QuestsListForReading.Any(q=>!q.Historical && q.PartsListForReading.OfType<QuestPart_InitiateTradeRequest>().Any(p=>p.settlement==settlement));
        }
        public Settlement ContractSettlement(FactionKnowledge report,Map map)
        {
            if(!ContractEvidenceEligible(report,map))return null;
            return Find.WorldObjects.SettlementBases.Where(s=>ContractSettlementEligible(report,map,s)).OrderBy(s=>Find.WorldGrid.ApproxDistanceInTiles(map.Tile,s.Tile)).ThenBy(s=>s.ID).FirstOrDefault();
        }
    }
}
