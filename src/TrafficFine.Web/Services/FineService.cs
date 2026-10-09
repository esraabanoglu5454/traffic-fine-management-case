using Microsoft.EntityFrameworkCore;
using TrafficFine.Web.Data;
using TrafficFine.Web.Domain;
using TrafficFine.Web.ViewModels;

namespace TrafficFine.Web.Services;

public sealed class FineService(AppDbContext db, TimeProvider clock)
{
    public async Task<int> CreateAsync(FineForm form, Actor actor, CancellationToken ct = default)
    {
        if (!actor.HasRole(Roles.Operations)) throw new BusinessRuleException("Ceza oluşturma yetkiniz yok.");
        await ValidateAsync(form, null, ct);
        var fine = new FineRecord { CreatedById = actor.Id, CreatedAt = clock.GetUtcNow() };
        ApplyFields(fine, form);
        AddHistory(fine, actor, FineAction.Created, null, "Ceza kaydı oluşturuldu.");
        db.Fines.Add(fine);
        await db.SaveChangesAsync(ct);
        return fine.Id;
    }
    public async Task UpdateAsync(int id, FineForm form, Actor actor, CancellationToken ct = default)
    {
        var fine = await FindAsync(id, ct);
        CheckVersion(fine, form.Version);
        var action = fine.Status == FineStatus.Rejected ? FineAction.Revised : FineAction.Edited;
        var next = Workflow.Next(fine, action, actor, null);
        await ValidateAsync(form, id, ct);
        var previous = fine.Status;
        ApplyFields(fine, form);
        fine.Status = next;
        fine.Version = Guid.NewGuid();
        AddHistory(fine, actor, action, previous, "Ceza bilgileri güncellendi." +
            (action == FineAction.Revised ? " Yeniden yönetici ve finans onayı gerekir." : ""));
        await db.SaveChangesAsync(ct);
    }
    public async Task DecideAsync(DecisionForm form, Actor actor, CancellationToken ct = default)
    {
        if (form.Action is not (FineAction.Submitted or FineAction.ManagerApproved or FineAction.FinanceApproved or FineAction.Rejected))
            throw new BusinessRuleException("Geçersiz onay işlemi.");
        if (form.Comment?.Length > 1000) throw new BusinessRuleException("Açıklama en fazla 1000 karakter olabilir.");
        var fine = await FindAsync(form.Id, ct);
        CheckVersion(fine, form.Version);
        var next = Workflow.Next(fine, form.Action, actor, form.Comment);
        var previous = fine.Status;
        fine.Status = next;
        fine.Version = Guid.NewGuid();
        AddHistory(fine, actor, form.Action, previous, form.Comment?.Trim() ?? "");
        // EF Core wraps this UPDATE and the audit INSERT in a single transaction.
        // The Version predicate makes a racing/stale approval fail atomically.
        await db.SaveChangesAsync(ct);
    }
    private async Task<FineRecord> FindAsync(int id, CancellationToken ct) =>
        await db.Fines.SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new BusinessRuleException("Ceza kaydı bulunamadı.");
    private static void CheckVersion(FineRecord fine, Guid submitted)
    {
        if (fine.Version != submitted) throw new BusinessRuleException("Kayıt başka bir işlemle değişti. Sayfayı yenileyip tekrar deneyin.");
    }
    private async Task ValidateAsync(FineForm form, int? id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(form.Reference) || form.Reference.Trim().Length > 50)
            throw new BusinessRuleException("Geçerli bir tutanak numarası girin.");
        if (string.IsNullOrWhiteSpace(form.Description) || form.Description.Trim().Length > 1000)
            throw new BusinessRuleException("İhlal açıklaması 1–1000 karakter olmalı.");
        if (form.Amount <= 0 || form.Amount > 9999999999999999.99m || decimal.Round(form.Amount, 2) != form.Amount)
            throw new BusinessRuleException("Tutar pozitif olmalı ve en fazla iki ondalık hane içermeli.");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime);
        if (form.ViolationDate == default || form.ViolationDate > today)
            throw new BusinessRuleException("İhlal tarihi geçerli olmalı ve gelecekte olamaz.");
        if (!await db.Vehicles.AnyAsync(x => x.Id == form.VehicleId, ct))
            throw new BusinessRuleException("Seçilen araç bulunamadı.");
        var reference = form.Reference.Trim().ToUpperInvariant();
        if (await db.Fines.AnyAsync(x => x.Reference == reference && x.Id != id, ct))
            throw new BusinessRuleException("Bu tutanak numarası zaten kayıtlı.");
    }
    private static void ApplyFields(FineRecord fine, FineForm form)
    {
        fine.Reference = form.Reference.Trim().ToUpperInvariant();
        fine.VehicleId = form.VehicleId;
        fine.ViolationDate = form.ViolationDate;
        fine.Amount = form.Amount;
        fine.Description = form.Description.Trim();
    }
    private void AddHistory(FineRecord fine, Actor actor, FineAction action, FineStatus? previous, string comment) =>
        fine.History.Add(new ApprovalHistory
        {
            ActorId = actor.Id, ActorName = actor.Name, OccurredAt = clock.GetUtcNow(),
            Action = action, PreviousStatus = previous, NewStatus = fine.Status, Comment = comment
        });
}
