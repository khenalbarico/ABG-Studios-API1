using Azure.Data.Tables;
using System.Net.Sockets;

namespace Abg.Data.Tests.TestSupport;

/// <summary>
/// Connects table-store tests to a locally running Azurite emulator.
/// Tests skip when Azurite is not listening on the table endpoint (port 10002).
/// </summary>
public sealed class AzuriteFixture
{
    public const string ConnectionString = "UseDevelopmentStorage=true";

    public bool IsAvailable { get; }

    public AzuriteFixture()
    {
        try
        {
            using var tcp = new TcpClient();
            IsAvailable = tcp.ConnectAsync("127.0.0.1", 10002).Wait(TimeSpan.FromMilliseconds(1500));
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public TableServiceClient CreateClient()
    {
        Skip.IfNot(IsAvailable, "Azurite is not running on 127.0.0.1:10002.");

        return new TableServiceClient(ConnectionString);
    }

    public static string UniqueTableName() => $"T{Guid.NewGuid():N}";
}
