# Backend uçları

Senaryo sayısı: **37** · Öncelik: P0=4, P1=33

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## API-001 — GET /health

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. Kimliksiz GET /health gönder
2. Yanıtı oku

### Beklenen sonuç

200; status=ok, service=Harekat.Api ve UTC alanı bulunur; bu yalnız API canlılığını gösterir.

---

## API-002 — POST /auth/register

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. Benzersiz test kullanıcı/e-posta ve geçerli parola ile kayıt gönder

### Beklenen sonuç

Başarılı kayıt tek oyuncu oluşturur; dönen auth verisinde parola yer almaz.

---

## API-003 — POST /auth/login

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. Kayıtlı A hesabının doğru parolasını gönder
2. Yanlış parola ile tekrarla

### Beklenen sonuç

Doğru bilgiyle token; yanlış bilgiyle başarısız yanıt; stack trace/parola açığa çıkmaz.

---

## API-004 — POST /auth/refresh

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. Geçerli refresh token ile yenile
2. Geçersiz token ile tekrarla

### Beklenen sonuç

Geçerli akış yeni auth verisi üretir; geçersiz token kimlik doğrulamaz.

---

## API-005 — POST /auth/verify-email

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. A tokenı ve test doğrulama koduyla gönder
2. Yanlış kodla tekrarla

### Beklenen sonuç

Geçerli işlem verified=true; yanlış kod doğrulanmış hesap üretmez.

---

## API-006 — POST /auth/logout

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. A tokenıyla çıkış yap
2. Eski refresh ile yenilemeyi dene

### Beklenen sonuç

loggedOut=true; çıkış yapılan oturumun yenileme erişimi politikaya göre iptal edilir; sapma hata kaydıdır.

---

## API-007 — GET /players/me

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. A tokenıyla isteği gönder
2. Token olmadan tekrarla

### Beklenen sonuç

A'nın profili döner; kimliksiz istek 401 alır.

---

## API-008 — GET /players/{username}

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. A kullanıcı adıyla kimliksiz iste
2. Olmayan adı sorgula

### Beklenen sonuç

A'nın herkese açık profili döner; özel auth alanları yoktur; olmayan ad açık hata üretir.

---

## API-009 — POST /squads

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. A tokenıyla geçerli isimli tim kur

### Beklenen sonuç

A komutan/üye olarak tek timde görünür; davet kodu kullanılabilir.

---

## API-010 — POST /squads/join

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. B hesabıyla A'nın davet kodunu gönder

### Beklenen sonuç

B aynı timde görünür; mevcut üyeler kaybolmaz.

---

## API-011 — GET /squads/me

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. B tokenıyla sorgula
2. Timsiz hesapla sorgula

### Beklenen sonuç

B'nin timi döner; timsiz hesap 404 döner.

---

## API-012 — GET /squads/{id:guid}

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. Bilinen tim GUID ile yetkili sorgu yap
2. Geçersiz GUID gönder

### Beklenen sonuç

Geçerli timin üyeleri döner; geçersiz GUID rota eşleşmez ve 500 üretmez.

---

## API-013 — POST /squads/ready

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. A tokenıyla ready=true sorgu parametresi gönder
2. ready=false tekrarla

### Beklenen sonuç

Hazır durumu değişir ve lobi Ready olayı yeni durumla tutarlıdır.

---

## API-014 — POST /squads/leave

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. B ile ayrıl
2. Tim listesine tekrar bak

### Beklenen sonuç

left=true; B listeden çıkar; diğer üyeler korunur.

---

## API-015 — POST /matchmaking/queue

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. A tokenıyla geçerli QueueRequest gönder

### Beklenen sonuç

Bir bilet kimliği ve gerçek durum döner; UI olmayan tahmini beklemeyi gerçek veri diye sunmaz.

---

## API-016 — DELETE /matchmaking/queue

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. Kuyruktaki A ile iptal isteği gönder

### Beklenen sonuç

cancelled=true; A bekleyen eşleştirme girişinden çıkar.

---

## API-017 — GET /matchmaking/tickets/{id:guid}

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. A'nın biletini sorgula
2. Rastgele geçerli GUID sorgula

### Beklenen sonuç

Bilet durumu döner; bilinmeyen GUID için 404.

---

## API-018 — GET /matches/{id:guid}

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. Atanmış maç kimliğini sorgula
2. Bilinmeyen GUID gönder

### Beklenen sonuç

Maç kaydı döner; bilinmeyen GUID 404; token gereklidir.

---

## API-019 — POST /servers/register

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. RegisterServerRequest ile izole test sunucusunu kaydet

### Beklenen sonuç

Sunucu kaydı alınır; üretilen anahtar yalnız yetkili laboratuvar kaydında tutulur; rota bugün AllowAnonymous olduğundan güvenlik kapısı ayrı incelenir.

---

## API-020 — POST /servers/heartbeat

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. Kayıtlı test sunucusuyla heartbeat gönder

### Beklenen sonuç

Son canlılık ve sunucu durum alanları güncellenir; yanlış kimlik/anahtar uygulama servisi politikasına göre reddedilir.

---

## API-021 — GET /servers

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. Kimliksiz liste iste
2. Yanıt alanlarını kontrol et

### Beklenen sonuç

Sunucu listesi gelir; server key gibi sırlar yanıtta bulunmaz.

---

## API-022 — POST /matches/{id:guid}/result yetkisiz

- **Öncelik:** P0
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. X-Server-Id ve X-Server-Key olmadan sonuç gönder

### Beklenen sonuç

401; maç ve oyuncu XP kaydı değişmez.

---

## API-023 — POST /matches/{id:guid}/result geçerli

- **Öncelik:** P0
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. S kimlik/anahtarıyla kendisine atanmış maç sonucunu gönder
2. Aynı sonucu tekrar gönder

### Beklenen sonuç

Yetkili sonuç tek kez kaydedilir; tekrarda XP/kill çiftlenmesi olmaz; idempotency sapması hata olarak açılır.

---

## API-024 — GET /leaderboards

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. metric=experience ve take=10 ile sorgula

### Beklenen sonuç

En çok 10 kayıt ilgili metriğe göre sıralıdır; diğer gizli oyuncu alanları sızmaz.

---

## API-025 — GET /leaderboards/season

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. Bilinen season ve take=10 ile sorgula

### Beklenen sonuç

Sezon puanı doğru sezonla sınırlı; kariyer toplamıyla karışmaz.

---

## API-026 — POST /friends/request

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. A ile B oyuncu kimliğine istek gönder

### Beklenen sonuç

B için tek bekleyen arkadaşlık isteği oluşur.

---

## API-027 — POST /friends/{id:guid}/accept

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. B ile alınan istek kimliğini kabul et

### Beklenen sonuç

A/B arkadaşlığı tutarlı görünür; ilgisiz kullanıcı kabul edemez.

---

## API-028 — GET /friends

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. A tokenıyla arkadaş listesini sorgula

### Beklenen sonuç

Kabul edilen B görünür; başka hesabın özel istekleri görünmez.

---

## API-029 — GET /seasons/active

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. Aktif sezon fixture ile iste
2. Aktif sezonsuz fixture ile iste

### Beklenen sonuç

Aktif sezon döner; aktif sezon yoksa 404.

---

## API-030 — GET /seasons/{number:int}/archive

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. Bilinen sezon numarasıyla arşiv iste

### Beklenen sonuç

İstenen sezon kayıtları gelir; geçerli yeni sezon durumu arşivi değiştirmez.

---

## API-031 — GET /achievements/me

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. A tokenıyla başarımları sorgula

### Beklenen sonuç

A'nın koşullarına göre kilitli/açılmış liste; kimliksiz 401.

---

## API-032 — GET /cosmetics/me

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. A tokenıyla kozmetikleri sorgula

### Beklenen sonuç

A'nın sahiplik/kullanım bilgisi tutarlı; kimliksiz 401.

---

## API-033 — POST /cosmetics/equip

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. A'nın sahip olduğu kozmetiği kuşan
2. Sahip olmadığı kimliği dene

### Beklenen sonuç

Sahip olunan kozmetik kuşanılır; olmayan sahiplik güç/ödül kazandırmaz.

---

## API-034 — POST /moderation/report

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. A ile B hakkında test raporu gönder

### Beklenen sonuç

reported=true; moderasyon kuyruğunda bir kayıt; rapor tek başına otomatik suç hükmü değildir.

---

## API-035 — POST /moderation/ban

- **Öncelik:** P0
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. Normal A tokenıyla test B'yi banlamayı dene
2. M ile kontrollü tekrarla

### Beklenen sonuç

A için 403; M için işlem yetki politikasıyla uygulanır; B test hesabıdır.

---

## API-036 — POST /moderation/mute

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. Normal A tokenıyla dene
2. M ile test B üzerinde süreli mute uygula

### Beklenen sonuç

A için 403; M işlemi uygular; süre/ana gerekçe denetlenebilir.

---

## API-037 — GET /moderation/reports

- **Öncelik:** P0
- **Kaynak:** `Backend/Harekat.Api/Program.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.

### Adımlar

1. A ve M tokenlarıyla sırayla liste iste

### Beklenen sonuç

A için 403; yalnız M moderasyon kayıtlarını görür.

---
