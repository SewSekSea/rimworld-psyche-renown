using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Renown
{
    public partial class GameComponent_Renown
    {
        ReadScope readScope;
        public IDisposable BeginReadScope()=>new ReadScope(this);
        sealed class ReadScope : IDisposable
        {
            readonly GameComponent_Renown owner;
            readonly ReadScope previous;
            readonly Dictionary<Tuple<Pawn,Pawn>,List<Knowledge>> pairs=new Dictionary<Tuple<Pawn,Pawn>,List<Knowledge>>();
            readonly Dictionary<Pawn,List<Knowledge>> about=new Dictionary<Pawn,List<Knowledge>>();
            readonly Dictionary<Pawn,List<Knowledge>> held=new Dictionary<Pawn,List<Knowledge>>();
            public ReadScope(GameComponent_Renown owner)
            {
                this.owner=owner;previous=owner.readScope;
                var valid=owner.Known.Concat(owner.History).Where(k=>k!=null && k.Witness!=null && !k.Witness.Dead && k.Maker!=null && owner.Evidence(k)!=null).ToList();
                foreach(var group in valid.GroupBy(k=>Tuple.Create(k.Witness,k.Maker)))
                    pairs[group.Key]=group.GroupBy(k=>k.DeedId).Select(g=>g.OrderBy(k=>k.Hops).First())
                        .OrderByDescending(k=>owner.Evidence(k).Tick).ThenByDescending(k=>k.DeedId).Take(HistoryPerPair+1).ToList();
                foreach(var group in valid.GroupBy(k=>k.Maker))
                    about[group.Key]=group.Select(k=>k.Witness).Distinct().Select(p=>pairs[Tuple.Create(p,group.Key)].First()).ToList();
                foreach(var group in valid.GroupBy(k=>k.Witness))
                    held[group.Key]=group.Select(k=>k.Maker).Distinct().Select(p=>pairs[Tuple.Create(group.Key,p)].First()).ToList();
                owner.readScope=this;
            }
            public List<Knowledge> Accounts(Pawn observer,Pawn subject)=>observer!=null && subject!=null && pairs.TryGetValue(Tuple.Create(observer,subject),out var result)?new List<Knowledge>(result):new List<Knowledge>();
            public List<Knowledge> About(Pawn subject)=>subject!=null && about.TryGetValue(subject,out var result)?new List<Knowledge>(result):new List<Knowledge>();
            public List<Knowledge> Held(Pawn observer)=>observer!=null && held.TryGetValue(observer,out var result)?new List<Knowledge>(result):new List<Knowledge>();
            public void Dispose(){if(owner.readScope==this)owner.readScope=previous;}
        }
    }
}
