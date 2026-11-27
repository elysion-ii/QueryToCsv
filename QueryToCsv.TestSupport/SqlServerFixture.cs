using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using DotNet.Testcontainers.Builders;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace QueryToCsv.TestSupport;

// One SQL Server for the test run, started in Docker and removed when the run ends. The
// server presents a certificate created here, and clients trust exactly that certificate
// ("Server Certificate"), the way an operator pins a server certificate: no test needs
// TrustServerCertificate, which the application rejects (docs/specs/QueryToCsv.md, QTC-7).
// Tests query literal values only, so the server needs no schema.
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Pinned by digest so every run tests against the same server build (16.0.4325.4).
    private const string Image =
        "mcr.microsoft.com/mssql/server@sha256:4525e3efac9f3e241cb003bb2ff2a82818b502f37e12049d788b1a9fa7e13b7f";

    private const string CertificateFolder = "/var/opt/mssql/certs";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"QueryToCsvTests_sql_{Guid.NewGuid():N}");
    private MsSqlContainer? _container;

    public string Password { get; } = CreatePassword();

    // The client side of the pinned certificate.
    public string CertificatePath => Path.Combine(_directory, "server.cer");

    public string Server => $"127.0.0.1,{_container!.GetMappedPublicPort(MsSqlBuilder.MsSqlPort)}";

    // Encrypted, certificate-validated, without credentials: the form the application
    // accepts in appsettings.json.
    public string ConnectionStringWithoutCredentials => new SqlConnectionStringBuilder
    {
        DataSource = Server,
        InitialCatalog = "master",
        Encrypt = SqlConnectionEncryptOption.Mandatory,
        ServerCertificate = CertificatePath,
    }.ConnectionString;

    public string ConnectionString => new SqlConnectionStringBuilder(ConnectionStringWithoutCredentials)
    {
        UserID = "sa",
        Password = Password,
    }.ConnectionString;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        using RSA key = RSA.Create(2048);
        CertificateRequest request = new("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        SubjectAlternativeNameBuilder names = new();
        names.AddDnsName("localhost");
        names.AddIpAddress(System.Net.IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false));
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));

        await File.WriteAllBytesAsync(CertificatePath, certificate.Export(X509ContentType.Cert));

        string mssqlConf =
            "[network]\n"
            + $"tlscert = {CertificateFolder}/cert.pem\n"
            + $"tlskey = {CertificateFolder}/key.pem\n"
            + "tlsprotocols = 1.2\n"
            + "forceencryption = 0\n";

        _container = new MsSqlBuilder(Image)
            .WithPassword(Password)
            .WithResourceMapping(Encoding.ASCII.GetBytes(certificate.ExportCertificatePem()), FilePath.Of($"{CertificateFolder}/cert.pem"))
            .WithResourceMapping(Encoding.ASCII.GetBytes(key.ExportPkcs8PrivateKeyPem()), FilePath.Of($"{CertificateFolder}/key.pem"))
            .WithResourceMapping(Encoding.ASCII.GetBytes(mssqlConf), FilePath.Of("/var/opt/mssql/mssql.conf"))
            .Build();

        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync($"Could not remove the test directory {_directory}: {ex.Message}");
        }
    }

    // Meets SQL Server's complexity policy: upper, lower, digit, and symbol characters.
    private static string CreatePassword()
        => "Qtc-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)) + "a1!";
}
