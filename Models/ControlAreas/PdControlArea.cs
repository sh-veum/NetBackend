using NetBackend.Models.History;

namespace NetBackend.Models.ControlAreas;

public class PdControlArea
{
    public int Id { get; set; }
    public string? ForskNr { get; set; }
    public string? ForskNavn { get; set; }
    public string? ForskLink { get; set; }
    public DateTime? SistEndret { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? Version { get; set; }
    public DateTime? OriginalDate { get; set; }

    // Separate collections for the different types of links
    public List<PdSurveillanceAreaLink> PdSurveillanceAreaLinks { get; set; } = new List<PdSurveillanceAreaLink>();
    public List<PdProtectionAreaLink> PdProtectionAreaLinks { get; set; } = new List<PdProtectionAreaLink>();
}