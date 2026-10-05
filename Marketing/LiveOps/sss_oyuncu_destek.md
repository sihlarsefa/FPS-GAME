# HAREKÂT — Oyuncu Destek SSS (LiveOps)

**Odak:** Bağlantı, Windows kurulum, sistem gereksinimleri, performans.  
Genel oyun SSS: `Marketing/roadmap/sss.md`.  
Sistem tablosu: `sistem_gereksinimleri_windows.md`.

---

## A. Kurulum (Windows)

**Desteklenen işletim sistemleri?**  
Windows 10 64-bit (21H2+) ve Windows 11 64-bit. 32-bit yok. Linux / macOS istemci hedef değil.

**Nasıl yüklerim?**  
Steam (hedef) veya resmi launcher duyurusu. Klasör yolunda Türkçe karakter / çok uzun path sorun çıkarırsa kısa yol deneyin (ör. `C:\Games\Harekat`).

**Kurulum yarıda kalıyor / doğrulama bozuluyor?**  
1. Steam “Dosyaları doğrula”.  
2. Antivirüs istisnası (oyun klasörü).  
3. Disk doluluğu: min. birkaç GB boş + sayfalama için alan.  
4. Yönetici olarak çalıştırma yalnızca destek isterse.

**DirectX / Visual C++ hatası?**  
Steam normally redistributables yükler. Manuel: güncel VC++ x64 + DirectX End-User Runtime (Microsoft). Sonra yeniden başlat.

**Oyun açılmıyor (siyah ekran)?**  
GPU sürücüsünü temiz kurun; tam ekran yerine pencere; overlay (Discord/Steam) geçici kapat; `sistem_gereksinimleri_windows.md` minimumunu kontrol edin.

---

## B. Bağlantı ve online

**Eşleştirmede takılıyorum?**  
Bölge seçimi, NAT tipi, firewall. Windows Güvenlik Duvarı’nda HAREKÂT / Dedicated izinleri. Router “Strict NAT” ise UPnP veya port yönlendirme (duyurulan portlar).

**Paket kaybı / rubber-banding?**  
Kablolu tercih; Wi-Fi 5 GHz; VPN kapat (gerekmedikçe); arka plan indirme durdur; MTU aşırı düşük olmasın.

**“Sunucuya bağlanılamadı”?**  
Durum sayfası / Discord `#duyurular`. Kendi tarafında: DNS (ör. güvenilir genel DNS), tarih-saat otomatik, proxy kapalı.

**Takım arkadaşım sesi duymuyor?**  
Oyun içi / Discord izni; Windows gizlilik mikrofon; doğru giriş cihazı; Tim ses odası limiti 10.

**Maç ortası kopunca ne olur?**  
Yeniden bağlanmayı deneyin. Turnuvalarda: `turnuva_kural_kitabi.md` §7.

---

## C. Performans ipuçları

**FPS düşükse ne yapayım?**  
1. Önerilen ayar preset’i (Düşük / Orta).  
2. VSync kapatmayı dene; FPS tavanı ekran Hz’e yakın.  
3. Arka plan: tarayıcı, yayın yazılımı yükünü azalt.  
4. Güncel GPU sürücüsü (Game Ready / Adrenalin).  
5. Windows “Oyun modu” ve donanım hızlandırmalı GPU zamanlama — dene / geri al (sisteme göre).  
6. SSD’ye kurulum.

**Stutter (takılma)?**  
Shader derleme ilk maçlarda normal olabilir. Disk %100 ise başka indirmeleri durdur. Tam ekran exclusive dene.

**Yüksek ping ama indirme hızlı?**  
İndirme ≠ gecikme. Coğrafi bölge ve yönlendirme önemli; VPN bazen iyileştirir bazen kötüleştirir — A/B ölçün.

**Düşük uç PC?**  
Minimum satırını karşılayın; çözünürlük %75–85; gölge / AA düşür; izleyici / ikinci monitör yükünü azaltın.

---

## D. Hesap, güvenlik, hile

**Pay-to-win var mı?**  
Hayır. Ödüller kozmetik. `sezon_odulleri.md`.

**Hile gördüm?**  
Klip + maç ID + oyuncu adı → Discord `#turnuva-rapor` veya destek. Teşhir / doxxing yok.

**Hesabım çalındıyasa?**  
Steam / hesap kurtarma; destek ticket; şüpheli e-posta değişimini bildirin.

**Anti-cheat ne ister?**  
Online fazlarda kernel/user-mode bileşen duyurusu yapılır. Yanlış pozitif için destek + log.

---

## E. Turnuva / lig

**Nasıl kaydolurum?**  
`#tim-kayit` + sezon duyurusu. 10+2 roster.

**İtiraz?**  
Maç bitişinden 60 dk; `#turnuva-itiraz`; kanıt zorunlu.

**Ödüller ne zaman gelir?**  
Genelde 72 saat ceza penceresi sonrası.

---

## F. English quick answers

- **OS:** Windows 10/11 64-bit only.  
- **Install stuck:** Verify files, AV exclusion, free disk space.  
- **Lag:** Prefer Ethernet, close VPN/downloads, check NAT.  
- **Low FPS:** Lower preset, update GPU drivers, install on SSD.  
- **P2W?** No — cosmetics only.  
- **Report cheat:** Clip + match ID; no doxxing.
