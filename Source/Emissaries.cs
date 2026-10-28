using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Renown
{
    public partial class GameComponent_Renown
    {
        public bool EmissaryEligible(Pawn envoy,Caravan caravan,Settlement settlement)
        {
            if(envoy==null || envoy.Dead || envoy.Downed || envoy.InMentalState || envoy.RaceProps?.Humanlike!=true || envoy.DevelopmentalStage.Juvenile() || envoy.needs?.mood==null || !envoy.IsFreeColonist || envoy.IsPrisoner || envoy.IsSlave || envoy.Spawned)return false;
            if(caravan==null || !caravan.Spawned || caravan.pather.Moving || caravan.Faction!=Faction.OfPlayer || !caravan.PawnsListForReading.Contains(envoy) || envoy.GetCaravan()!=caravan)return false;
            if(settlement==null || !settlement.Spawned || !settlement.Visitable || !caravan.Tile.Valid || !settlement.Tile.Valid || caravan.Tile!=settlement.Tile)return false;
            var faction=settlement.Faction;
            if(faction==null || faction.IsPlayer || faction.Hidden || faction.defeated || faction.HostileTo(Faction.OfPlayer) || faction.leader==null || faction.leader.Dead || Fame(envoy)<30)return false;
            int now=Find.TickManager.TicksGame;
            return Abroad.Any(r=>r!=null && r.Faction==faction && r.Maker==envoy && Policy.Fresh(r.Tick,now) && Deeds.Any(d=>d.Id==r.DeedId && d.Maker==envoy && Policy.Fresh(d.Tick,now)));
        }
    }
}
