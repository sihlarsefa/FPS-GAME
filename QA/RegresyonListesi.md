# HAREKÂT — Regresyon Listesi (60 kritik senaryo)

Her sürümde (aday derleme) koşulur. Ayrıntı: `QA/Senaryolar/` ve `QA/cases.json`. Hata bildirimi: [Hata_Sablonu.md](Hata_Sablonu.md).
Geçiş koşulu: P0 satırların tamamı geçer; P1 başarısızlığı sürümü durdurur. Sonuçlar `QA/dashboard.html` ile izlenir.

| # | ID | Modül | Senaryo | Öncelik | Sonuç |
|---|----|-------|---------|---------|-------|
| 1 | MOV-001 | Hareket | Yürüme hızı | P1 | ☐ |
| 2 | MOV-002 | Hareket | İleri sprint | P1 | ☐ |
| 3 | MOV-010 | Hareket | Normal zıplama | P1 | ☐ |
| 4 | MOV-021 | Hareket | Düşme hasarı sınırı | P1 | ☐ |
| 5 | MOV-023 | Hareket | Araçta motor kilidi | P1 | ☐ |
| 6 | WPN-001 | Silahlar | SAR 9: şarjör kapasitesi | P1 | ☐ |
| 7 | WPN-003 | Silahlar | SAR 9: yeniden doldurma | P1 | ☐ |
| 8 | WPN-009 | Silahlar | SAR 9: hasar bileşeni | P1 | ☐ |
| 9 | WPN-021 | Silahlar | SAR 109T: şarjör kapasitesi | P1 | ☐ |
| 10 | WPN-051 | Silahlar | G3A7: şarjör kapasitesi | P1 | ☐ |
| 11 | WPN-071 | Silahlar | JNG-90: şarjör kapasitesi | P1 | ☐ |
| 12 | INV-001 | Envanter | İlk ana silahı alma | P1 | ☐ |
| 13 | INV-003 | Envanter | Dolu ana yuva değiştirme | P1 | ☐ |
| 14 | INV-014 | Envanter | Ölüm yağması | P1 | ☐ |
| 15 | HEAL-001 | İyileşme | Sargı Bezi: tam kullanım | P1 | ☐ |
| 16 | HEAL-006 | İyileşme | İlk Yardım Çantası: tam kullanım | P1 | ☐ |
| 17 | HEAL-011 | İyileşme | Sıhhiye Çantası: tam kullanım | P1 | ☐ |
| 18 | ARM-001 | Zırh | Yelek Sv.1: azaltma | P1 | ☐ |
| 19 | ARM-010 | Zırh | Kask Sv.1: azaltma | P1 | ☐ |
| 20 | TRN-001 | İntikal | T-70 tercihinin uygulanması | P1 | ☐ |
| 21 | TRN-005 | İntikal | Erken iniş engeli | P1 | ☐ |
| 22 | TRN-010 | İntikal | İntikalden maç fazına | P1 | ☐ |
| 23 | CMD-001 | Komuta ve topçu | Takip emri | P1 | ☐ |
| 24 | CMD-005 | Komuta ve topçu | Komutan dışı emir | P1 | ☐ |
| 25 | CMD-010 | Komuta ve topçu | Topçu bekleme süresi | P1 | ☐ |
| 26 | ZON-001 | Bölge ve maç | Başlangıçta zone hasarı | P1 | ☐ |
| 27 | ZON-006 | Bölge ve maç | Doğrusal daralma | P1 | ☐ |
| 28 | ZON-012 | Bölge ve maç | Yeni maç zone sıfırlama | P1 | ☐ |
| 29 | AI-001 | Yapay zekâ | Dost hedefi reddi | P1 | ☐ |
| 30 | AI-005 | Yapay zekâ | Zone emre üstün | P1 | ☐ |
| 31 | HUD-001 | HUD ve harita | Can göstergesi | P1 | ☐ |
| 32 | HUD-010 | HUD ve harita | Dost dünya işareti | P1 | ☐ |
| 33 | MENU-001 | Menüler ayarlar kariyer | Kurulum tim alt sınırı | P1 | ☐ |
| 34 | MENU-010 | Menüler ayarlar kariyer | FPS ayarı | P1 | ☐ |
| 35 | RNG-001 | Atış Poligonu | Doğru sahne | P1 | ☐ |
| 36 | PERF-001 | Performans | 20 savaşan temel ölçüm | P1 | ☐ |
| 37 | NET-001 | Online ve dayanıklılık | Kuyruk temel akışı | P1 | ☐ |
| 38 | NET-005 | Online ve dayanıklılık | Yanlış davet kodu | P1 | ☐ |
| 39 | NET-010 | Online ve dayanıklılık | Yüzde 1 kayıp | P1 | ☐ |
| 40 | API-001 | Backend uçları | GET /health | P1 | ☐ |
| 41 | API-010 | Backend uçları | POST /squads/join | P1 | ☐ |
| 42 | WIN-001 | Windows matrisi | DPI yüzde 100 | P1 | ☐ |
| 43 | KRP-001 | Sürülebilir Kirpi | Kirpi'ye binme | P1 | ☐ |
| 44 | KRP-002 | Sürülebilir Kirpi | Gaz, fren ve direksiyon | P1 | ☐ |
| 45 | KRP-003 | Sürülebilir Kirpi | Araçtan inme | P1 | ☐ |
| 46 | KRP-005 | Sürülebilir Kirpi | Araç hasarı ve imha | P1 | ☐ |
| 47 | KRP-006 | Sürülebilir Kirpi | Kirpi ile intikal | P1 | ☐ |
| 48 | DEV-001 | Geliştirici konsolu | Konsolu açma/kapama | P1 | ☐ |
| 49 | DEV-004 | Geliştirici konsolu | Çevrimiçi maçta hile engeli | P0 | ☐ |
| 50 | PNL-001 | Online paneller | Giriş/hesap paneli | P1 | ☐ |
| 51 | PNL-004 | Online paneller | Sunucuya bağlanma | P0 | ☐ |
| 52 | PNL-005 | Online paneller | Backend kapalıyken çevrimdışı | P0 | ☐ |
| 53 | SRV-001 | Windows sunucu | Sunucu servisini başlatma | P0 | ☐ |
| 54 | SRV-002 | Windows sunucu | Dedicated maç örneği | P0 | ☐ |
| 55 | SRV-003 | Windows sunucu | Örnek kapatma | P1 | ☐ |
| 56 | SRV-004 | Windows sunucu | Çökme sonrası toparlama | P1 | ☐ |
| 57 | OVR-001 | İçerik override | Varsayılan içerik | P1 | ☐ |
| 58 | OVR-004 | İçerik override | Bozuk override | P1 | ☐ |
| 59 | BAL-001 | Denge | SAR 9: BalanceCalc gövde TTK | P1 | ☐ |
| 60 | WIN-002 | Windows matrisi | DPI yüzde 150 | P1 | ☐ |
