#!/usr/bin/env python3
"""FAZ 3 senaryolarını cases.json'a ekler (idempotent) ve RegresyonListesi.md üretir.
Sıra: seed_cases.py -> add_faz3_cases.py -> export_scenarios.py"""
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
CASES = ROOT / "cases.json"
PRE = "Windows istemci, Unity Editör veya yerel derleme; "
def c(i, mod, title, pre, steps, exp, pri, src):
    return dict(id=i, module=mod, title=title, precondition=PRE + pre, steps=steps, expected=exp,
                priority=pri, source=src, basis="FAZ3 görev C3-8", status="Çalıştırılmadı")
K, D, P, S, O = ("Sürülebilir Kirpi", "Geliştirici konsolu", "Online paneller", "Windows sunucu", "İçerik override")
KS = "Assets/_Project/Scripts (araç/Kirpi)"
NEW = [
 c("KRP-001",K,"Kirpi'ye binme","Kirpi yakınında, ayakta.",["Kirpi'ye yaklaş","Bin tuşuna bas"],"Sürücü koltuğuna geçilir; kamera araç kamerasına döner; HUD araç göstergesi açılır.","P1",KS),
 c("KRP-002",K,"Gaz, fren ve direksiyon","Kirpi'de sürücü.",["W ile hızlan","S ile fren","A/D ile dön"],"Araç girdiyle tutarlı hızlanır, durur ve döner; ani takla yok.","P1",KS),
 c("KRP-003",K,"Araçtan inme","Kirpi sürülüyor.",["Dur","İn tuşuna bas"],"Oyuncu araç yanında güvenli noktada belirir; motor kilidi kalkar (bkz. MOV-023).","P1",KS),
 c("KRP-004",K,"Yolcu koltukları","İki oyuncu veya bot ile.",["Sürücü bin","İkinci oyuncu bin"],"Yolcular boş koltuklara oturur; dolu koltuk reddedilir.","P2",KS),
 c("KRP-005",K,"Araç hasarı ve imha","Kirpi, silah veya bomba ile.",["Araca ateş et/bomba at","Sağlık sıfırlanana kadar devam"],"Hasar kademeleri görünür; imhada içindekiler hasar alır veya atılır; enkaz kalır.","P1",KS),
 c("KRP-006",K,"Kirpi ile intikal","Maç, intikal aşaması.",["Kirpi ile intikal et","Bölgeye in"],"Kirpi intikali tamamlanır; oyuncular indikten sonra sürülebilir araç olarak kalır.","P1",KS),
 c("KRP-007",K,"Eğimde ve engelde sürüş","Arazi sahnesi.",["Rampaya çık","Alçak engele çarp"],"Araç tırmanır; takılma halinde geri alınabilir; yere gömülme yok.","P2",KS),
 c("DEV-001",D,"Konsolu açma/kapama","Menü veya maçta.",["Konsol tuşuna bas","Tekrar bas"],"Konsol açılır/kapanır; açıkken oyuncu girdisi kilitlenir.","P1","Assets/_Project/Scripts (konsol)"),
 c("DEV-002",D,"Komut listesi ve yardım","Konsol açık.",["help yaz","Bilinmeyen komut yaz"],"Komut listesi görünür; bilinmeyen komut anlaşılır hata verir, çökme yok.","P2","Assets/_Project/Scripts (konsol)"),
 c("DEV-003",D,"Hile komutları yerel modda","Atış Poligonu.",["Silah/can komutu çalıştır"],"Poligonda komutlar etkili olur; sonuç konsola yazılır.","P1","Assets/_Project/Scripts (konsol)"),
 c("DEV-004",D,"Çevrimiçi maçta hile engeli","Online maç, normal oyuncu.",["Hile komutu çalıştır"],"Sunucu yetkisiz komutu reddeder; durum değişmez.","P0","Assets/_Project/Scripts (konsol)"),
 c("DEV-005",D,"Komut geçmişi","Konsol açık.",["Üç komut yaz","Yukarı oka bas"],"Önceki komutlar sırayla gelir.","P3","Assets/_Project/Scripts (konsol)"),
 c("DEV-006",D,"Release derlemede konsol","Release build.",["Konsol tuşuna bas"],"Konsol kapalı veya yetki kapısı arkasındadır.","P1","Assets/_Project/Scripts (konsol)"),
 c("PNL-001",P,"Giriş/hesap paneli","Ana menü, backend açık.",["Giriş panelini aç","Geçerli hesapla giriş yap"],"Giriş başarılı; oyuncu adı ve rütbe görünür.","P1","Assets/_Project/Scripts/Presentation/UI/MainMenuController.cs"),
 c("PNL-002",P,"Giriş hatası","Backend açık.",["Yanlış parola gir"],"Anlaşılır hata mesajı; tekrar denenebilir; parola düz metin görünmez.","P1","Assets/_Project/Scripts/Presentation/UI/MainMenuController.cs"),
 c("PNL-003",P,"Sunucu listesi","Giriş yapılmış.",["Sunucu paneline gir","Yenile"],"Sunucular ad, oyuncu sayısı ve gecikmeyle listelenir; boş liste mesajı vardır.","P1","Assets/_Project/Scripts/Presentation/UI/MainMenuController.cs"),
 c("PNL-004",P,"Sunucuya bağlanma","Çalışan sunucu.",["Sunucu seç","Bağlan"],"Lobiye geçilir; hata durumunda menüye dönülür.","P0","Assets/_Project/Scripts/Presentation/Bootstrap/GameSession.cs"),
 c("PNL-005",P,"Backend kapalıyken çevrimdışı","Backend durdurulmuş.",["Oyunu aç","Atış Poligonu'nu başlat"],"Oyun çökmez; online panelleri 'bağlanılamadı' gösterir; offline mod çalışır.","P0","Assets/_Project/Scripts/Presentation/Bootstrap/GameSession.cs"),
 c("PNL-006",P,"Steam kancası","Steam yok.",["Menüde Steam öğelerine bak"],"Steam yoksa öğeler pasif/gizli; hata günlüğü yok.","P3","Assets/_Project/Scripts/Presentation/UI/MainMenuController.cs"),
 c("SRV-001",S,"Sunucu servisini başlatma","Windows Server, Backend/Harekat.ServerManager.",["Servisi/konsolu başlat"],"Yönetici açılır, port dinler, günlük yazar.","P0","Backend/Harekat.ServerManager/Program.cs"),
 c("SRV-002",S,"Dedicated maç örneği","ServerManager çalışıyor.",["Maç örneği oluştur"],"Dedicated sunucu işlemi başlar ve listede görünür.","P0","Backend/Harekat.ServerManager/Program.cs"),
 c("SRV-003",S,"Örnek kapatma","Çalışan örnek.",["Örneği durdur"],"İşlem temiz kapanır, port serbest kalır, oyuncular menüye döner.","P1","Backend/Harekat.ServerManager/Program.cs"),
 c("SRV-004",S,"Çökme sonrası toparlama","Çalışan örnek.",["Dedicated işlemi öldür"],"Yönetici kaybı algılar; liste güncellenir; yeni örnek açılabilir.","P1","Backend/Harekat.ServerManager/Program.cs"),
 c("SRV-005",S,"appsettings yapılandırması","appsettings.json değiştirilmiş.",["Portu değiştir","Yeniden başlat"],"Yeni ayar uygulanır; geçersiz değerde açık hata ile durur.","P2","Backend/Harekat.ServerManager/appsettings.json"),
 c("SRV-006",S,"Güvenlik duvarı ve dış erişim","Ayrı makineden istemci.",["Sunucuya LAN/İnternetten bağlan"],"Gerekli portlar açıkken bağlanılır; kapalıyken zaman aşımı mesajı.","P1","Backend/Harekat.ServerManager"),
 c("OVR-001",O,"Varsayılan içerik","Override yok.",["Oyunu başlat"],"Yer tutucu/varsayılan varlıklar yüklenir; eksik varlık uyarısı çökertmez.","P1","Assets/ThirdParty/README.md"),
 c("OVR-002",O,"Model override","Geçerli model ThirdParty'ye konmuş.",["Oyunu başlat","Silahı gözle"],"Override model kullanılır; ölçek/pivot doğru.","P2","Assets/ThirdParty/README.md"),
 c("OVR-003",O,"Ses override","Geçerli ses dosyası konmuş.",["Silahla ateş et"],"Override ses çalınır; ses seviyesi ayarlara uyar.","P2","Assets/_Project/Scripts/Infrastructure/Audio/GameAudio.cs"),
 c("OVR-004",O,"Bozuk override","Geçersiz/bozuk dosya konmuş.",["Oyunu başlat"],"Varsayılana düşülür, uyarı günlüğe yazılır, oyun çökmez.","P1","Assets/ThirdParty/README.md"),
 c("OVR-005",O,"Malzeme override","Malzeme dosyası konmuş.",["Sahneyi aç"],"Malzeme uygulanır; shader eksikse yedek malzeme kullanılır.","P2","Assets/_Project/Scripts/Infrastructure/Rendering/MaterialLibrary.cs"),
]
REG = """MOV-001 MOV-002 MOV-010 MOV-021 MOV-023 WPN-001 WPN-003 WPN-009 WPN-021 WPN-051 WPN-071 INV-001 INV-003 INV-014 HEAL-001 HEAL-006 HEAL-011
ARM-001 ARM-010 TRN-001 TRN-005 TRN-010 CMD-001 CMD-005 CMD-010 ZON-001 ZON-006 ZON-012 AI-001 AI-005 HUD-001 HUD-010 MENU-001 MENU-010 RNG-001 PERF-001
NET-001 NET-005 NET-010 API-001 API-010 WIN-001 KRP-001 KRP-002 KRP-003 KRP-005 KRP-006 DEV-001 DEV-004 PNL-001 PNL-004 PNL-005 SRV-001 SRV-002 SRV-003 SRV-004 OVR-001 OVR-004 BAL-001 WIN-002""".split()
def main():
    cases = json.loads(CASES.read_text(encoding="utf-8"))
    have = {x["id"] for x in cases}
    cases += [n for n in NEW if n["id"] not in have]
    CASES.write_text(json.dumps(cases, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    by = {x["id"]: x for x in cases}
    ids = [i for i in REG if i in by]
    assert len(ids) == 60, len(ids)
    L = ["# HAREKÂT — Regresyon Listesi (60 kritik senaryo)", "",
         "Her sürümde (aday derleme) koşulur. Ayrıntı: `QA/Senaryolar/` ve `QA/cases.json`. Hata bildirimi: [Hata_Sablonu.md](Hata_Sablonu.md).",
         "Geçiş koşulu: P0 satırların tamamı geçer; P1 başarısızlığı sürümü durdurur. Sonuçlar `QA/dashboard.html` ile izlenir.", "",
         "| # | ID | Modül | Senaryo | Öncelik | Sonuç |", "|---|----|-------|---------|---------|-------|"]
    for n, i in enumerate(ids, 1):
        x = by[i]; L.append(f"| {n} | {i} | {x['module']} | {x['title']} | {x['priority']} | ☐ |")
    (ROOT / "RegresyonListesi.md").write_text("\n".join(L) + "\n", encoding="utf-8")
    print(len(cases), "senaryo;", len(ids), "regresyon")
main()
