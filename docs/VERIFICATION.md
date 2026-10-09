# Doğrulama durumu

Kontrol tarihi: 9 Ekim 2026.

| Kontrol | Sonuç |
| --- | --- |
| .NET 9 SDK | 9.0.318 ile derlendi |
| Debug web derlemesi | Başarılı; 0 uyarı, 0 hata |
| Release solution derlemesi | Başarılı; 0 uyarı, 0 hata |
| Otomatik testler | 37 geçti; 0 başarısız, 0 atlanan |
| SQL Server migration üretimi | Başarılı |
| Model ile migration tutarlılığı | Bekleyen model değişikliği yok |
| Idempotent SQL script üretimi | docs/schema.sql oluşturuldu |
| Identity + MVC endpoint doğrulaması | In-process host'ta başarıyla test edildi |
| Türkçe tutar girişi | 1500,50 girdisi 1500.50m olarak kaydedildi |
| Eşzamanlı onay ve audit rollback | SQLite ilişkisel testte başarılı |
| Ekran görünümü | MVC'nin ürettiği gerçek HTML ve uygulama CSS'iyle Chromium'da masaüstü/mobil render incelendi |
| Gerçek SQL Server migration / uçtan uca demo | **Kullanıcı tarafından 9 Ekim 2026 tarihinde Windows SQL Server LocalDB ile doğrulandı (ekran görüntüleri üzerinden gözlendi)** |
| Windows LocalDB | Kullanıcının bilgisayarında MSSQLLocalDB Running doğrulandı; kurulum komutları manuel çalıştırıldı, setup script ayrıca test edilmedi |
| GitHub erişimi / push | Uzak repository oluşturulmadı, kaynak yerel Git deposunda |

## Test sınırları

PersistenceTests SQLite kullanır; WebTests EF InMemory sağlayıcısı kullanır. Bu sağlayıcılar sadece test projesine eklenmiştir; uygulama SQL Server ile yapılandırılmıştır. SQLite testleri transaction ve concurrency davranışını ilişkisel olarak kontrol eder; SQL Server migration ve SQL Server'a özel sorgu/kolasyon davranışları için gerçek SQL Server gereklidir.

Ekran görüntüleri [screenshots](screenshots/) klasöründedir. Test verileriyle MVC'nin ürettiği HTML render edilmiştir; bunlar SQL Server üzerinden çalışan canlı bir tarayıcı oturumu kanıtı değildir. Görsel kontrolde masaüstü 1440px, mobil 390px kullanılmış, ana sayfa body yatay taşması bulunmamıştır.

## Teslimden önce yerelde tamamlanacak

1. README komutlarıyla SQL Server/LocalDB üzerinde migration uygulayın.
2. Development demo seed'ini başlatıp üç hesapla giriş yapın.
3. Araç ve yeni ceza ekleme → yönetici ret → düzeltme/yeniden gönderme → yönetici onay → finans onay akışını tamamlayın.
4. Tamamlanan kaydın kilidini ve işlem geçmişini doğrulayın.
5. Aynı kaydı iki sekmede açıp eski formdan ikinci kararın reddedildiğini kontrol edin.
6. dotnet test çalıştırın; GitHub'a yükleyip alıcının erişimini kontrol edin.

Bu kontrollerden sonra aşağıya gerçek tarih, kullanılan SQL Server sürümü ve sonucu ekleyin.

**Yerel SQL Server doğrulaması:** 09.10.2026 tarihinde MSSQLLocalDB 15.0.4382.1 üzerinde `dotnet ef database update` işlemi başarıyla tamamlandı. Üç demo kullanıcıyla giriş yapıldı. Araç ekleme ve mükerrer plaka engelleme; ceza oluşturma; yönetici ve finans onayı; tamamlanan kaydın UI üzerinden kilitlenmesi; gerekçeli ret, düzeltme ve yeniden onay; beş adımlı audit geçmişi tarayıcıdan kontrol edildi. `dotnet test`: 37 geçti, 0 başarısız, 0 atlanan. `dotnet build --no-restore`: başarılı. **Sınır:** Sunucu tarafında yetki bypass/yarış durumu ve güvenlik penetrasyon testleri kullanıcı ortamında ayrıca yürütülmedi; eski ekran görüntüleri teslimden önce güncellenmelidir.
