using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrafficFine.Web.Data;
using TrafficFine.Web.Domain;
using TrafficFine.Web.Services;
using TrafficFine.Web.ViewModels;
namespace TrafficFine.Web.Controllers;
[Authorize]
public sealed class FinesController(AppDbContext db, FineService service, ILogger<FinesController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, FineStatus? status, int page = 1, CancellationToken ct = default)
    {
        if (status.HasValue && !Enum.IsDefined(status.Value)) return BadRequest();
        var query = db.Fines.AsNoTracking().Include(x => x.Vehicle).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant().Replace(" ", "");
            query = query.Where(x => x.Reference.Contains(term) || x.Vehicle.Plate.Contains(term));
        }
        if (status.HasValue) query = query.Where(x => x.Status == status);
        var count = await query.CountAsync(ct);
        var pages = Math.Max(1, (int)Math.Ceiling(count / 15d));
        page = Math.Clamp(page, 1, pages);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * 15).Take(15).ToListAsync(ct);
        return View(new FineListView(items, search, status, page, pages, count));
    }
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var fine = await db.Fines.AsNoTracking().Include(x => x.Vehicle).Include(x => x.CreatedBy)
            .Include(x => x.History).SingleOrDefaultAsync(x => x.Id == id, ct);
        return fine is null ? NotFound() : View(fine);
    }
    [Authorize(Roles = Roles.Operations), HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct) => View("Form", await Options(new FineForm(), ct));
    [Authorize(Roles = Roles.Operations), HttpPost]
    public async Task<IActionResult> Create(FineForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var id = await service.CreateAsync(form, User.ToActor(), ct);
                TempData["Success"] = "Ceza oluşturuldu. Hazır olduğunda onaya gönderin.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (BusinessRuleException ex) { ModelState.AddModelError("", ex.Message); }
            catch (DbUpdateException ex) { HandleDatabaseError(ex); }
        }
        form.Id = 0;
        return View("Form", await Options(form, ct));
    }
    [Authorize(Roles = Roles.Operations), HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var fine = await db.Fines.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (fine is null) return NotFound();
        if (fine.CreatedById != User.ToActor().Id || fine.Status is not (FineStatus.New or FineStatus.Rejected)) return Forbid();
        return View("Form", await Options(new FineForm
        {
            Id = fine.Id, Version = fine.Version, Reference = fine.Reference, VehicleId = fine.VehicleId,
            ViolationDate = fine.ViolationDate, Amount = fine.Amount, Description = fine.Description,
            IsRevision = fine.Status == FineStatus.Rejected
        }, ct));
    }
    [Authorize(Roles = Roles.Operations), HttpPost]
    public async Task<IActionResult> Edit(int id, FineForm form, CancellationToken ct)
    {
        if (id != form.Id) return BadRequest();
        if (ModelState.IsValid)
        {
            try
            {
                await service.UpdateAsync(id, form, User.ToActor(), ct);
                TempData["Success"] = "Ceza bilgileri güncellendi.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (BusinessRuleException ex) { ModelState.AddModelError("", ex.Message); }
            catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "Kayıt değişti. Sayfayı yenileyin."); }
            catch (DbUpdateException ex) { HandleDatabaseError(ex); }
        }
        return View("Form", await Options(form, ct));
    }
    [HttpPost]
    public async Task<IActionResult> Decide(DecisionForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "İşlem bilgileri geçersiz. Açıklama en fazla 1000 karakter olabilir.";
            return RedirectToAction(nameof(Details), new { id = form.Id });
        }
        try
        {
            await service.DecideAsync(form, User.ToActor(), ct);
            TempData["Success"] = "İşlem kaydedildi ve geçmişe eklendi.";
        }
        catch (BusinessRuleException ex) { TempData["Error"] = ex.Message; }
        catch (DbUpdateConcurrencyException) { TempData["Error"] = "Başka bir kullanıcı bu kaydı değiştirdi. Güncel durumu kontrol edin."; }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Ceza {FineId} üzerinde işlem kaydedilemedi", form.Id);
            TempData["Error"] = "İşlem kaydedilemedi. Lütfen yeniden deneyin.";
        }
        return RedirectToAction(nameof(Details), new { id = form.Id });
    }
    private async Task<FineForm> Options(FineForm form, CancellationToken ct)
    {
        form.Vehicles = await db.Vehicles.AsNoTracking().OrderBy(x => x.Plate)
            .Select(x => new SelectListItem(x.Plate + " · " + x.Brand + " " + x.Model, x.Id.ToString()))
            .ToListAsync(ct);
        return form;
    }
    private void HandleDatabaseError(DbUpdateException ex)
    {
        logger.LogWarning(ex, "Ceza kaydedilemedi.");
        ModelState.AddModelError("", "Kayıt kaydedilemedi. Tutanak numarası başka bir işlemle eklenmiş olabilir.");
    }
}
