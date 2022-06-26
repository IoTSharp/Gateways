using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using IoTSharp.Gateway.Modbus.Data;

namespace IoTSharp.Gateway.Modbus.Pages.PointMapAdmin
{
    public class CreateModel : PageModel
    {
        private readonly IoTSharp.Gateway.Modbus.Data.ApplicationDbContext _context;

        public CreateModel(IoTSharp.Gateway.Modbus.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        [BindProperty]
        public PointMapping PointMapping { get; set; } = default!;
        

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
          if (!ModelState.IsValid || _context.PointMappings == null || PointMapping == null)
            {
                return Page();
            }

            _context.PointMappings.Add(PointMapping);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
