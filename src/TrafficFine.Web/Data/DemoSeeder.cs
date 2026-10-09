using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrafficFine.Web.Domain;
using TrafficFine.Web.Services;
using TrafficFine.Web.ViewModels;
namespace TrafficFine.Web.Data;
public static class DemoSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        var password = config["Demo:Password"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Demo:Seed etkin. Demo:Password değerini user-secrets ile belirtin.");
        var db = services.GetRequiredService<AppDbContext>();
        if ((await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Önce dotnet ef database update komutunu çalıştırın.");
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var users = services.GetRequiredService<UserManager<AppUser>>();
        var actors = new Dictionary<string, Actor>();
        foreach (var (role, email, name) in new[]
        {
            (Roles.Operations, "operasyon@case.local", "Operasyon Kullanıcısı"),
            (Roles.Manager, "yonetici@case.local", "Yönetici"),
            (Roles.Finance, "finans@case.local", "Finans Kullanıcısı")
        })
        {
            if (!await roles.RoleExistsAsync(role)) Ensure(await roles.CreateAsync(new IdentityRole(role)));
            var user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                user = new AppUser { UserName = email, Email = email, DisplayName = name, EmailConfirmed = true };
                Ensure(await users.CreateAsync(user, password));
            }
            if (!await users.IsInRoleAsync(user, role)) Ensure(await users.AddToRoleAsync(user, role));
            actors[role] = new Actor(user.Id, user.UserName!, new HashSet<string> { role });
        }
        if (!await db.Vehicles.AnyAsync())
        {
            db.Vehicles.AddRange(
                new Vehicle { Plate = "34ABC123", Type = VehicleType.Passenger, Brand = "Renault", Model = "Clio" },
                new Vehicle { Plate = "41SRL041", Type = VehicleType.Tractor, Brand = "Mercedes-Benz", Model = "Actros" },
                new Vehicle { Plate = "54DRS054", Type = VehicleType.Trailer, Brand = "Tırsan", Model = "Tenteli" },
                new Vehicle { Plate = "06KRL006", Type = VehicleType.Rental, Brand = "Fiat", Model = "Egea" });
            await db.SaveChangesAsync();
        }
        var vehicleId = await db.Vehicles.Select(x => x.Id).FirstAsync();
        var service = services.GetRequiredService<FineService>();
        for (var i = 0; i < 5; i++)
        {
            var reference = $"DEMO-2026-{i + 1:000}";
            if (await db.Fines.AnyAsync(x => x.Reference == reference)) continue;
            var id = await service.CreateAsync(new FineForm { Reference = reference, VehicleId = vehicleId,
                Amount = 1500 + i * 750, ViolationDate = new DateOnly(2026, 1, 10),
                Description = "Demo kayıt: hız sınırı ihlali." }, actors[Roles.Operations]);
            async Task Act(FineAction action, string role, string? comment = null)
            {
                var fine = await db.Fines.SingleAsync(x => x.Id == id);
                await service.DecideAsync(new DecisionForm { Id = id, Version = fine.Version, Action = action, Comment = comment }, actors[role]);
            }
            if (i > 0) await Act(FineAction.Submitted, Roles.Operations);
            if (i is 2 or 3) await Act(FineAction.ManagerApproved, Roles.Manager);
            if (i == 3) await Act(FineAction.FinanceApproved, Roles.Finance);
            if (i == 4) await Act(FineAction.Rejected, Roles.Manager, "Tutanak açıklaması eksik. Operasyon tarafından düzeltilmeli.");
        }
    }
    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
