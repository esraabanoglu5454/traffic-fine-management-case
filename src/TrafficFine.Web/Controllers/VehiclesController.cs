using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrafficFine.Web.Data;
using TrafficFine.Web.Domain;
using TrafficFine.Web.ViewModels;
namespace TrafficFine.Web.Controllers;
[Authorize]
public sealed class VehiclesController(AppDbContext db, ILogger<VehiclesController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken ct)
    {
        ViewBag.Search = search;
        var query = db.Vehicles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant().Replace(" ", "");
            query = query.Where(x => x.Plate.Contains(term) || x.Brand.Contains(search.Trim()) || x.Model.Contains(search.Trim()));
        }
        return View(await query.OrderBy(x => x.Plate).ToListAsync(ct));
    }
    [Authorize(Roles = Roles.Operations), HttpGet]
    public IActionResult Create() => View(new VehicleForm());
    [Authorize(Roles = Roles.Operations), HttpPost]
    public async Task<IActionResult> Create(VehicleForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);
        var plate = form.Plate.Trim().ToUpperInvariant().Replace(" ", "");
        if (await db.Vehicles.AnyAsync(x => x.Plate == plate, ct))
        { ModelState.AddModelError(nameof(form.Plate), "Bu plaka zaten kayıtlı."); return View(form); }
        db.Vehicles.Add(new Vehicle { Plate = plate, Type = form.Type, Brand = form.Brand.Trim(), Model = form.Model.Trim() });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Araç kaydedilemedi.");
            ModelState.AddModelError("", "Araç kaydedilemedi. Plaka başka bir işlemle eklenmiş olabilir.");
            return View(form);
        }
        TempData["Success"] = "Araç kaydedildi.";
        return RedirectToAction(nameof(Index));
    }
}
