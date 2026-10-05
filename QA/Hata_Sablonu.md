# HAREKÂT — Hata Raporu Şablonu

Kopyala-yapıştır için. GitHub Issue metni önerisidir; **`.github/` klasörüne yazılmaz** (Cursor F2 sahipliği).

---

## Hata kaydı

```markdown
### Başlık
[MODÜL] Kısa belirti (ör. [ZON] Güvenli alanda zone hasarı)

### Ortam
- Build / commit:
- Platform: Windows istemci | Dedicated Server | macOS Editör | API lab
- OS / GPU / sürücü / DPI:
- Dil: TR / EN
- Çevrimiçi: Hayır / Evet (RTT/kayıp varsa)

### Öncelik × Önem
- Önem (Severity): S1 / S2 / S3 / S4
- Öncelik (Priority): P0 / P1 / P2 / P3
- Matris hücresi: (aşağıdaki tabloya göre)

### Senaryo bağlantısı
- Senaryo ID: (ör. ZON-002)
- Smoke satırı: (varsa Sxx)

### Ön koşullar
-

### Adımlar
1.
2.
3.

### Beklenen
-

### Gerçekleşen
-

### Kanıt
- Ekran videosu / log / Profiler / harita koordinatı / API yanıtı (token gizle)

### Etki
- Oynanış / ilerleme / veri / güvenlik:

### Geçici çözüm
- Yok / …

### Notlar
-
```

---

## Önem (Severity)

| Kod | Tanım | Örnek |
|-----|-------|-------|
| **S1** | Çökme, veri kaybı, güvenlik deliği, maç sonucunun bozulması | Yetkisiz `/matches/.../result`, XP çiftlenmesi |
| **S2** | Çekirdek döngü kırık; oynanış ilerleyemez | Hareket yok, silah ateş etmiyor, zone sürekli öldürüyor |
| **S3** | Önemli özellik bozuk; geçici çözüm var | Topçu bekleme UI yanlış, tek silah ADS örtüsü |
| **S4** | Kozmetik, metin, nadir UX | İkon hizası, küçük yazım |

## Öncelik (Priority)

| Kod | Tanım |
|-----|-------|
| **P0** | Sürümü durdur; hemen düzelt |
| **P1** | Bu milestone’da düzelmeli |
| **P2** | Planlı sprint |
| **P3** | Biriktir / backlog |

## Önem × Öncelik matrisi (önerilen ilk atama)

|  | **P0** | **P1** | **P2** | **P3** |
|--|--------|--------|--------|--------|
| **S1** | Güvenlik, kayıp, çift sonuç | Kritik çökme (nadir repro) | — | — |
| **S2** | Ana döngü tamamen kapalı | Ana döngü bozuk, repro stabil | Kenar platform | — |
| **S3** | — | Sık görülen özellik hatası | Orta sıklık | Nadir |
| **S4** | — | — | Görünür metin | Kozmetik |

**Kural:** S1 her zaman en az P1; güvenlik ve yetki kaçakları P0.

---

## GitHub Issue şablon önerisi (yalnız metin)

Aşağıyı ileride `.github/ISSUE_TEMPLATE/bug_harekat.md` olarak eklemek Cursor’un işidir.

```yaml
name: HAREKÂT Bug
description: Oyun / API / sunucu hatası
title: "[MOD] "
labels: ["bug", "needs-triage"]
body:
  - type: dropdown
    id: platform
    attributes:
      label: Platform
      options: [Windows istemci, Dedicated Server, macOS Editör, API lab, Web portal]
  - type: input
    id: scenario
    attributes:
      label: Senaryo ID
      placeholder: MOV-001
  - type: dropdown
    id: severity
    attributes:
      label: Önem
      options: [S1, S2, S3, S4]
  - type: dropdown
    id: priority
    attributes:
      label: Öncelik
      options: [P0, P1, P2, P3]
  - type: textarea
    id: steps
    attributes:
      label: Adımlar / Beklenen / Gerçekleşen
  - type: textarea
    id: evidence
    attributes:
      label: Kanıt (token/parola yok)
```

## Etiket önerileri

`bug`, `smoke-fail`, `P0`, `P1`, `windows-client`, `dedicated-server`, `api`, `perf`, `ai`, `zone`, `needs-repro`

## Triage kontrol listesi

- [ ] Token / parola / server key issue gövdesinde yok  
- [ ] Senaryo ID veya Smoke satırı bağlı  
- [ ] Tek sorun = tek ticket  
- [ ] “Çalışmıyor” yerine ölçülebilir belirti  
- [ ] Engelli ortam mı, yoksa gerçek regresyon mu ayrıldı  
