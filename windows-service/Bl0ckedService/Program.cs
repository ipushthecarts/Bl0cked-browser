using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bl0ckedService
{
    /// <summary>
    /// Entry point for the Bl0cked Lockdown Service.
    /// Configures and runs the service as a Windows Service.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Bl0cked Lockdown Service - Starting...");

                await CreateHostBuilder(args).Build().RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Creates the host builder for the Windows Service
        /// </summary>
        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "Bl0ckedService";
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Register the lockdown service
                    services.AddHostedService<LockdownService>();
                });
    }
}
