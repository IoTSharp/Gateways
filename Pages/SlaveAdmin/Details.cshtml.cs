using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using IoTSharp.Gateways.Data;

namespace IoTSharp.Gateways.Pages.SlaveAdmin
{
    public class DetailsModel : PageModel
    {
        private readonly IoTSharp.Gateways.Data.ApplicationDbContext _context;

        public DetailsModel(IoTSharp.Gateways.Data.ApplicationDbContext context)
        {
            _context = context;
        }

      public ModbusSlave ModbusSlave { get; set; } = default!; 

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null || _context.ModbusSlaves == null)
            {
                return NotFound();
            }

            var modbusslave = await _context.ModbusSlaves.FirstOrDefaultAsync(m => m.Id == id);
            if (modbusslave == null)
            {
                return NotFound();
            }
            else 
            {
                ModbusSlave = modbusslave;
            }
            return Page();
        }
    }
}
