# Atış Poligonu — Görev Zinciri (18 adım)

Kaynak veri: [`tutorial_steps.json`](tutorial_steps.json). Kontrol şeması `PauseMenu` ile aynı.

**Toplam adım: 18** · Tahmini süre (Er): ~12–15 dk · Günlük XP tavanı: 400

---

## 1. Hareket (`poly_01_move`)

| Alan | Değer |
|------|--------|
| Hedef | WASD ile işaretli alana yürü; fare ile bakış çevir |
| Ekran TR | Hareket: W A S D · Bakış: Fare |
| Ekran EN | Move: W A S D · Look: Mouse |
| Başarı | `PlayerReachedWaypoint` + `LookDeltaSatisfied` |
| İpucu | Koşmak için Shift; zıplamak için Space |
| Süre | 90 sn |
| XP | 25 |

## 2. Çömel (`poly_02_crouch`)

| Alan | Değer |
|------|--------|
| Hedef | Ctrl/C ile çömel, sonra ayağa kalk |
| Ekran TR | Çömel: Ctrl / C |
| Ekran EN | Crouch: Ctrl / C |
| Başarı | `StanceChanged` Crouch + Stand |
| İpucu | Çömelince silüet küçülür; nişan sabitlenir |
| Süre | 45 sn |
| XP | 25 |

## 3. Yüzüstü (`poly_03_prone`)

| Alan | Değer |
|------|--------|
| Hedef | Z ile yüzüstü yat, sonra kalk |
| Ekran TR | Yüzüstü: Z |
| Ekran EN | Prone: Z |
| Başarı | `StanceChanged` Prone + Stand |
| İpucu | Yüzüstü yavaştır; uzun menzilde avantaj |
| Süre | 45 sn |
| XP | 25 |

## 4. Yana eğilme (`poly_04_lean`)

| Alan | Değer |
|------|--------|
| Hedef | Q ve E ile her iki yana birer kez eğil |
| Ekran TR | Yana eğil: Q / E |
| Ekran EN | Lean: Q / E |
| Başarı | `LeanPerformed` Left + Right |
| İpucu | Köşe arkası için eğil; sürekli eğik kalma |
| Süre | 45 sn |
| XP | 25 |

## 5. Ateş (`poly_05_fire`)

| Alan | Değer |
|------|--------|
| Hedef | Sol tık ateş; 25 m hedefine 5 isabet |
| Ekran TR | Ateş: Sol Tık · Hedef: 25 m |
| Ekran EN | Fire: LMB · Target: 25 m |
| Başarı | `WeaponFiredEvent` ≥1 + `HitConfirmedEvent` ×5 |
| İpucu | Poligonda mermi sınırsız; kısa seriler isabetli |
| Süre | 90 sn |
| XP | 50 |

## 6. Nişan / ADS (`poly_06_ads`)

| Alan | Değer |
|------|--------|
| Hedef | Sağ tık nişan; nişanlıyken 3 isabet |
| Ekran TR | Nişan: Sağ Tık (basılı tut) |
| Ekran EN | ADS: Hold RMB |
| Başarı | `AdsActive` + `HitConfirmedEvent` (WhileAiming) ×3 |
| İpucu | ADS hassasiyet düşürür, yayılmayı daraltır |
| Süre | 90 sn |
| XP | 50 |

## 7. Dürbün (`poly_07_scope`)

| Alan | Değer |
|------|--------|
| Hedef | KNT-76 / JNG-90 ile 100 m’de 2 isabet |
| Ekran TR | Dürbün: Sağ Tık · Mesafe: 100 m |
| Ekran EN | Scope: RMB · Range: 100 m |
| Başarı | `ScopeActive` + `HitConfirmedEvent` (WhileScoped, ≥80 m) ×2 |
| İpucu | KNT ≈3×, JNG ≈6×; kısa durakla |
| Süre | 120 sn |
| XP | 75 |

## 8. Şarjör (`poly_08_reload`)

| Alan | Değer |
|------|--------|
| Hedef | R ile şarjör değiştir; bitene kadar bekle |
| Ekran TR | Şarjör: R |
| Ekran EN | Reload: R |
| Başarı | `WeaponReloadStartedEvent` + `WeaponReloadedEvent` |
| İpucu | Şarjör sırasında ateş yok; kapak arkasında değiştir |
| Süre | 60 sn |
| XP | 25 |

## 9. Ateş modu (`poly_09_firemode`)

| Alan | Değer |
|------|--------|
| Hedef | B ile Single ↔ Auto; her modda ateş |
| Ekran TR | Atış modu: B |
| Ekran EN | Fire mode: B |
| Başarı | `FireModeChanged` Single+Auto + `WeaponFiredEvent` ×2 |
| İpucu | Uzun menzil Tekli, yakın Otomatik |
| Süre | 75 sn |
| XP | 50 |

## 10. El bombası (`poly_10_frag`)

| Alan | Değer |
|------|--------|
| Hedef | G ile frag at; kukla grubuna patlama |
| Ekran TR | El bombası: G |
| Ekran EN | Frag grenade: G |
| Başarı | `ExplosionEvent` **veya** `ItemUsedEvent` (`grenade_frag`, Completed) |
| İpucu | Duvar arkasına sektirme; zamanlamayı öğren |
| Süre | 90 sn |
| XP | 50 |
| ItemId | `grenade_frag` |

## 11. Sis (`poly_11_smoke`)

| Alan | Değer |
|------|--------|
| Hedef | T ile sis at; bulut oluşsun |
| Ekran TR | Sis: T |
| Ekran EN | Smoke: T |
| Başarı | `ItemUsedEvent` (`grenade_smoke`) **veya** `SmokeVolumeSpawned` |
| İpucu | Sis görüş keser; yaralı çekme / geçiş |
| Süre | 75 sn |
| XP | 50 |
| ItemId | `grenade_smoke` |

## 12. İyileşme (`poly_12_heal`)

| Alan | Değer |
|------|--------|
| Hedef | H ile sargı kullan; Completed |
| Ekran TR | İyileş: H · Takviye: J |
| Ekran EN | Heal: H · Boost: J |
| Başarı | `ItemUsedEvent` bandage Started + Completed |
| İpucu | Sargı/ilk yardım 75 HP tavanı; medkit 100 |
| Süre | 90 sn |
| XP | 50 |
| Kurulum | −40 HP + 2× `bandage` |

## 13. Envanter (`poly_13_inventory`)

| Alan | Değer |
|------|--------|
| Hedef | TAB envanter; raftan eşya al |
| Ekran TR | Envanter: TAB |
| Ekran EN | Inventory: TAB |
| Başarı | `InventoryOpened` + `LootPickedUpEvent` |
| İpucu | Ağırlık kapasitesine dikkat; silahlar 1–4 |
| Süre | 90 sn |
| XP | 50 |

## 14. Harita (`poly_14_map`)

| Alan | Değer |
|------|--------|
| Hedef | M harita; işaret koy |
| Ekran TR | Harita: M · İşaret: sol tık |
| Ekran EN | Map: M · Marker: LMB |
| Başarı | `MapOpened` + `MapMarkerPlaced` |
| İpucu | Beyaz çember = sonraki güvenli alan |
| Süre | 75 sn |
| XP | 50 |

## 15. Tim emirleri — Takip / Mevzi (`poly_15_orders_follow_hold`)

| Alan | Değer |
|------|--------|
| Hedef | F1 Takip, sonra F2 Mevzi |
| Ekran TR | Emirler: F1 Takip · F2 Mevzi |
| Ekran EN | Orders: F1 Follow · F2 Hold |
| Başarı | `SquadOrderIssuedEvent` Follow + HoldPosition |
| İpucu | Yalnızca komutan emir verir; komuta kıdemle devralınır |
| Süre | 90 sn |
| XP | 75 |
| Telemetri | `first_order_issued` |

## 16. Tim emirleri — Taarruz / Toplan (`poly_16_orders_attack_regroup`)

| Alan | Değer |
|------|--------|
| Hedef | F3 Taarruz, sonra F4 Toplan |
| Ekran TR | Emirler: F3 Taarruz · F4 Toplan |
| Ekran EN | Orders: F3 Attack · F4 Regroup |
| Başarı | `SquadOrderIssuedEvent` Attack + Regroup |
| İpucu | Taarruz için nişan veya harita işareti |
| Süre | 90 sn |
| XP | 75 |

## 17. Topçu (`poly_17_artillery`)

| Alan | Değer |
|------|--------|
| Hedef | V ile topçu çağır (çağrı kabul) |
| Ekran TR | Topçu: V · Hedef: nişangâh / harita |
| Ekran EN | Artillery: V · Target: aim / map |
| Başarı | `ArtilleryStrikeEvent` (`IsImpact`: false) |
| İpucu | Poligon CD ~25 sn; maç ~150 sn; telsizci gerekli |
| Süre | 120 sn |
| XP | 100 |
| Telemetri | `first_artillery_call` |

## 18. Kirpi (`poly_18_kirpi`)

| Alan | Değer |
|------|--------|
| Hedef | F ile bin; ≥25 m sür; F ile in |
| Ekran TR | Araç: F bin/in · WASD sür |
| Ekran EN | Vehicle: F enter/exit · WASD drive |
| Başarı | `VehicleEntered` + `VehicleDriven` + `VehicleExited` (Kirpi) |
| İpucu | Zırhlıdır ama patlayabilir; yolcu koltukları var |
| Süre | 120 sn |
| XP | 100 |

---

## Tamamlama

- Ekran: «Eğitim tamam. Hızlı Maça hazır mısın?» / «Training complete. Ready for Quick Match?»
- CTA: Hızlı Maç
- Bonus XP: 150 · Rozet: `training_range_basic`
- Adım XP toplamı (bonus hariç): **975** (günlük tavan 400 ile spam engelli)

## Kategori özeti

| Kategori | Adımlar |
|----------|---------|
| movement | 01–04 |
| weapons | 05–09 |
| throwables | 10–11 |
| survival | 12 |
| ui | 13–14 |
| command | 15–17 |
| vehicle | 18 |
