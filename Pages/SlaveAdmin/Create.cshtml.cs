using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using IoTSharp.Gateways.Data;

namespace IoTSharp.Gateways.Pages.SlaveAdmin
{
    public class CreateModel : PageModel
    {
        private readonly IoTSharp.Gateways.Data.ApplicationDbContext _context;

        public CreateModel(IoTSharp.Gateways.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        [BindProperty]
        public ModbusSlave ModbusSlave { get; set; } = default!;
        

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
          if (!ModelState.IsValid || _context.ModbusSlaves == null || ModbusSlave == null)
            {
                return Page();
            }

            _context.ModbusSlaves.Add(ModbusSlave);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
