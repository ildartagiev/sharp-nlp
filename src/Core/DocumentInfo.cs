namespace SharpNlp.Core;

public class DocumentInfo
{
    public const string LABEL_PERSON = "PERSON";
    public const string LABEL_ORGANISATION = "ORGANISATION";
    public const string LABEL_DATE = "DATE";
    public const string LABEL_MINERALDEPOSIT = "MINERALDEPOSIT";
    public const string LABEL_RESERVOIR = "RESERVOIR";
    public const string LABEL_NPT = "NPT";

    public List<string>? PERSON { get; set; }

    public List<string>? ORGANISATION { get; set; }

    public List<string>? DATE { get; set; }

    public List<string>? MINERALDEPOSIT { get; set; }

    public List<string>? RESERVOIR { get; set; }

    public List<string>? NPT { get; set; }
}
