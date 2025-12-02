using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace ModernWebApi;

/// <summary>
///     Demonstrates how to use placeholders with options and reloading.
/// </summary>
/// <remarks>
///     Change AppOptions:InputFile to see how reloading works.
/// </remarks>
public sealed class ConfigReloadWorker(
    ILogger<ConfigReloadWorker> logger,
    IOptions<AppOptions> options,
    IOptionsMonitor<AppOptions> optionsMonitor,
    IServiceProvider serviceProvider ) : BackgroundService
{
    #region Overrides of BackgroundService

    /// <summary>
    ///     This method is called when the <see cref="IHostedService" /> starts. The
    ///     implementation should return a task that represents
    ///     the lifetime of the long-running operation(s) being performed.
    /// </summary>
    /// <param name="stoppingToken">
    ///     Triggered when StopAsync is called.
    /// </param>
    /// <returns>A <see cref="Task" /> that represents the long-running operations.</returns>
    /// <remarks>
    ///     See <see href="https://learn.microsoft.com/dotnet/core/extensions/workers">Worker Services in .NET</see> for
    ///     implementation guidelines.
    /// </remarks>
    protected override async Task ExecuteAsync( CancellationToken stoppingToken )
    {
        await Task.Yield();
        logger.LogInformation( "Worker started..." );

        await using var scope = serviceProvider.CreateAsyncScope();
        var optionsSnapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<AppOptions>>();
        LogOptions( optionsSnapshot );

        var changed = optionsMonitor.OnChange( _ => { LogOptions( optionsSnapshot ); } );
        stoppingToken.Register( () => changed?.Dispose() );
    }

    private void LogOptions( IOptionsSnapshot<AppOptions> optionsSnapshot )
    {
        // ReSharper disable once InvertIf
        if ( logger.IsEnabled( LogLevel.Information ) )
        {
            var plainOptions = options.Value;
            var optionsFromMonitor = optionsMonitor.CurrentValue;
            var optionsFromSnapshot = optionsSnapshot.Value;

            var sb = new StringBuilder();
            sb.AppendLine( CultureInfo.InvariantCulture, $"Current InputFile plain   : {plainOptions.InputFile}" );
            sb.AppendLine( CultureInfo.InvariantCulture, $"Current InputFile Monitor : {optionsFromMonitor.InputFile}" );
            sb.AppendLine( CultureInfo.InvariantCulture, $"Current InputFile Snapshot: {optionsFromSnapshot.InputFile}" );
            logger.LogInformation( "{Values}", sb );
        }
    }

    #endregion
}