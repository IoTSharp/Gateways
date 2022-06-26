using IoTSharp.Gateway.Modbus.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SilkierQuartz;

namespace IoTSharp.Gateway.Modbus
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(connectionString));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount=false)
                .AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.AddRazorPages();
            builder.Services.AddSilkierQuartz(option =>
            {
                option.VirtualPathRoot = "/SilkierQuartz";
                option.UseLocalTime = true;
                option.DefaultDateFormat = "yyyy-MM-dd";
                option.DefaultTimeFormat = "HH:mm:ss";
            }, auth=>
            {
                auth.AccessRequirement = SilkierQuartzAuthenticationOptions.SimpleAccessRequirement.AllowAnonymous;
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
            app.UseSilkierQuartz();
           
            app.MapRazorPages();

            app.Run();
        }
    }
}