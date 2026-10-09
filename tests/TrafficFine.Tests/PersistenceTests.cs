using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TrafficFine.Web.Data;
using TrafficFine.Web.Domain;
using TrafficFine.Web.Services;
using TrafficFine.Web.ViewModels;
using Xunit;
namespace TrafficFine.Tests;

// SQLite is used only as a relational test double. The MVC app always uses SQL Server.
public sealed class PersistenceTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private DbContextOptions<AppDbContext> options = null!;
    private readonly Actor operations = new("ops", "Operations", new HashSet<string> { Roles.Operations });
    private readonly Actor manager = new("mgr", "Manager", new HashSet<string> { Roles.Manager });
    private readonly Actor finance = new("fin", "Finance", new HashSet<string> { Roles.Finance });
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = NewContext();
        await db.Database.EnsureCreatedAsync();
        db.Users.AddRange(new AppUser { Id = "ops", UserName = "ops" }, new AppUser { Id = "mgr", UserName = "mgr" }, new AppUser { Id = "fin", UserName = "fin" });
        db.Vehicles.Add(new Vehicle { Id = 1, Plate = "34ABC123", Brand = "Ford", Model = "Focus" });
        await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => connection.DisposeAsync().AsTask();
    private AppDbContext NewContext() => new(options);
    private static FineService Service(AppDbContext db) => new(db, TimeProvider.System);
    private static FineForm Form(string reference = "TEST-001") => new()
    { Reference = reference, VehicleId = 1, ViolationDate = new DateOnly(2026, 1, 1), Amount = 1250.50m, Description = "Speeding" };
    private async Task<int> Create()
    {
        await using var db = NewContext();
        return await Service(db).CreateAsync(Form(), operations);
    }
    private async Task Decide(int id, FineAction action, Actor actor, string? reason = null)
    {
        await using var db = NewContext();
        var fine = await db.Fines.SingleAsync(x => x.Id == id);
        await Service(db).DecideAsync(new DecisionForm { Id = id, Version = fine.Version, Action = action, Comment = reason }, actor);
    }
    [Fact]
    public async Task Full_workflow_persists_actor_time_state_and_audit()
    {
        var id = await Create();
        await Decide(id, FineAction.Submitted, operations);
        await Decide(id, FineAction.ManagerApproved, manager, "Checked");
        await Decide(id, FineAction.FinanceApproved, finance);
        await using var db = NewContext();
        Assert.Equal(FineStatus.Completed, (await db.Fines.SingleAsync()).Status);
        var history = await db.ApprovalHistory.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(4, history.Count);
        Assert.Null(history[0].PreviousStatus);
        Assert.Equal(FineStatus.ManagerApproval, history[2].PreviousStatus);
        Assert.Equal(FineStatus.FinanceApproval, history[2].NewStatus);
        Assert.Equal("mgr", history[2].ActorId);
        Assert.Equal("Checked", history[2].Comment);
        Assert.All(history, item => Assert.NotEqual(default, item.OccurredAt));
    }
    [Fact]
    public async Task Rejected_record_is_revised_then_requires_all_approvals_again()
    {
        var id = await Create();
        await Decide(id, FineAction.Submitted, operations);
        await Decide(id, FineAction.Rejected, manager, "Incomplete description");
        await using (var db = NewContext())
        {
            var fine = await db.Fines.SingleAsync();
            var form = Form(); form.Id = id; form.Version = fine.Version; form.Description = "Corrected description";
            await Service(db).UpdateAsync(id, form, operations);
        }
        await using (var db = NewContext())
        {
            Assert.Equal(FineStatus.New, (await db.Fines.SingleAsync()).Status);
            Assert.Contains(await db.ApprovalHistory.ToListAsync(), x => x.Action == FineAction.Rejected && x.Comment == "Incomplete description");
        }
        await Decide(id, FineAction.Submitted, operations);
        await Decide(id, FineAction.ManagerApproved, manager);
        await Decide(id, FineAction.FinanceApproved, finance);
        await using var check = NewContext();
        Assert.Equal(7, await check.ApprovalHistory.CountAsync());
    }
    [Fact]
    public async Task Invalid_decision_creates_no_history_and_changes_no_status()
    {
        var id = await Create();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Decide(id, FineAction.FinanceApproved, finance));
        await using var db = NewContext();
        Assert.Equal(FineStatus.New, (await db.Fines.SingleAsync()).Status);
        Assert.Equal(1, await db.ApprovalHistory.CountAsync());
    }
    [Fact]
    public async Task Stale_form_is_rejected_without_extra_audit()
    {
        var id = await Create();
        Guid token;
        await using (var db = NewContext()) token = (await db.Fines.SingleAsync()).Version;
        await Decide(id, FineAction.Submitted, operations);
        await using (var db = NewContext())
            await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).DecideAsync(
                new DecisionForm { Id = id, Version = token, Action = FineAction.Submitted }, operations));
        await using var check = NewContext();
        Assert.Equal(2, await check.ApprovalHistory.CountAsync());
    }
    [Fact]
    public async Task Concurrent_decisions_roll_back_losing_audit_insert()
    {
        var id = await Create();
        await Decide(id, FineAction.Submitted, operations);
        await using var first = NewContext();
        await using var second = NewContext();
        var a = await first.Fines.SingleAsync();
        var b = await second.Fines.SingleAsync();
        var form = new DecisionForm { Id = id, Version = a.Version, Action = FineAction.ManagerApproved };
        await Service(first).DecideAsync(form, manager);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Service(second).DecideAsync(
            new DecisionForm { Id = id, Version = b.Version, Action = FineAction.ManagerApproved }, manager));
        await using var check = NewContext();
        Assert.Equal(FineStatus.FinanceApproval, (await check.Fines.SingleAsync()).Status);
        Assert.Equal(3, await check.ApprovalHistory.CountAsync());
    }
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(12.345)]
    public async Task Invalid_amount_is_rejected(decimal amount)
    {
        var form = Form(); form.Amount = amount;
        await using var db = NewContext();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).CreateAsync(form, operations));
        Assert.Equal(0, await db.Fines.CountAsync());
    }
    [Fact]
    public async Task Duplicate_reference_is_rejected_after_normalization()
    {
        await Create();
        await using var db = NewContext();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).CreateAsync(Form(" test-001 "), operations));
        Assert.Equal(1, await db.Fines.CountAsync());
    }
    [Fact]
    public async Task Future_violation_date_is_rejected()
    {
        var form = Form(); form.ViolationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        await using var db = NewContext();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).CreateAsync(form, operations));
    }
    [Fact]
    public async Task Nonexistent_vehicle_is_rejected()
    {
        var form = Form(); form.VehicleId = 999;
        await using var db = NewContext();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).CreateAsync(form, operations));
    }
}
