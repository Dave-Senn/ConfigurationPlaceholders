using System.Globalization;
using System.Net.NetworkInformation;
using ConfigurationPlaceholders;
using ModernWebApi;

var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
var fullDomainName = ipProperties.HostName.ToLowerInvariant();
if ( !String.IsNullOrWhiteSpace( ipProperties.DomainName ) )
    fullDomainName = $"{ipProperties.HostName}.{ipProperties.DomainName}".ToLowerInvariant();

var builder = WebApplication.CreateBuilder( args );
builder
    .AddConfigurationPlaceholders( new InMemoryPlaceholderResolver( new Dictionary<String, String?>
    {
        { "FQDN", fullDomainName },
        { "Port", 5003.ToString( CultureInfo.InvariantCulture ) },
        { "FileExtension", ".txt" }
    } ),
    MissingPlaceholderValueStrategy.UseEmptyValue );

builder
    .Services
    .AddOptions<AppOptions>()
    .BindConfiguration( AppOptions.SectionName )
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services
    .AddHostedService<ConfigReloadWorker>();

var app = builder.Build();
app.MapGet( "/GetCertInfo",
( IConfiguration configuration ) => $"Use certificate with subject {configuration[ "CertificateSubject" ]} for HTTPS connection." );

app.Run();