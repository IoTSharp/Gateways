using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using IoTSharp.Gateways.Data;

namespace IoTSharp.Gateways.Pages.PointMapAdmin
{
    public class CreateModel : PageModel
    {
        private readonly IoTSharp.Gateways.Data.ApplicationDbContext _context;

        public CreateModel(IoTSharp.Gateways.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet(Guid? id)
        {
            return Page();
        }

        [BindProperty]
        public PointMapping PointMapping { get; set; } = default!;
        

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync(Guid  id)
        {
            var owner = _context.ModbusSlaves.Find(id);
          if (!ModelState.IsValid || _context.PointMappings == null || PointMapping == null || owner==null)
            {
                return Page();
            }
            PointMapping.Id = Guid.NewGuid();
            PointMapping.Owner = owner;
            _context.PointMappings.Add(PointMapping);
            await _context.SaveChangesAsync();
            return RedirectToPage($"./Index", id);
        }
    }
}
