using UploadBox.Tests.UI.Components;
using Microsoft.AspNetCore.DataProtection;

namespace UploadBox.Tests.UI
{
    /// <summary>
    /// Configures and runs the local visual test web host.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Builds and starts the interactive server-rendered UI test host.
        /// </summary>
        /// <param name="args">Command-line arguments passed to the web host.</param>
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            // This host is a local visual test harness; its protection keys need not survive restarts.
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
