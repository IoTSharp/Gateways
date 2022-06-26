using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IoTSharp.Gateway.Modbus.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ModbusSlave> ModbusSlaves { get; set; }

        public DbSet<PointMapping> PointMappings { get; set; }
    }
}