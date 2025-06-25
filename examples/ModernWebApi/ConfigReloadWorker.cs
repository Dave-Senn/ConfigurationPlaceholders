using System.Text;
using Microsoft.Extensions.Options;

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
    ///     This method is called when the <see cref="T:Microsoft.Extensions.Hosting.IHostedService" /> starts. The
    ///     implementation should return a task that represents
    ///     the lifetime of the long-running operation(s) being performed.
    /// </summary>
    /// <param name="stoppingToken">
    ///     Triggered when
    ///     <see cref="M:Microsoft.Extensions.Hosting.IHostedService.StopAsync(System.Threading.CancellationToken)" /> is
    ///     called.
    /// </param>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task" /> that represents the long-running operations.</returns>
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
        var plainOptions = options.Value;
        var optionsFromMonitor = optionsMonitor.CurrentValue;
        var optionsFromSnapshot = optionsSnapshot.Value;

        var sb = new StringBuilder();
        sb.AppendLine( $"Current InputFile plain   : {plainOptions.InputFile}" );
        sb.AppendLine( $"Current InputFile Monitor : {optionsFromMonitor.InputFile}" );
        sb.AppendLine( $"Current InputFile Snapshot: {optionsFromSnapshot.InputFile}" );
        logger.LogInformation( "{Values}", sb );
    }

    #endregion
}