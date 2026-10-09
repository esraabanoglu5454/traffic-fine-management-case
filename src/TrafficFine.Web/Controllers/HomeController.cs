using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrafficFine.Web.Data;
using TrafficFine.Web.Domain;
using TrafficFine.Web.ViewModels;
namespace TrafficFine.Web.Controllers;
[Authorize]
public sealed class HomeController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var counts = await db.Fines.AsNoTracking().GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Status, x => x.Count, ct);
        int Count(FineStatus status) => counts.GetValueOrDefault(status);
        return View(new DashboardView(await db.Vehicles.CountAsync(ct), Count(FineStatus.New),
            Count(FineStatus.ManagerApproval), Count(FineStatus.FinanceApproval), Count(FineStatus.Completed),
            Count(FineStatus.Rejected), await db.Fines.SumAsync(x => (decimal?)x.Amount, ct) ?? 0));
    }
    [AllowAnonymous]
    public IActionResult Error() { Response.StatusCode = 500; return View(); }
}
