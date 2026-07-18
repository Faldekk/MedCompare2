namespace DrugCompare.Application.Models.Rag;

public enum QueryIntent
{
    Unknown,
    Interaction,
    Contraindication,
    Warning,
    PregnancyLactation,
    AdverseReaction,
    Posology,
    RenalImpairment,
    HepaticImpairment,
    Pharmacokinetics,
    Overdose,
    Excipient
}
