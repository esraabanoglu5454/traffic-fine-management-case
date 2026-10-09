using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using TrafficFine.Web.Domain;

namespace TrafficFine.Web.ViewModels;

public sealed class LoginForm
{
    [Required(ErrorMessage = "E-posta gerekli."), EmailAddress, Display(Name = "E-posta")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Şifre gerekli."), DataType(DataType.Password), Display(Name = "Şifre")]
    public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
public sealed class VehicleForm
{
    [Required, StringLength(20), RegularExpression(@"^[A-Za-z0-9 ]{3,20}$", ErrorMessage = "Plaka 3–20 harf, rakam veya boşluk içermeli."), Display(Name = "Plaka")]
    public string Plate { get; set; } = "";
    [EnumDataType(typeof(VehicleType)), Display(Name = "Araç tipi")]
    public VehicleType Type { get; set; }
    [Required, StringLength(60), Display(Name = "Marka")] public string Brand { get; set; } = "";
    [Required, StringLength(60), Display(Name = "Model")] public string Model { get; set; } = "";
}
public sealed class FineForm
{
    public int Id { get; set; }
    public Guid Version { get; set; }
    [Required, StringLength(50), Display(Name = "Ceza / tutanak numarası")]
    public string Reference { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "Bir araç seçin."), Display(Name = "Araç")]
    public int VehicleId { get; set; }
    [Required, DataType(DataType.Date), Display(Name = "İhlal tarihi")]
    public DateOnly ViolationDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [Range(typeof(decimal), "0.01", "9999999999999999.99", ErrorMessage = "Tutar sıfırdan büyük olmalı."), Display(Name = "Tutar (TL)")]
    public decimal Amount { get; set; }
    [Required, StringLength(1000), Display(Name = "İhlal açıklaması")]
    public string Description { get; set; } = "";
    public List<SelectListItem> Vehicles { get; set; } = [];
    public bool IsRevision { get; set; }
}
public sealed class DecisionForm
{
    public int Id { get; set; }
    public Guid Version { get; set; }
    [EnumDataType(typeof(FineAction))] public FineAction Action { get; set; }
    [StringLength(1000)] public string? Comment { get; set; }
}
public sealed record DashboardView(int Vehicles, int New, int Manager, int Finance, int Completed, int Rejected, decimal TotalAmount);
public sealed record FineListView(IReadOnlyList<FineRecord> Items, string? Search, FineStatus? Status, int Page, int TotalPages, int TotalCount);
public static class Labels
{
    public static string Status(FineStatus s) => s switch
    { FineStatus.New => "Yeni", FineStatus.ManagerApproval => "Yönetici Onayı", FineStatus.FinanceApproval => "Finans Onayı", FineStatus.Completed => "Tamamlandı", _ => "Reddedildi" };
    public static string Vehicle(VehicleType t) => t switch
    { VehicleType.Passenger => "Binek", VehicleType.Tractor => "Çekici", VehicleType.Trailer => "Dorse", _ => "Kiralık Araç" };
    public static string Action(FineAction a) => a switch
    { FineAction.Created => "Oluşturuldu", FineAction.Edited => "Düzenlendi", FineAction.Submitted => "Onaya gönderildi", FineAction.ManagerApproved => "Yönetici onayladı", FineAction.FinanceApproved => "Finans onayladı", FineAction.Rejected => "Reddedildi", _ => "Düzeltilerek yeniden açıldı" };
}
