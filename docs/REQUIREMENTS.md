# Gereksinim izlenebilirliği

Kaynak: kullanıcının sağladığı Junior .NET Developer case dokümanı ve güncel e-posta görseli. Şirket dokümanları kaynak koda eklenmemiştir.

| Gereksinim                                           | Uygulama                                                     | Kontrol                                                          |
| ---------------------------------------------------- | ------------------------------------------------------------ | ---------------------------------------------------------------- |
| Plaka, tip, marka/model                              | Vehicle / VehicleForm / Vehicles ekranları                   | MVC ekran testi, yerel demo kontrolü                             |
| Dört araç tipi                                       | VehicleType enum                                             | Razor select, seed dört tipi içerir                              |
| Araç tanımlama/listeleme                             | VehiclesController Create/Index                              | Role bağlı POST, unique index                                    |
| Yeni → yönetici → finans → tamamlandı                | Workflow.Next / FineService.DecideAsync                      | Geçiş ve servis testleri                                         |
| Yalnız kendi aşamasında karar                        | Role ve status birlikte sunucuda kontrol edilir              | Yetkisiz geçiş ve forged POST testleri                           |
| Ret ve zorunlu neden                                 | Workflow.Reject                                              | Null/boş/boşluk testleri                                         |
| Kullanıcı, tarih, işlem, açıklama, önceki/yeni durum | ApprovalHistory                                              | Tam akış persistence testi, Details geçmiş tablosu               |
| EF Core / SQL Server                                 | AppDbContext / UseSqlServer / migration                      | Derleme, migration generation; gerçek DB yerel kontrolü bekliyor |
| .NET 9 MVC                                           | net9.0 / controller / Razor                                  | Derleme ve web host testleri                                     |
| Validation ve hata yönetimi                          | DataAnnotations, FineService, try/catch, üretim hata sayfası | Servis ve web testleri                                           |
