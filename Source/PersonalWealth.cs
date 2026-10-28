using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Renown
{
    public partial class GameComponent_Renown
    {
        Dictionary<Pawn,int> wealthObserved=new Dictionary<Pawn,int>();
        int wealthCursor;
        List<Pawn> wealthObservedKeys;
        List<int> wealthObservedValues;
        public float PersonalEquipmentValue(Pawn pawn)
        {
            if(!Person(pawn) || !pawn.IsFreeColonist)return 0;
            var items=(pawn.equipment?.AllEquipmentListForReading.Cast<Thing>()??Enumerable.Empty<Thing>()).Concat(pawn.apparel?.WornApparel.Cast<Thing>()??Enumerable.Empty<Thing>());
            double value=items.Sum(t=>(double)t.GetStatValue(StatDefOf.MarketValue));
            return double.IsNaN(value) || double.IsInfinity(value) || value<0?0:(float)Math.Min(1000000,value);
        }
        public Room PersonalWealthRoom(Pawn pawn)
        {
            var bed=pawn?.ownership?.OwnedBed;var room=bed?.GetRoom();
            return bed!=null && bed.Spawned && !bed.ForPrisoners && bed.OwnersForReading.Count()==1 && room!=null && room.Role==RoomRoleDefOf.Bedroom && pawn.GetRoom()==room?room:null;
        }
        public bool ObservePersonalWealth(Pawn pawn)
        {
            if(RenownMod.Settings?.WealthPrestige!=true || !Person(pawn) || !pawn.IsFreeColonist)return false;
            int now=Find.TickManager.TicksGame;
            if(wealthObserved.TryGetValue(pawn,out int last) && now-last<30*Policy.Day)return false;
            foreach(var old in wealthObserved.Where(p=>p.Key==null || p.Key.Dead || now-p.Value>=30*Policy.Day).Select(p=>p.Key).ToArray())wealthObserved.Remove(old);
            if(wealthObserved.Count>=256)return false;
            float gear=PersonalEquipmentValue(pawn);var room=PersonalWealthRoom(pawn);
            float quarters=room?.GetStat(RoomStatDefOf.Wealth)??0;
            if(float.IsNaN(quarters) || float.IsInfinity(quarters) || quarters<0)quarters=0;
            var witnesses=pawn.Map.mapPawns.AllPawnsSpawned.Where(p=>Sees(p,pawn) && gear+(room!=null && p.GetRoom()==room?quarters:0)>=2000).ToList();
            if(witnesses.Count==0)return false;
            wealthObserved[pawn]=now;
            var deed=new Deed{Id=nextId++,Kind="wealth",Maker=pawn,Map=pawn.Map,Tick=now,ProductId="wealth:"+pawn.ThingID+":"+now,ProductLabel="displaying valuable personal equipment or private quarters",Witnesses=witnesses};
            Deeds.Add(deed);
            foreach(var witness in witnesses)
            {
                var known=Known.FirstOrDefault(k=>k.Witness==witness && k.Maker==pawn);
                if(known==null)Known.Add(new Knowledge{Witness=witness,Maker=pawn,DeedId=deed.Id,LearnedAt=now});
                else{Archive(known);known.DeedId=deed.Id;known.Source=null;known.Hops=0;known.LearnedAt=now;}
                DisplayBridge.Record(witness.LabelShort+" noticed "+pawn.LabelShort+"'s valuable personal possessions.","At least2000 market value in worn/equipped items and, only when both pawns are inside, a solely assigned private bedroom. No inventory or colony stockpile credit. Recognition is not earned merit. One observation per pawn per thirty days.",witness,pawn,pawn.Map);
            }
            Prune();return true;
        }
        void RefreshPersonalWealth()
        {
            if(RenownMod.Settings?.WealthPrestige!=true)return;
            var pawns=Find.Maps.SelectMany(m=>m.mapPawns.FreeColonistsSpawned).OrderBy(p=>p.thingIDNumber).ToArray();
            if(pawns.Length==0)return;
            for(int i=0;i<Math.Min(8,pawns.Length);i++){wealthCursor=(wealthCursor%pawns.Length+pawns.Length)%pawns.Length;ObservePersonalWealth(pawns[wealthCursor]);wealthCursor++;}
        }
        void ExposePersonalWealth()
        {
            Scribe_Collections.Look(ref wealthObserved,"renownWealthObserved",LookMode.Reference,LookMode.Value,ref wealthObservedKeys,ref wealthObservedValues);Scribe_Values.Look(ref wealthCursor,"renownWealthCursor");
            if(Scribe.mode==LoadSaveMode.PostLoadInit)wealthObserved=(wealthObserved??new Dictionary<Pawn,int>()).Where(p=>p.Key!=null).Take(256).ToDictionary(p=>p.Key,p=>p.Value);
        }
    }
}
