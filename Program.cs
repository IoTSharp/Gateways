using IoTSharp.Gateways.Data;
using IoTSharp.Gateways.Jobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace IoTSharp.Gateways
{
    public class Program
    {
        public static void Main(string[] args)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var builder = WebApplication.CreateBuilder(args);
            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(connectionString));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount=false)
                .AddEntityFrameworkStores<ApplicationDbContext>();



            builder.Services.AddRazorPages();
            builder.Services.AddMemoryCache();

       builder.Services.AddIoTSharpMqttSdk(builder.Configuration);

            builder.Services.AddQuartz(q =>
            {
                q.UseMicrosoftDependencyInjectionJobFactory();

                var ModbusSchedulerJobKey = new JobKey("ModbusSchedulerJob");
                q.AddJob<ModbusSchedulerJob>(opts => opts.WithIdentity(ModbusSchedulerJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(ModbusSchedulerJobKey)
                    .WithIdentity("ModbusSchedulerJob-trigger")
                    .WithSimpleSchedule(x => x
                        .WithIntervalInMinutes(1)
                        .RepeatForever()).StartNow());
 
                // base quartz scheduler, job and trigger configuration
            });

            // ASP.NET Core hosting
            builder.Services.AddQuartzServer(options =>
            {
                options.StartDelay = TimeSpan.FromSeconds(10);
                // when shutting down we want jobs to complete gracefully
                options.WaitForJobsToComplete = true;
            });





            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();
       
           
            app.MapRazorPages();
           
 
            app.Run();
        }
    }
}