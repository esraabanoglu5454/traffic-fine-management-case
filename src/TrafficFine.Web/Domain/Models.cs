using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace TrafficFine.Web.Domain;

public static class Roles
{
    public const string Operations = "Operations";
    public const string Manager = "Manager";
    public const string Finance = "Finance";
}
public enum VehicleType
{
    [Display(Name = "Binek")] Passenger = 0,
    [Display(Name = "Çekici")] Tractor = 1,
    [Display(Name = "Dorse")] Trailer = 2,
    [Display(Name = "Kiralık Araç")] Rental = 3
}
public enum FineStatus
{
    [Display(Name = "Yeni")] New = 0,
    [Display(Name = "Yönetici Onayı")] ManagerApproval = 1,
    [Display(Name = "Finans Onayı")] FinanceApproval = 2,
    [Display(Name = "Tamamlandı")] Completed = 3,
    [Display(Name = "Reddedildi")] Rejected = 4
}
public enum FineAction
{
    [Display(Name = "Oluşturuldu")] Created,
    [Display(Name = "Düzenlendi")] Edited,
    [Display(Name = "Onaya gönderildi")] Submitted,
    [Display(Name = "Yönetici onayladı")] ManagerApproved,
    [Display(Name = "Finans onayladı")] FinanceApproved,
    [Display(Name = "Reddedildi")] Rejected,
    [Display(Name = "Düzeltilerek yeniden açıldı")] Revised
}
public sealed class AppUser : IdentityUser
{
    [MaxLength(100)] public string DisplayName { get; set; } = "";
}
public sealed class Vehicle
{
    public int Id { get; set; }
    [MaxLength(20)] public string Plate { get; set; } = "";
    public VehicleType Type { get; set; }
    [MaxLength(60)] public string Brand { get; set; } = "";
    [MaxLength(60)] public string Model { get; set; } = "";
    public ICollection<FineRecord> Fines { get; set; } = new List<FineRecord>();
}
public sealed class FineRecord
{
    public int Id { get; set; }
    [MaxLength(50)] public string Reference { get; set; } = "";
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public DateOnly ViolationDate { get; set; }
    public decimal Amount { get; set; }
    [MaxLength(1000)] public string Description { get; set; } = "";
    public FineStatus Status { get; set; } = FineStatus.New;
    public string CreatedById { get; set; } = "";
    public AppUser CreatedBy { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    // An application-managed token also allows relational SQLite tests.
    public Guid Version { get; set; } = Guid.NewGuid();
    public ICollection<ApprovalHistory> History { get; set; } = new List<ApprovalHistory>();
}
public sealed class ApprovalHistory
{
    public long Id { get; set; }
    public int TrafficFineId { get; set; }
    public FineRecord FineRecord { get; set; } = null!;
    public string ActorId { get; set; } = "";
    public AppUser Actor { get; set; } = null!;
    [MaxLength(100)] public string ActorName { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public FineAction Action { get; set; }
    [MaxLength(1000)] public string Comment { get; set; } = "";
    public FineStatus? PreviousStatus { get; set; }
    public FineStatus NewStatus { get; set; }
}
