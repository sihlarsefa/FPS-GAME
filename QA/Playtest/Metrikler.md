# Playtest Metrikleri

Ölçümler oturum sonrası doldurulur. Hedefler geçici QA kabulüdür; cihaz kaydı olmadan “geçti” yazılmaz.

## 1. Oturum metrikleri

| Metrik | Tanım | Nasıl | Hedef (geçici) | Ölçülen |
|--------|-------|-------|----------------|---------|
| Tamamlanan maç | Crash’siz biten maç sayısı | Sayım | ≥ 1 / oturum | |
| Ort. maç süresi | İlk iniş → MatchEnded | Kronometre / log | Kaydet | |
| Erken terk | Brief sonrası 10 dk içinde çıkan | Sayım | 0 ideal | |
| Anket tamamlanma | Gönderilen / davetli | Anket JSON | ≥ %70 | |
| P0 bulgu | S1+P0 ticket | Triage | 0 açık | |
| Zaman kodlu not | Gözlem satırı | Form | ≥ 5 | |

## 2. Oynanış hissedişi (anket ortalaması 1–5)

| Metrik | Soru anahtarı | Hedef | Ölçülen |
|--------|---------------|-------|---------|
| Kontroller | `controls_clarity` | ≥ 3,5 | |
| Tim emirleri | `squad_orders` | ≥ 3,0 | |
| HUD okuma | `hud_readability` | ≥ 3,5 | |
| Adalet hissi | `fairness` | ≥ 3,0 | |
| Tekrar oynama | `replay_intent` | ≥ 3,5 | |
| Performans | `perf_feel` | ≥ 3,0 | |

## 3. Teknik metrikler (mümkünse)

| Metrik | Kaynak | Hedef notu |
|--------|--------|------------|
| P50 / P95 / P99 frametime | PERF senaryoları / Profiler | 1080p orta; cihaz kaydı |
| 1% low FPS | Aynı | Raporla |
| GC spike sayısı | Profiler | Hot path sürekli Alloc yok |
| Ortalama ping (online) | İstemci | Lab RTT ile karşılaştır |
| Yeniden bağlanma başarısı | NET | Politika belgelenmiş olmalı |
| API hata oranı (lab) | IIS log | 5xx = ticket |

## 4. Funnel (tim BR)

```
Brief tamam → Maç A başladı → İniş yaptı → İlk yağma → İlk ölüm/eleme → Maç bitti → Anket
```

Her adımda düşen oyuncu sayısı kaydedilir.

| Adım | Sayı |
|------|------|
| Brief | |
| Maç A start | |
| İniş | |
| Yağma | |
| Anket | |

## 5. Karar kuralları

- `replay_intent` < 3,0 ve ≥ 3 benzer şikâyet → tasarım incelemesi  
- Crash ≥ 1 → P0/P1 ticket olmadan “playtest yeşil” yok  
- `perf_feel` < 3,0 → PERF koşumu zorunlu  

## 6. Oturum kaydı

Tarih: _____________ · Build: _____________ · Karar: Geçti / Şartlı / Kaldı  
İmza: _____________
