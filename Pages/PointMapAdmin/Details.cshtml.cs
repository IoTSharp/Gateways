using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using IoTSharp.Gateways.Data;

namespace IoTSharp.Gateways.Pages.PointMapAdmin
{
    public class DetailsModel : PageModel
    {
        private readonly IoTSharp.Gateways.Data.ApplicationDbContext _context;

        public DetailsModel(IoTSharp.Gateways.Data.ApplicationDbContext context)
        {
            _context = context;
        }

      public PointMapping PointMapping { get; set; } = default!; 

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null || _context.PointMappings == null)
            {
                return NotFound();
            }

            var pointmapping = await _context.PointMappings.FirstOrDefaultAsync(m => m.Id == id);
            if (pointmapping == null)
            {
                return NotFound();
            }
            else 
            {
                PointMapping = pointmapping;
            }
            return Page();
        }
    }
}
