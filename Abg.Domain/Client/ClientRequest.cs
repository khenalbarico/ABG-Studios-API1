using Abg.Domain.PolicyForms;
using static Abg.Domain.Constants;

namespace Abg.Domain.Client;

public sealed class ClientRequest
{
    public ClientInformation   ClientInformation { get; set; } = new();
    public List<ClientService> ClientServices    { get; set; } = [];
    public ConsentModel        ClientConsent     { get; set; } = new();
    public ClientStatus        Status            { get; set; } = ClientStatus.Pending;
}
