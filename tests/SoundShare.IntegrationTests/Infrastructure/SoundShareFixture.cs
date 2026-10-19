// Shared environment: Oracle in Testcontainers with project migrations, WireMock instead of YouTube, in-memory API host.
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Mvc.Testing;
using Oracle.ManagedDataAccess.Client;
using WireMock.Server;

namespace SoundShare.IntegrationTests.Infrastructure;

public sealed class SoundShareFixture : IAsyncLifetime
{
    private const string OracleImage = "gvenzl/oracle-free:23-slim";

    private readonly string _systemPassword = Secret("Sys");
    private readonly string _ownerPassword = Secret("Own");
    private readonly string _appPassword = Secret("App");
    private IContainer _oracle = null!;

    public WireMockServer YouTube { get; private set; } = null!;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public string OwnerConnectionString { get; private set; } = null!;

    public string AppConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _oracle = new ContainerBuilder(OracleImage)
            .WithEnvironment("ORACLE_PASSWORD", _systemPassword)
            .WithPortBinding(FreeTcpPort(), 1521)
            .WithResourceMapping(new DirectoryInfo(Path.Combine(RepositoryRoot(), "db")), "/tmp/db/")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("DATABASE IS READY TO USE!"))
            .Build();
        await _oracle.StartAsync();

        await ExecAsync(
            "sed '/^ACCEPT/d' /tmp/db/bootstrap/01_create_users.sql > /tmp/boot.sql"
            + $" && printf 'DEFINE owner_password={_ownerPassword}\\nDEFINE app_password={_appPassword}\\n@/tmp/boot.sql\\n' > /tmp/run_boot.sql"
            + $" && sqlplus -S -L system/{_systemPassword}@//localhost:1521/FREEPDB1 @/tmp/run_boot.sql");
        await ExecAsync($"cd /tmp/db && sqlplus -S -L soundshare/{_ownerPassword}@//localhost:1521/FREEPDB1 @apply_migrations.sql");

        var dataSource = $"{_oracle.Hostname}:{_oracle.GetMappedPublicPort(1521)}/FREEPDB1";
        OwnerConnectionString = $"User Id=SOUNDSHARE;Password={_ownerPassword};Data Source={dataSource}";
        AppConnectionString = $"User Id=SOUNDSHARE_APP;Password={_appPassword};Data Source={dataSource}";

        YouTube = WireMockServer.Start();
        Environment.SetEnvironmentVariable("ConnectionStrings__SoundShare", AppConnectionString);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", Secret("Key") + Secret("Key") + Secret("Key"));
        Environment.SetEnvironmentVariable("YouTube__ApiKey", "test-key");
        Environment.SetEnvironmentVariable("YouTube__BaseUrl", $"{YouTube.Url}/youtube/v3/");
        Environment.SetEnvironmentVariable("RateLimiting__LoginPermitLimit", "100000");
        Factory = new WebApplicationFactory<Program>();
        _ = Factory.Server;
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        YouTube?.Stop();
        if (_oracle is not null)
        {
            await _oracle.DisposeAsync();
        }
    }

    public async Task<int> ExecuteOwnerSqlAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new OracleConnection(OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.Add(new OracleParameter(name, value));
        }

        return await command.ExecuteNonQueryAsync();
    }

    private async Task ExecAsync(string script)
    {
        var result = await _oracle.ExecAsync(["bash", "-c", "export NLS_LANG=AMERICAN_AMERICA.AL32UTF8; " + script]);
        if (result.ExitCode != 0 || result.Stdout.Contains("ORA-", StringComparison.Ordinal) || result.Stdout.Contains("SP2-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Database setup failed ({result.ExitCode}): {result.Stdout} {result.Stderr}");
        }
    }

    private static int FreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string Secret(string prefix) => prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SoundShare.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}

[CollectionDefinition(Name)]
public sealed class SoundShareTestGroup : ICollectionFixture<SoundShareFixture>
{
    public const string Name = "SoundShare";
}
