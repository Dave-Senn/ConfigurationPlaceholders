#pragma warning disable CA1050 // Declare types in namespaces
/// <summary>
/// </summary>
public sealed class Startup
#pragma warning restore CA1050 // Declare types in namespaces
{
    /// <summary>
    /// </summary>
    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure( IApplicationBuilder app )
    {
        app.UseStaticFiles();
        app.UseRouting();
        app.UseCors();

        app.UseEndpoints( endpoints => { endpoints.MapControllerRoute( "default", "{controller=Home}/{action=Index}" ); } );
    }

    /// <summary>
    /// </summary>
    public void ConfigureServices( IServiceCollection services ) =>
        services.AddMvc();
}