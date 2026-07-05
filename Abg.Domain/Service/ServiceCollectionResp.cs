namespace Abg.Domain.Service;

public sealed class ServiceCollectionResp
{
    public List<NailsService>    Nails    { get; set; } = [];
    public List<LashesService>   Lashes   { get; set; } = [];
    public List<EyebrowsService> Eyebrows { get; set; } = [];
    public List<FootspaService>  Footspa  { get; set; } = [];
    public List<PedicureService> Pedicure { get; set; } = [];
}
