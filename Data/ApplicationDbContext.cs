using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IoTSharp.Gateways.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
            if (Database.GetPendingMigrations().Count() > 0)
            {
                Database.Migrate();
            }
        }

        public DbSet<Client> Clients { get; set; }

        public DbSet<ModbusMapping>  ModbusMappings { get; set; }


        public DbSet<OPCUAMapping>   OPCUAMappings { get; set; }
    }
}