using System;
using System.Linq;
using RimWorld;
using Verse;

namespace Renown
{
    public sealed class Appraisal
    {
        public int Admiration,Threat,Distrust;
        public string Reason;
        public string Personality="";
        public string Summary=>"Admiration "+Admiration+" | Perceived threat "+Threat+" | Distrust "+Distrust;
    }
    public static class AppraisalPolicy
    {
        // A transparent interpretation of one known deed, not a diagnosis or a universal moral score.
        public static Appraisal Evaluate(string kind,int opinion,bool hostile,bool harmedOwnFaction,int hops)
        {
            var result=new Appraisal();
            if(hops<0 || hops>3 || (kind!="craft" && kind!="rescue" && kind!="combat" && kind!="medical" && kind!="execution" && kind!="wealth" && kind!="speech" && kind!="duel"))
            {result.Reason="No supported account to assess.";return result;}
            int confidence=hops==0?100:hops==1?60:hops==2?40:25;
            if(kind!="execution" && !hostile && opinion>=0 && !harmedOwnFaction)
                result.Admiration=(kind=="wealth"?5:kind=="rescue"?30:kind=="craft" || kind=="medical"?20:15)*confidence/100;
            if(kind=="combat" && (hostile || harmedOwnFaction))result.Threat=40*confidence/100;
            if(kind=="execution"){result.Threat=(hostile || harmedOwnFaction?40:20)*confidence/100;result.Distrust=10*confidence/100;}
            if(opinion<0)result.Distrust=Math.Max(result.Distrust,Math.Min(40,-Math.Max(-100,opinion)/2)*confidence/100);
            if(hostile || harmedOwnFaction)result.Distrust=Math.Max(result.Distrust,30*confidence/100);
            result.Reason=(hops==0?"Direct evidence":"Hearsay, transmission "+hops)+"; confidence weight "+confidence+"%. "+
                (hostile?"Observer is hostile. ":"")+(harmedOwnFaction?"The victim belonged to the observer's faction. ":"")+"Current opinion: "+opinion+
                ". These values interpret the latest known deed, not all past behaviour. Threat is not a fear mood or forced retreat; distrust is not proof of dishonesty.";
            return result;
        }
    }
    public partial class GameComponent_Renown
    {
        public Appraisal AssessAccount(Knowledge knowledge)
        {
            var observer=knowledge?.Witness;var subject=knowledge?.Maker;var deed=Evidence(knowledge);
            if(observer==null || subject==null || observer==subject || observer.Dead || deed==null)
                return new Appraisal{Reason="No fresh valid account held by a living observer."};
            var victimFaction=deed.Kind=="execution"?deed.SubjectFaction:deed.Subject?.Faction;
            bool ownFaction=(deed.Kind=="combat" || deed.Kind=="execution") && victimFaction!=null && victimFaction==observer.Faction;
            var appraisal=AppraisalPolicy.Evaluate(deed.Kind,observer.relations?.OpinionOf(subject)??0,observer.HostileTo(subject),ownFaction,knowledge.Hops);
            return PersonalityAppraisal.Apply(appraisal,deed.Kind,SocialContext.Trait(observer,"Kind"),SocialContext.Trait(observer,"Psychopath"),
                SocialContext.Trait(observer,"SP_Sociopath"),SocialContext.Trait(observer,"NR_Narcissist"),SocialContext.Trait(observer,"MAC_Machiavellian"));
        }
        public Appraisal Assess(Pawn observer,Pawn subject)
        {
            var accounts=Accounts(observer,subject);
            if(accounts.Count==0)return new Appraisal{Reason="No fresh valid accounts held by a living observer."};
            var views=accounts.Select(AssessAccount).ToArray();
            return new Appraisal {Admiration=(int)views.Average(v=>v.Admiration),Threat=views.Max(v=>v.Threat),Distrust=views.Max(v=>v.Distrust),
                Reason="Based on "+views.Length+" distinct retained accounts. Admiration is averaged; threat and distrust retain the strongest evidence until it expires or is evicted by history limits. Current relationships still affect interpretation. "+
                    string.Join(" ",views.Select(v=>v.Personality).Where(p=>!string.IsNullOrEmpty(p)).Distinct())+
                    " No automatic fear mood, obedience or proof of dishonesty."};
        }
        public string AppraisalText(Pawn observer,Pawn subject)
        {
            var appraisal=Assess(observer,subject);
            return appraisal.Summary+"\n"+appraisal.Reason;
        }
        public string ReputationAudience(Pawn subject)
        {
            var audience=AccountsAbout(subject).Select(k=>k.Witness).ToArray();
            var views=audience.Select(p=>Assess(p,subject)).ToArray();
            return "Informed audience: "+audience.Length+"; admiring: "+views.Count(v=>v.Admiration>0)+"; threatened: "+views.Count(v=>v.Threat>0)+"; distrustful: "+views.Count(v=>v.Distrust>0)+". Reactions may overlap.";
        }
    }
}
