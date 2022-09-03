using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using IoTSharp.Gateways.Data;

namespace IoTSharp.Gateways.Pages.SlaveAdmin
{
    public class EditModel : PageModel
    {
        private readonly IoTSharp.Gateways.Data.ApplicationDbContext _context;

        public EditModel(IoTSharp.Gateways.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ModbusSlave ModbusSlave { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null || _context.ModbusSlaves == null)
            {
                return NotFound();
            }

            var modbusslave =  await _context.ModbusSlaves.FirstOrDefaultAsync(m => m.Id == id);
            if (modbusslave == null)
            {
                return NotFound();
            }
            ModbusSlave = modbusslave;
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(ModbusSlave).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ModbusSlaveExists(ModbusSlave.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool ModbusSlaveExists(Guid id)
        {
          return (_context.ModbusSlaves?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
