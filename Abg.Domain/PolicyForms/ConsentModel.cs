namespace Abg.Domain.PolicyForms;

public sealed class ConsentModel
{
    public bool      NoRecentProcedure           { get; set; }
    public bool      WillArriveClean             { get; set; }
    public bool      SensitiveEyes               { get; set; }
    public bool      SensitiveSkin               { get; set; }
    public bool      HistoryOfAllergicReactions  { get; set; }
    public bool      ChemicalAllergies           { get; set; }
    public bool      CurrentEyeIssue             { get; set; }
    public bool      RecentEyeProcedure          { get; set; }
    public bool      WearingContactsToday        { get; set; }
    public bool      NoneOfTheAbove              { get; set; }
    public bool      ProceedAtOwnRisk            { get; set; }
    public bool      RequestPatchTest            { get; set; }
    public bool      AllowPhotos                 { get; set; }
    public bool      DisallowPhotos              { get; set; }
    public bool      ReadAndUnderstood           { get; set; }
    public bool      InformationIsTrue           { get; set; }
    public bool      AgreeToProceed              { get; set; }
    public bool      NailsRulesReadAndUnderstood { get; set; }
    public bool      NailsRulesAgreed            { get; set; }
    public DateTime? NailsRulesAgreedAt          { get; set; }
}
