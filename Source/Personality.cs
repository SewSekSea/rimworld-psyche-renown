using System;

namespace Renown
{
    public static class PersonalityAppraisal
    {
        // Bounded game interpretation, not a diagnosis. Personality cannot create missing evidence or reverse hostility.
        public static Appraisal Apply(Appraisal appraisal,string kind,bool kindTrait,bool psychopath,bool sociopath,bool narcissist,bool machiavellian)
        {
            if(appraisal==null)return new Appraisal{Reason="No appraisal available."};
            int percent=100,distrustPercent=100;string reason="";
            if(kind=="rescue" || kind=="medical")
            {
                // More detached reactions take precedence if another mod allows otherwise conflicting traits.
                if(psychopath){percent=50;reason="Psychopath: "+kind+" admiration weighted 50%.";}
                else if(sociopath){percent=75;reason="Sociopath: "+kind+" admiration weighted 75%.";}
                else if(kindTrait){percent=125;reason="Kind: "+kind+" admiration weighted 125%.";}
            }
            else if(kind=="craft")
            {
                // Contrasting competitive/pragmatic interpretations cancel rather than stack.
                if(narcissist && !machiavellian){percent=75;reason="Narcissist: others' achievement admiration weighted 75%.";}
                else if(machiavellian && !narcissist){percent=125;reason="Machiavellian: useful achievement admiration weighted 125%.";}
            }
            else if(kind=="execution" && kindTrait && !psychopath && !sociopath)
            {distrustPercent=125;reason="Kind: execution distrust weighted 125%; no admiration added.";}
            appraisal.Admiration=Math.Min(40,Math.Max(0,appraisal.Admiration)*percent/100);
            if(distrustPercent!=100)appraisal.Distrust=Math.Min(40,Math.Max(0,appraisal.Distrust)*distrustPercent/100);
            appraisal.Personality=reason;
            if(reason!="")appraisal.Reason+=" "+reason+" Existing evidence and relationship gates remain; threat is unchanged."+(distrustPercent==100?" Distrust is unchanged.":" Distrust remains capped at 40.");
            return appraisal;
        }
    }
}
