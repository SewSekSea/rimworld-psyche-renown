using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Renown
{
    public partial class GameComponent_Renown
    {
        public const int HistoryPerPair=7,HistoryLimit=4096;
        public List<Knowledge> History=new List<Knowledge>();
        public void Archive(Knowledge account)
        {
            if(account==null || account.Witness==null || account.Maker==null || Evidence(account)==null)return;
            var existing=History.FirstOrDefault(k=>k.Witness==account.Witness && k.Maker==account.Maker && k.DeedId==account.DeedId);
            if(existing!=null && existing.Hops<=account.Hops)return;
            if(existing!=null)History.Remove(existing);
            History.Add(new Knowledge {Witness=account.Witness,Maker=account.Maker,DeedId=account.DeedId,Source=account.Source,
                Hops=account.Hops,LearnedAt=account.LearnedAt,LastRecognition=account.LastRecognition,LastShared=account.LastShared,LastPersuaded=account.LastPersuaded});
        }
        public List<Knowledge> Accounts(Pawn observer,Pawn subject)
        {
            if(readScope!=null)return readScope.Accounts(observer,subject);
            if(observer==null || observer.Dead || subject==null)return new List<Knowledge>();
            return Known.Concat(History).Where(k=>k!=null && k.Witness==observer && k.Maker==subject && Evidence(k)!=null)
                .GroupBy(k=>k.DeedId).Select(g=>g.OrderBy(k=>k.Hops).First())
                .OrderByDescending(k=>Evidence(k).Tick).ThenByDescending(k=>k.DeedId).Take(HistoryPerPair+1).ToList();
        }
        public List<Knowledge> AccountsAbout(Pawn subject)=>readScope!=null?readScope.About(subject):Known.Concat(History)
            .Where(k=>k!=null && k.Maker==subject && k.Witness!=null && !k.Witness.Dead && Evidence(k)!=null)
            .Select(k=>k.Witness).Distinct().Select(p=>Accounts(p,subject).First()).ToList();
        public List<Knowledge> AccountsHeld(Pawn observer)=>readScope!=null?readScope.Held(observer):Known.Concat(History)
            .Where(k=>k!=null && k.Witness==observer && k.Maker!=null && Evidence(k)!=null)
            .Select(k=>k.Maker).Distinct().Select(p=>Accounts(observer,p).FirstOrDefault()).Where(k=>k!=null).ToList();
        public string HistoryText(Pawn observer,Pawn subject)
        {
            var accounts=Accounts(observer,subject);int now=Find.TickManager.TicksGame;
            return "RETAINED ACCOUNTS ("+accounts.Count+"/8)\n"+string.Join("\n",accounts.Select(k=>
            {
                var deed=Evidence(k);
                return deed.ProductLabel+" | "+(Math.Max(0,now-deed.Tick)/60000f).ToString("0.0")+" days ago | "+
                    (k.Hops==0?"direct witness":"heard from "+(k.Source?.LabelShort??"unavailable")+"; transmissions "+k.Hops);
            }))+"\nEach deed counted once. Up to eight accounts per pair, subject to global limits and 30-day expiry. No lost pre-update history is reconstructed.";
        }
        void PruneHistory()
        {
            History=History.Where(k=>k!=null && k.Witness!=null && !k.Witness.Dead && k.Maker!=null && Evidence(k)!=null)
                .GroupBy(k=>new {k.Witness,k.Maker}).SelectMany(g=>g.GroupBy(k=>k.DeedId).Select(d=>d.OrderBy(k=>k.Hops).First())
                    .OrderByDescending(k=>Evidence(k).Tick).ThenByDescending(k=>k.DeedId).Take(HistoryPerPair))
                .OrderByDescending(k=>k.LearnedAt).Take(HistoryLimit).ToList();
        }
        void ExposeHistory()
        {
            Scribe_Collections.Look(ref History,"renownAccountHistory",LookMode.Deep);
            if(Scribe.mode==LoadSaveMode.PostLoadInit && History==null)History=new List<Knowledge>();
        }
    }
}
