namespace Uptime.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ServiceTracker.Start();
            // Host.CreateDefaultBuilder(args)
            //     .ConfigureWebHostDefaults(webBuilder =>
            //     {
            //         webBuilder.UseStartup<Startup>();
            //     });
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllersWithViews();
            builder.Services.AddRazorPages();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseBlazorFrameworkFiles();
            app.UseStaticFiles();

            app.UseRouting();


            app.MapRazorPages();
            app.MapControllers();
            app.MapFallbackToFile("index.html");

            app.Run();
        }
    }
}