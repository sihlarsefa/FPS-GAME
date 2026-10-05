# U5. UI/UX Akış Diyagramları

Arayüz metinleri Türkçe. Diyagramlar Mermaid.

## 20.1 Ana menü

```mermaid
flowchart TD
  A[Açılış / Logo] --> B[Ana Menü]
  B --> C[Hızlı Maç]
  B --> D[Eğitim / Poligon]
  B --> E[Tim]
  B --> F[Profil / Rütbe]
  B --> G[Ayarlar]
  B --> H[Çıkış]
  C --> I{Intikal seç}
  I -->|T-70| J[Maç yükleniyor]
  I -->|Kirpi| J
  D --> K[TutorialBootstrap / Poligon]
  E --> L[Davet / Bekleme]
  L --> C
  F --> B
  G --> B
```

## 20.2 Maç akışı

```mermaid
stateDiagram-v2
  [*] --> OnMac: PreMatch ~6sn
  OnMac --> Intikal: İntikal animasyonu
  Intikal --> Yagma: İniş / çıkış
  Yagma --> Muharebe: Zone Start
  Muharebe --> Muharebe: Waiting / Shrinking döngüsü
  Muharebe --> Sonuc: Tek tim kaldı / süre
  Muharebe --> Izleyici: Oyuncu öldü
  Izleyici --> Sonuc: Maç bitti
  Sonuc --> [*]: XP & menü
```

## 20.3 Maç içi HUD bilgi mimarisi

```mermaid
flowchart LR
  subgraph Sol
    HP[Can / Zırh / Boost]
    Silah[Silah / mermi]
  end
  subgraph Orta
    Nisangah[Nişangâh]
    Emir[Aktif emir banner]
  end
  subgraph Sag
    Mini[Minimap + zone]
    Tim[Tim listesi + rütbe]
    Topcu[Topçu CD]
  end
  subgraph Alt
    Kill[Kill feed]
    ZoneTxt[Faz / süre]
  end
```

## 20.4 Ölüm ve izleyici

```mermaid
flowchart TD
  O[Hasar alındı] --> P{Can > 0?}
  P -->|Evet| Q[Hasar yönü UI]
  P -->|Hayır| R[Ölüm kamerası]
  R --> S{Komuta devri?}
  S -->|Evet| T[Yeni komutan bildirimi]
  S -->|Hayır| U[Tim yok]
  T --> V[İzleyici: tim arkadaşı seç]
  U --> V
  V --> W[Serbest kamera / sonraki]
  V --> X[Maç sonucu bekle]
  X --> Y[XP özeti]
  Y --> Z[Ana menü / Yeniden]
```

## 20.5 Emir verme (komutan)

```mermaid
sequenceDiagram
  participant O as Oyuncu
  participant UI as Komuta UI
  participant S as SquadOrderService
  participant B as Bot AI
  O->>UI: Emir tuşu / tekerlek
  UI->>UI: Hedef noktası (Attack)
  UI->>S: Issue(order, target)
  S-->>B: SquadOrderIssuedEvent
  B->>B: Decide() öncelik sırası
  B-->>UI: Tim panelinde emir ikonu
```

## 20.6 Topçu çağrısı

```mermaid
flowchart TD
  A[Telsizci hayatta + CD hazır] --> B[Haritada hedef]
  B --> C[Onay]
  C --> D[6 sn ıslık + işaret]
  D --> E[8 mermi]
  E --> F[CD 150 sn]
  A -->|CD dolu| G[UI: kalan süre]
```

## 20.7 Erişilebilirlik notları

- Emir tekerleği: klavye sayıları + fare.  
- Zone: renk + süre metni + ses.  
- Ölüm ekranı: otomatik odak “Yeniden / Menü” (gamepad).
