using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using IoTSharp.Gateway.Modbus.Data;

namespace IoTSharp.Gateway.Modbus.Pages.SlaveAdmin
{
    public class IndexModel : PageModel
    {
        private readonly IoTSharp.Gateway.Modbus.Data.ApplicationDbContext _context;

        public IndexModel(IoTSharp.Gateway.Modbus.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<ModbusSlave> ModbusSlave { get;set; } = default!;

        public async Task OnGetAsync()
        {
            if (_context.ModbusSlaves != null)
            {
                ModbusSlave = await _context.ModbusSlaves.ToListAsync();
            }
        }
    }
}
