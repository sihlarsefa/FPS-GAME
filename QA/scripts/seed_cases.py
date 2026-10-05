"""Vaka veri setinin kaynakları; yalnız QA/ altına yazar. Çalıştırma gerçek oyunu sınamaz."""
from pathlib import Path
import json, re, csv, hashlib
ROOT=Path(__file__).resolve().parents[2]
rows=[]
S='Assets/_Project/Scripts/'
def group(code,module,source,fixture,lines,kind='Sözleşme'):
    for line in lines.strip().splitlines():
        title,steps,expected,*p=line.split('|')
        rows.append(dict(id=f'{code}-{sum(r["module"]==module for r in rows)+1:03}',module=module,title=title,precondition=fixture,steps=steps.split(' ~ '),expected=expected,priority=p[0] if p else 'P1',source=source,basis=kind,status='Çalıştırılmadı'))
group('MOV','Hareket',S+'Infrastructure/Player/CharacterControllerMotor.cs','Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.', '''
Yürüme hızı|W ile 5 saniye yürü ~ Başlangıç/bitiş koordinatını kaydet|Engelsiz düz hatta yaklaşık 23 m; nominal hız 4,6 m/sn; ölçüm toleransı yüzde 5.
İleri sprint|W+Shift ile 5 saniye ilerle ~ Bırakıp yürümeye geç|Sprint yaklaşık 36 m; bırakınca yürüyüşe döner; hız takılı kalmaz.
Geri sprint engeli|S+Shift ile 5 saniye geri yürü ~ İleri sprint mesafesiyle karşılaştır|Geri hareket ileri sprintin 7,2 m/sn hızına çıkmaz.
Çapraz hız|W ile 5 saniye ölç ~ Aynı zeminde W+D ile ölç|Çapraz toplam hız düz yürüyüşten yüzde 5 fazla olmaz; hız vektörü normalize edilir.
Çömelme anahtarı|C bas bırak ~ 5 saniye yürü ~ C ile ayağa kalk|Çömelme kalıcıdır; nominal hız 2,4 m/sn; ikinci basış ayakta duruşa döner.
Çömelme basılı tutma|Ctrl basılı yürüyüp bırak ~ Kamera yüksekliğini izle|Ctrl bırakılınca yeterli tavan boşluğunda ayağa kalkılır.
Yüzüstü hareket|Z ile yat ~ 5 saniye yürü ~ Z ile kalk|Nominal hız 1,1 m/sn; yüzüstü kamera zeminin içine girmez.
Çömelirken zıplama|C ile çömel ~ Space bas|Çömelme sırasında zıplama başlamaz.
Yatarken zıplama|Z ile yat ~ Space bas|Yüzüstü duruşta zıplama başlamaz.
Normal zıplama|Ayakta Space bas ~ Havadayken tekrar Space bas|Tek zıplama oluşur; havada ikinci zıplama üretilmez.
Alçak tavanda kalkış|1,3 m tavan altına çömelerek gir ~ C ile kalkmayı dene|Ayakta kapsül tavana geçmez; boşluk olmadan ayakta duruşa geçilmez.
Yatış tavan geçişi|0,9 m yüksek tünelde yat ~ Z ile kalkmayı dene|Kapsül engelle kesişmez; tünelden çıkınca kalkış mümkün olur.
Kapı genişliği|1,3 m açıklıktan ayakta geç ~ İki yönde tekrarla|Sözleşmedeki asgari kapı açıklığı iki yönde takılmadan geçilir.
Rampa geçişi|35 derece rampadan yukarı yürü ~ Aşağı dön|Merdiven yerine kullanılan rampa kontrollü geçilir; oyuncu zeminden düşmez.
Duvar çarpışması|Düz duvara W ile 5 saniye yürü ~ Köşede çapraz yürü|Duvarın içinden geçilmez; köşede kalıcı kilitlenme olmaz.
Sol eğilme|Q basılı nişan al ~ Q bırak|Kamera sola eğilir; bırakınca merkezine döner, hitbox duvar arkasını bedelsiz aşmaz.
Sağ eğilme|E basılı nişan al ~ E bırak|Sağa eğilme ve merkez dönüşü soldaki davranışla tutarlıdır.
Sprint sırasında eğilme|W+Shift sırasında Q bas ~ Sprint bırak|Sprint sırasında eğilme uygulanmaz; sprint bitince kontrol düzelir.
Kamera düşey sınır|Fareyi sürekli yukarı sonra aşağı hareket ettir|Bakış takla atmaz; düşey açı sınırlarında kalır.
Ters Y uygulaması|Ayarlar ters Y aç ~ Fareyi yukarı hareket ettir ~ Ayarı kapat|Bakış yönü anında tersine döner; kapatınca varsayılan yön döner.
Düşme hasarı sınırı|Test düzeneğinde 12 m/sn iniş hızı ayarla ~ İnişi kaydet|12 m/sn için düşme hasarı 0 olur; ölüm olayı oluşmaz.
Düşme hasarı üstü|Test düzeneğinde 16 m/sn iniş ayarla ~ Can değişimini ölç|Zırh dışı düşme hasarı (16−12)×7,5=30; yalnız bir iniş uygulanır.
Araçta motor kilidi|Kirpi sürücü koltuğuna gir ~ Hareket tuşlarını kullan|Karakter yaya motoruyla koltuktan uzaklaşmaz; yalnız araç hareketi uygulanır.
Odak kaybı|Yürürken Alt+Tab yap ~ Tuşları dışarıda bırak ~ Oyuna dön|Sıkışmış tuş yüzünden sürekli hareket oluşmaz; fare kilidi uygun geri yüklenir.
Duruş hitbox uyumu|İkinci test istemcisinden ayakta/çömelmiş/yatan hedefi gözle ~ Görünen başa ateş et|Görünen beden ve hasar alanı her duruşta hizalıdır; boş havaya görünmez hitbox kalmaz.
''')
# Her silahın ayrı çalıştırılabilir katalog temelli kabul testleri.
catalog=json.loads((ROOT/'Tools/BalanceCalc/out/catalog.json').read_text())
for w in catalog['weapons']:
    name=w['display_name']; base=f"Atış Poligonu test düzeneği; {name}; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli."
    damage=w['damage']; pellet=w['pellet_count']; scope='Dürbün örtüsü görünür' if w['has_scope'] else 'Dürbün örtüsü açılmaz; standart ADS görünür'
    group('WPN','Silahlar',S+'Application/Catalogs/WeaponCatalog.cs',base,f'''
{name}: şarjör kapasitesi|Silahı kuşan ~ HUD şarjör sayısını kaydet ~ Bir atış yap|Başlangıç {w['magazine_size']} mermi; her tetiklenen atışta bir mermi azalır, saçma sayısı {pellet} olsa da maliyet bir fişektir.
{name}: boş tetik|Şarjörü tüket ~ Son mermiden sonra tetiği üç kez çek|0 altında mühimmat oluşmaz; boş tetik hasar vermez.
{name}: yeniden doldurma|Şarjörden 3 mermi harca ~ R bas ~ Tamamlanma süresini ölç|Katalog süresi {w['reload_duration_seconds']} sn (görüntü toleransı 0,15 sn); şarjör kapasiteye döner, yedekten tam 3 alınır.
{name}: yedeksiz doldurma|Yedek mühimmatı sıfırla ~ Şarjörden 3 harca ~ R bas|Yeniden doldurma başlamaz; mevcut şarjör mermisi kaybolmaz.
{name}: doldurma iptali|3 mermi harca ~ R bas ~ Süre bitmeden başka silaha geç ~ Geri dön|İptal edilen doldurma bedelsiz mermi eklemez; HUD takılı ilerleme göstermez.
{name}: atış aralığı|Uygun modda 5 atışı video/zaman kaydına al ~ Ardışık atış aralığını ölç|Ateş aralığı {w['fire_interval_seconds']} sn altına inmez (bir simülasyon tick toleransı); sabit makro kullanılmaz, gözlenen gecikme ayrı raporlanır.
{name}: atış modu döngüsü|B tuşuna tüm seçenekler dönene kadar bas ~ Her modda tetiği basılı tut|Sıra katalogla uyumlu: {', '.join(w['fire_modes'])}; Single tek basışta bir atış, Auto basılı tutmada devam, yalnız listelenen modlar seçilebilir.
{name}: ADS ve büyütme|RMB ile nişan al ~ Görüş açısı ve örtüyü gözle ~ Bırak|{scope}; katalog büyütmesi {w['ads_zoom']}×; bırakınca temel görüş açısına döner.
{name}: hasar bileşeni|Hedefi FalloffStart öncesine yerleştir ~ Gövdeye tek mermi veya kontrollü tek saçma isabet ettir ~ Can farkını kaydet|Zırhsız gövde bileşen hasarı {damage}; pompalıda her saçma ayrı hasar bileşenidir, tüm saçmalar toplamı can tavanına kırpılır.
{name}: menzil düşüşü|Aynı zırhsız hedefi {w['falloff_end']} m mesafeye koy ~ Tek mermi/saçma gövde hasarını kaydet|Menzil sonu çarpanı {w['min_damage_factor']}; tek bileşen hasarı {round(damage*w['min_damage_factor'],4)}; kaçan atışlar hasar ölçümüne katılmaz.
''')
group('INV','Envanter',S+'Application/Services/InventoryService.cs','Offline test sahnesi; boş envanter, yerde bilinen eşya yığınları; başlangıç kapasitesi 60; doğrudan fixture yoksa Engelli.', '''
İlk ana silahı alma|Yerdeki MPT-76 üzerine bak ~ F bas ~ Tab aç|Boş ana yuvaya alınır ve otomatik kuşanılır; yerdeki silah tek kez kaybolur.
İkinci ana yuva|MPT-76 varken G3A7 al ~ Yuvaları kontrol et|İkinci ana yuva dolar; ilk silah korunur.
Dolu ana yuva değiştirme|İki ana yuva doluyken birini seç ~ KNT-76 al|Seçili ana silah yerde düşer; yeni silah seçili yuvada olur.
Tabanca aktifken ana silah alma|İki ana yuvayı doldur ~ Tabancayı seç ~ MPT-55 al|Ana yuva 0 değiştirilir; tabanca yuvası korunur.
Tabanca yuvası|SAR 9 al ~ Canik TP9 al|Tek tabanca yuvası 2 kullanılır; eski tabanca yere düşer.
Düşen silahın mermisi|MPT-76 şarjörünü 7 mermiye indir ~ Silahı bırak ~ Yeniden al|Yerdeki ve geri alınan silahta 7 mermi korunur; tam şarjöre bedelsiz dönmez.
Boş yuva seçimi|Tek silahla boş yuvanın numara tuşuna bas|Boş yuva geçerli silah gibi kuşanılmaz; mevcut silah bozulmaz.
Tekerlek yuva döngüsü|Bir ana silah ve tabancayla tekerleği iki yönde çevir|Yalnız dolu yuvalar arasında döner; sınırda takılmaz.
Silahsız yumruk|X ile silahı indir ~ 2 m içindeki hedefe LMB bas|Yumruk uygulanır; silah mermisi harcanmaz.
Mühimmat kısmi alma|Kapasitede 5 ağırlık boşluk bırak ~ 30 adet 5.56 yığınını al|Her mermi 0,5 ağırlık olduğundan 10 alınır; yerde 20 kalır.
Kapasite doluyken alma|Yükü tam kapasiteye getir ~ Bir bandaj almayı dene|Alım reddedilir; yerde eşya ve mevcut envanter değişmez.
Kuşanılan ekipman ağırlığı|Boş envanterde kask/yelek/silah kuşan ~ Ağırlık sayacını izle|Kuşanılan teçhizat yığın ağırlığına eklenmez; yelek kapasiteye 50 ekler.
Çanta kaybında fazla yük|Sv.3 çantayla 200 ağırlık taşı ~ Çantayı bırak ~ Yeni mühimmat almayı dene|Mevcut eşyalar sessizce silinmez; fazla yük gösterilir ve yeni yığın alınmaz.
Ölüm yağması|Mermi ve iki silahla öl ~ İkinci gözlemciyle düşen eşyaları say|DropLootOnDeath açıkken envanter bir kez düşer; aynı ölümde çoğalma olmaz.
''')
meds=[('Sargı Bezi',4,50,60,75),('İlk Yardım Çantası',6,20,75,75),('Sıhhiye Çantası',8,20,100,100)]
for name,seconds,start,end,cap in meds:
 group('HEAL','İyileşme',S+'Application/Services/ItemUseService.cs',f'100 azami can; envanterde 2 adet {name}; boost 0; can test düzeneği erişilebilir.',f'''
{name}: tam kullanım|Canı {start} yap ~ Envanterden kullan ~ {seconds} sn bekle|Can {end} olur; eşya sayısı bir azalır; kullanım çubuğu kapanır.
{name}: iyileşme tavanı|Canı {cap} yap ~ Kullanmayı dene|Kullanım başlamaz; can ve eşya sayısı değişmez.
{name}: ateşle iptal|Canı {start} yap ~ Kullan ~ Süre dolmadan ateş et|Kullanım iptal olur; eşya harcanmaz, iyileşme uygulanmaz.
{name}: silah değişiminde iptal|Canı {start} yap ~ Kullan ~ İkinci silahı seç|Kullanım/animasyon kapanır; eşya tüketilmez.
{name}: kullanımda hız|Canı {start} yap ~ Kullanırken düz yürü ~ Kullanımdan sonra yürü|Kullanımda hız çarpanı 0,5; bitiş/iptal sonrası 1,0 olur.
''')
group('HEAL','İyileşme',S+'Application/Services/BoostService.cs','Test sahnesi; 100 azami can, yaralı karakter, enerji içeceği/ağrı kesici; boost sayacı okunabilir.', '''
Enerji içeceği|Boost 0 iken enerji içeceğini kullan ~ 4 saniye bekle|40 boost eklenir; zamanla azalma varsa bitiş tick sırası kaydedilir; eşya bir azalır.
Ağrı kesici|Boost 0 iken ağrı kesici kullan ~ 6 saniye bekle|60 boost eklenir; can anlık tam dolmaz.
Boost tavanı|Boost 100 iken takviye kullanmayı dene|Kullanım başlamaz; eşya boşa harcanmaz.
Boost sönümü|Boost 60 yap ~ 10 saniye eşya kullanmadan bekle|Boost yaklaşık 6 azalır; 0 altına inmez.
Ölüm sırasında iyileşme|Sargı kullan ~ Süre dolmadan öldürücü hasar al|Ölü karakter iyileşmez; kullanım sonlandırılır.
Zıplamayla kullanım iptali|Yaralıyken H bas ~ Kullanım sırasında Space bas|Kullanım iptal olur ve tüketim gerçekleşmez.
Yaralanma tek ölüm bildirimi|1 can hedefe aynı karede iki öldürücü isabet uygula|Ölüm, skor ve yağma tek kez işlenir; öldürme sayısı çift artmaz.
''')
for kind,durs in [('Yelek',[200,220,250]),('Kask',[80,150,230])]:
 for level,(dur,red) in enumerate(zip(durs,[.30,.40,.55]),1):
  hit='gövde' if kind=='Yelek' else 'kafa'
  group('ARM','Zırh',S+'Application/Services/DamageCalculator.cs',f'Test hedefinde yeni Sv.{level} {kind}, dayanıklılık {dur}; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.',f'''
{kind} Sv.{level}: azaltma|{hit} bölgesine 40 ham hasar uygula ~ Can ve dayanıklılık farkını ölç|Can kaybı {40*(1-red):.0f}; dayanıklılık kaybı {40*red:.0f}; azalma yüzde {red*100:.0f}.
{kind} Sv.{level}: kırılmış parça|Dayanıklılığı 0 yap ~ Aynı bölgeye 40 ham hasar uygula|Kırık parça hasarı azaltmaz; can kaybı 40 olur.
{kind} Sv.{level}: daha sağlam eş|Kuşanılan dayanıklılığı 10 yap ~ Aynı seviye yeni parça al|Yeni {dur} dayanıklıklı parça kuşanılır; eski 10 dayanıklıklı parça yere düşer.
''')
group('TRN','İntikal', 'Docs/CONTRACTS.md','Kuzgun Vadisi; 4 tim × 10; sabit seed; görüntü/süre kaydı. Araç ve intikal fixture yoksa Engelli.', '''
T-70 tercihinin uygulanması|Kurulumda Helikopter seç ~ Harekât başlat|Oyuncu timi T-70 ile başlar; Kirpi tercihi yanlış uygulanmaz.
Kirpi tercihinin uygulanması|Kurulumda Zırhlı Araç seç ~ Harekât başlat|Oyuncu timi Kirpi koltuklarında başlar; yere erken düşmez.
On yolcu koltuğu|Her iki intikal türünde oyuncu ve 9 botu gözle|Tüm tim için benzersiz koltuk/iniş noktası vardır; üst üste karakter doğmaz.
Koltukta serbest bakış|İntikal sürerken fareyle sağa sola bak ~ WASD dene|Bakış çalışır; oyuncu koltuğu yürüyerek terk edemez.
Erken iniş engeli|Araç hedefe varmadan F bas|Uygulanmış intikal kuralı erken inişi engeller; oyuncu boşlukta/kayalık içinde bırakılmaz.
Elle inme|Araç Arrived durumuna gelince F bas|Yerde güvenli iniş noktasına geçilir; hareket açılır ve DropState Landed olur.
Otomatik inme|Varıştan sonra hiçbir tuşa basma ~ 3 saniye izle|Otomatik tahliye gerçekleşir; yolcular araçta süresiz kalmaz.
Sektör ayrılığı|Sabit seed ile tüm timlerin iniş noktalarını haritada işaretle|Timler harita kenarı sektörlerine dağılır; koordinatlar mutlak 400 m altında kalır.
İntikal zaman aşımı|Bir test aracının varışını geciktir ~ 90 saniye ilerlet|Maç Insertion durumunda sonsuz kalmaz; süre aşımı akışı gözlenir ve kayıt edilir.
İntikalden maç fazına|Tüm araçlar varıp tahliye olana dek izle|Maç InMatch fazına geçer; sayaç/zone başlangıcı çift tetiklenmez.
Araçtan güvenli çıkış|Sürülebilir Kirpiyle duvar yakınına git ~ F ile çık|Karakter katı duvar içine veya harita altına doğmaz.
Dolu sürücü koltuğu|İlk karakter Kirpiye girsin ~ İkinci karakterle girmeyi dene|Aynı sürücü koltuğuna ikinci sahip atanmaz.
Araç frenleme|Düz parkurda hızlan ~ Fren girdisini uygula|Hız azalır; çıkış sonrası eski gaz girdisi istemsiz sürmeyi sürdürmez.
''')
group('CMD','Komuta ve topçu',S+'Application/Services/SquadOrderService.cs','4 tim offline kontrollü fixture; oyuncu komutan, canlı telsizci; komuta kayıtları ve topçu olayları görülebilir.', '''
Takip emri|F1 bas ~ Komutanı botlardan 15 m uzaklaştır|Tim üyeleri Follow emriyle komutana yaklaşır; başka timler emir almaz.
Mevzi emri|Nişangâhı güvenli noktaya getir ~ F2 bas ~ Komutanı uzaklaştır|Mevzi emri kayıt edilir; düşman yokken tim mevzide kalır.
Taarruz hedefi|50 m uzakta zemine nişan al ~ F3 bas|Attack emri hedef koordinatını korur; botlar hedefe yönelir.
Toplan emri|Botları dağıt ~ F4 bas|Regroup emriyle hayattaki takım üyeleri komutana yaklaşır.
Komutan dışı emir|Komutansız normal piyade fixture ile F1-F4 bas|Yetkisiz oyuncu timin emrini değiştiremez; komuta yetkisi UI ile tutarlıdır.
Komuta devri|Komutanı öldür ~ Sağ kalan rütbe sırasını kaydet|En yüksek rütbeli sağ kalana komuta geçer; bildirimde doğru isim gösterilir.
Eş rütbede devir|Aynı rütbeli iki aday kaydet ~ Komutanı öldür|Kayıt sırası eşitlik bozucu olarak kullanılır; rastgele aday seçilmez.
Son komutan ölümü|Tek kalan tim üyesini öldür|Ölü oyuncuya yeni komuta verilmez; tim elenir.
Topçu ilk çağrı|Bekleme süresi 0 iken 100 m hedefe V bas|Çağrı bir kez kabul edilir; 8 mermi planlanır ve tim bekleme süresi başlar.
Topçu bekleme süresi|İlk çağrıdan hemen sonra V bas ~ Sayaç bitince tekrar dene|Süre içinde ikinci çağrı reddedilir; süre sonunda tekrar kabul edilir.
Topçu tim izolasyonu|Mavi çağrıdan sonra Kırmızı komutanı çağrı yapsın|Bir timin bekleme süresi diğer timi kilitlemez.
Topçu zamanlama|V çağrısından ilk patlamaya süre tut ~ Tüm patlamaları say|İlk patlama yaklaşık 6 sn sonra; toplam 8; ardışık aralıklar 0,35–0,75 sn.
Topçu saçılma alanı|Tek çağrının tüm patlama koordinatlarını kaydet|Her patlama merkezden yatay en çok 18 m uzaklıktadır.
Harita işaretinden topçu|M ile haritada hedef koy ~ Kapat ~ V ile çağır|Kullanılan hedef işaret ile örtüşür; kamera bakışı eski hedefi sessizce ezmez.
Yeni maçta topçu temizliği|Topçu çağırıp maçtan çık ~ Yeni maç başlat|Eski patlamalar/yasak süre yeni maçta sürmez; sayaç ve tehlike işareti temizdir.
''')
group('ZON','Bölge ve maç',S+'Application/Services/ZoneService.cs','Sabit seed 4×10 Kuzgun Vadisi; zone fazlarını hızlandırabilen izole test düzeneği.', '''
Başlangıçta zone hasarı|Zone başlamadan sınır dışına çık ~ 3 saniye bekle|Start öncesi zone hasarı uygulanmaz.
Güvenli alan hasarsızlığı|Zone başladıktan sonra merkezde 5 saniye bekle|Zone kaynaklı can kaybı olmaz.
Sınır dışı periyot|Aktif fazda dışarıda 3 tam hasar periyodu bekle|Saniyelik hasar mevcut fazın DPS değeriyle uyumludur; kare hızına göre çarpılmaz.
İçeri dönüş|Hasar alırken güvenli alana gir ~ İki periyot bekle|Sonraki dış alan hasarı durur; dış alan uyarısı kapanır.
Yeni çember kapsaması|Tüm fazların merkez/radius değerlerini kaydet|Yeni merkez uzaklığı + yeni radius önceki radius değerini aşmaz.
Doğrusal daralma|Daralma başlangıç/orta/bitiş radiusunu kaydet|Orta zamanda radius başlangıç ve bitiş ortalamasına yakın; sıçrama olmaz.
Harita sınırı|20 farklı seed ile son merkezleri kaydet|Merkez koordinatları harita halfsize×0,8 sınırı içinde kalır.
İntikal yolcusu muafiyeti|Zone dışında hâlâ intikal koltuğunda olan hedefi izle|Henüz Landed olmayan yolcuya yaya zone hasarı uygulanmaz.
Son tim galibiyeti|Diğer timlerin son üyelerini sırayla ele|Bir tim kaldığında yalnız bir MatchEnded olayı ve doğru kazanan görünür.
Tim değil birey sayımı|Mavi timde 1, Kırmızı timde 2 oyuncu bırak|2 canlı tim varken maç bitmez; oyuncu sayısıyla tim sayısı karıştırılmaz.
Çift ölüm bildirimi|Aynı ölü oyuncu için ikinci test ölüm olayı gönder|Canlı tim/oyuncu sayısı ikinci kez düşmez; yerleşim bozulmaz.
Yeni maç zone sıfırlama|Son fazda menüye dön ~ Yeni maç başlat|Önceki radius/süre/hasar yeni maça taşınmaz.
''')
group('AI','Yapay zekâ',S+'Application/Services/BotDecisionService.cs','Sabit seed; normal zorluk; Mavi/Kırmızı bot takımları; görüş ve emir durumları gözlemci kaydında.', '''
Dost hedefi reddi|Silahlı botun önüne yalnız dost yerleştir ~ 10 saniye izle|Bot dostu düşman seçmez, ateş açmaz.
Görünen düşman|Silahlı botun açık görüşüne düşman koy|Bot zorluk tepki süresi sonrası düşmana döner/çatışır; hedef kimliği doğrudur.
Duvar arkasını görmeme|Düşmanı kapalı duvar arkasına al ~ Son bilinen hedefi izle|Yeni görüş bilgisi duvardan üretilmez; geçmiş konum bilgisi yeni konumla karıştırılmaz.
Sis görüş engeli|Bot ve düşman arasına sis at ~ Süre boyunca izle|SmokeVolume görüş hattını engeller; kesintisiz tam görüş nişanı sürmez.
Zone emre üstün|Dışarıdaki bota Hold ver ~ Güvenli alanı gözle|Zone kaçışı mevzi emrinden önce gelir; bot ölümüne yerinde beklemez.
Mevzide savunma|Hold emrindeki botun görüşüne düşman sok|Mevzi davranışı düşman tehdidini yok saymaz; Engage olur.
Silahsız bot|Botun silahlarını boşalt ~ Önüne düşman koy|Silahsız bot geçersiz silahtan ateş üretmez; uygun karar/yağma davranışı gözlenir.
Mühimmat yağmalama|Botu kullanılabilir mermisiz bırak ~ Uygun kalibre mühimmat koy|Bot uygun mühimmata erişebiliyorsa alır; alakasız kalibreyi ateş için kullanmaz.
Lider değişimi|Takipteki lideri öldür ~ Yeni komutanı uzaklaştır|Takip hedefi ölü liderde takılı kalmaz; yeni liderle güncellenir.
NavMesh dar geçit|Timle 1,3 m kapıdan geç ~ 30 saniye izle|Botlar birbirini kalıcı kilitlemez; zeminden geçmeden çıkış bulur.
Zorluk tepki karşılaştırması|Aynı seed sahnesini Easy/Normal/Hard ile 10 kez çalıştır|Hedef tepki profilleri 0,9/0,55/0,3 sn ile yönsel uyumlu; dağılım kaydedilir, tek örnekten üstünlük hükmü verilmez.
Bot ölüm temizliği|Bir botu öldür ~ Olaylar ve hareketini izle|Ölü bot yeni ateş/yağma/komuta kararı üretmez; kayıt listesinden temizlenir.
''')
group('HUD','HUD ve harita','Docs/MODUL_SPESIFIKASYONLARI.md','Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.', '''
Can göstergesi|30 hasar al ~ 10 iyileş ~ Göstergeyi kontrol et|Can sırasıyla 70 ve 80; bar ve sayı aynı kaynağı gösterir.
Zırh dayanıklılığı|Yeleğe hasar al ~ Kaskı değiştir|Seviye/dayanıklılık doğru parçaya yansır; eski değer kalmaz.
Şarjör ve yedek|Tek ateş et ~ R ile doldur|Şarjör azalır ve yedekten tamamlanır; göstergeler birbirine karışmaz.
Ateş modu etiketi|B ile mod değiştir|Single/Burst/Auto Türkçe TEK/SERİ/OTO karşılığıyla eşleşir.
Pusula yönleri|Kuzey/doğu/güney/batıya dön|K/D/G/B etiketleri ve derece eşleşir; minimap kuzeyi sabittir.
İsabet sahibi|Yerel oyuncu ve bot ayrı ayrı vurulsun/vursun|Yerel atış isabetinde gösterge çıkar; başkasının atışı yerel isabet göstergesi üretmez.
Hasar yönü|Dört yönden tek tek hasar uygula|Gösterge gerçek kaynak yönünü işaret eder; kamera döndüğünde doğru dönüşür.
Öldürme akışı|Dost ve rakip öldürmelerini tetikle|İsim/silah/kafa isabeti doğru; dost ayrımı yalnız renk dışında metin/ikonla anlaşılır.
On kişilik tim paneli|10 üyeli timde farklı canlar ve bir ölü oluştur|10 ayrı satır; rütbe/rol/can doğru; ölü üye açıkça işaretli.
Dost dünya işareti|Dostu 100 m sonra 301 m uzağa taşı|İşaret yakın dostta görünür, 300 m üstü görünmez; rakipte dost işareti yoktur.
Komuta bildirimi|Komutan ölümünü tetikle|Bildirim doğru yeni komutan adı/rütbesiyle bir kez çıkar.
Kullanım ilerleme|H ile iyileş ~ Ateş ederek iptal et|İlerleme gerçek süreyi izler; iptalde gösterge kaybolur.
Dürbün örtüsü|JNG-90 ile nişan al ~ Bırak ~ SAR 9 ile nişan al|Dürbün retikülü yalnız dürbünlü durumda; eski siyah örtü tabancada kalmaz.
Zone uyarısı|Sınır dışına çık ~ Güvenli alana dön|Dış alan renk/uyarısı aktif duruma bağlı; güvenli alanda temizlenir.
Mini harita konumu|Harita köşelerine git ~ Oyuncu ve müttefik işaretlerini izle|Dünya koordinatları doğru UV yönüne dönüşür; yatay/dikey aynalanma olmaz.
Tam harita imleci|M aç ~ Bir nokta işaretle ~ M kapat|Açıkken imleç serbest; kapatınca oyun bakışı geri; işaret dünya noktasını tutar.
Harita pencere çakışması|M aç ~ Tab aç ~ Esc ile geri dön|Pencere önceliği tutarlı; gizli pencere tıklama almaz; imleç kilidi kaybolmaz.
Konum adları|Tüm isimli lokasyonları haritada kontrol et|WorldMetadata isimleri gösterilir; Türkçe İ/ı/Ş/ğ bozulmaz.
Mini harita veri yokluğu|MinimapTexture olmayan test fixture aç|Gri yedek gösterilir; hata yağmuru veya NullReference oluşmaz.
HUD yeniden giriş|Maçtan menüye dönüp üç kez başlat ~ Bir isabet olayı üret|HUD tek olay için tek bildirim verir; eski abonelikler birikmez.
''')
group('MENU','Menüler ayarlar kariyer',S+'Application/Services/SettingsService.cs','Temiz yerel test profili; MainMenu sahnesi; mevcut profilin yedeği alınmış; gerçek hesap kullanılmaz.', '''
Kurulum tim alt sınırı|Tim kaydırıcısını en düşüğe getir ~ Başlat|2 tim × 10 = 20 savaşan seçimi gösterilir.
Kurulum tim üst sınırı|Tim kaydırıcısını en yükseğe getir ~ Başlat|6 tim × 10 = 60 savaşan seçimi gösterilir.
Zorluk eşlemesi|Er/Uzman/Komando seçeneklerini sırayla kaydet|Seçimler Easy/Normal/Hard karşılığına dönüşür; yeniden açınca korunur.
FOV alt üst sınırı|FOV kaydırıcısını 60 sonra 100 yap|Görüş açısı 60–100 aralığında; ADS bırakıldığında seçili temele döner.
Ses ana seviye|Ana sesi 0 yap ~ Ateş et/menüye dön ~ Yeniden aç|Ana seviye tüm ilgili sesleri susturur; geri açınca ses döner.
Ortam sesi ayrımı|Ortam sesini 0 yap ~ Ateş et|Ortam döngüsü kısılır; ana ses açıksa silah sesinin ayrı kontrolü korunur.
ADS hassasiyeti|ADS çarpanını değiştir ~ Aynı fare hareketini ADS/kalçada uygula|ADS hassasiyeti çarpanı nişan durumuna uygulanır; genel ayar ezilmez.
Kalite uygulama|Düşük ve Ultra seç ~ Sahneyi gözle|Kalite seçimi uygulanır; sahne pembe materyale dönüşmez veya çökmez.
Tam ekran geçişi|Tam ekranı aç/kapat ~ Ana menüye dön|Pencere/odak düzeni korunur; UI ekran dışında kalmaz.
FPS ayarı|FPS göster aç ~ Kapat|Sayaç doğru ayara göre görünür/gizlenir; sürekli kaplamaya dönüşmez.
Ayar kalıcılığı|FOV/ses/oyuncu adı değiştir ~ Oyunu tamamen kapat/aç|Kaydedilen değerler geri gelir; geçersiz değerler servis sınırlarına alınır.
Offline duraklatma|Maçta Esc aç ~ 5 saniye bekle ~ Devam et|Offline simülasyon durur; dönünce Time.timeScale doğru normale gelir.
Menüye çıkış zaman ölçeği|Oyun duraklatılmışken Ana Menü seç ~ Yeni maç aç|Yeni maç donmuş timeScale=0 durumuyla başlamaz.
Kariyer XP formülü|4 timde 2. sıra, 3 kill, 1 headshot ve galibiyet yok sonuç kaydet|XP 3×100+1×25+(4−2)×150=625; kariyer bir kez artar.
Galibiyet XP|4 timde 1. sıra, 0 kill ve galibiyet sonucu kaydet|XP (4−1)×150+1000=1450; rütbe eşiği RankCatalog ile eşleşir.
''')
group('RNG','Atış Poligonu','Docs/MODUL_SPESIFIKASYONLARI.md','Ana menüden Atış Poligonu; gözlemci kamera ve ekran kaydı.', '''
Doğru sahne|Atış Poligonu düğmesine bas ~ Yüklemeyi bekle|TrainingRange açılır; canlı battle royale maçı başlatılmaz.
Silah rafı kapsamı|Rafları dolaş ~ Silah adlarını katalogla karşılaştır|10 katalog silahı erişilebilir; aynı silahın tekrarı eksik silahı gizlemez.
Sonsuz yedek|PMT-76 şarjörünü boşalt ~ Beş kez doldur|Yedek tükenmez; şarjör/doldurma mekaniği çalışmayı sürdürür.
Mesafe levhaları|25/50/100/200/300 m levhalarına bak ~ Gerçek koordinat uzaklığını ölç|Etiketler gerçek hedef uzaklığına uygundur; 300 m hedef alan dışına taşmaz.
Sabit hedef hasarı|Sabit hedefi öldür ~ Yeniden doğuş süresini izle|Hasar bildirimi doğru; hedef yenilenince tam canla çalışır.
Hareketli hedef|Hareketli hedefi bir tam tur gözle ~ Vur|Tanımlı iki nokta arasında hareket sürer; hasar hitboxla eşleşir.
Poligonda zone yok|10 dakika alanda kal|Zone daralması ve takım elenmesi poligonu sonlandırmaz.
Engel parkuru|Duvar ve kill house içini dolaş|Katı nesneler çarpışır; kapılar ve rampalar geçilebilir.
Poligondan çıkış|Esc ile ana menüye dön ~ Harekât başlat|Sonsuz mühimmat ve test hedefleri normal maça taşınmaz.
''')
group('PERF','Performans', 'Docs/CONTRACTS.md','Aynı Windows test cihazı, sürücü, güç profili ve build; 60 sn ısınma, 5 dk ölçüm; Profiler/ETW kaydı. Eşikler geçici QA hedefidir.', '''
20 savaşan temel ölçüm|2 timle 1080p orta kalitede 5 dakika oyna ~ CPU/GPU frametime kaydet|P50/P95/P99 ve 1 yüzde düşük FPS kaydedilir; cihaz hedefi TestPlan ile karşılaştırılır, ölçüm yoksa Geçti yazılmaz.
60 savaşan stres|6 timle yoğun çatışmada 5 dakika izle ~ Tahsisleri kaydet|Hot path sürekli kare başı GC tahsisi yapmaz; önerilen P95≤16,7 ms hedef sapması raporlanır.
Topçu VFX yükü|Altı tim topçu çağrısı başlat ~ Patlama zirvesini ölç|VFX havuzu kontrolsüz büyümez; kare sıçramaları ve GPU süresi kanıtla kaydedilir.
Sis yükü|Aynı görüşte 10 sis bulutu oluştur ~ GPU zamanını ölç|Saydamlık maliyeti raporlanır; bulutlar 25 sn sonrası temizlenir, kalıcı maliyet bırakmaz.
Mermi havuzu|PMT-76 ile kesintisiz yoğun ateş fixture çalıştır ~ Aktif mermi sayısını izle|Mermiler yaşam süresi/menzil sonrası temizlenir; sahne nesne sayısı atışla sınırsız büyümez.
Sahne tekrar bellek|Menü→maç→menü döngüsünü 10 kez yap ~ Her dönüşte belleği kaydet|Kalıcı artış varsa sızıntı adayı açılır; sabit bütçeyle kıyaslanır ve GC sonrası eğilim raporlanır.
Ses başlangıç maliyeti|Soğuk başlangıçta ses sentezi süresini ölç|Sözleşme hedefi toplam <300 ms; donanım ve süre ölçülerek raporlanır.
AudioSource sınırı|Aynı anda çok sayıda atış/patlama oluştur ~ Ses kaynaklarını say|3D ses havuzu 32 sınırını aşmaz; bittiğinde kaynaklar yeniden kullanılabilir.
Mermi izi temizliği|5 dakika duvara ateş et ~ Decal sayısını incele|Mermi izi havuzu üst sınır 150; eski izler geri dönüştürülür.
Yükleme süresi|Kuzgun Vadisi soğuk/sıcak açılışı üçer kez ölç|Yükleme süresi, disk tipi ve NavMesh/runtime üretim yolu ayrı kaydedilir; donmuş görüntü için yükleme durumu görünür.
Arka plan yükü|Windows güncelleme/arka plan iş yükü benzetimi altında maça gir ~ Süreyi ölç|Çökme/veri kaybı olmaz; FPS düşüşü temel ölçümden ayrı işaretlenir.
Uzun oturum|60 savaşanla 60 dakika tekrar maç döngüsü yap ~ Bellek/handle sayısını izle|Çökme yok; yükselen bellek/handle eğrisi varsa sızıntı incelemesi açılır.
''', 'QA kabul hedefi')
group('NET','Online ve dayanıklılık','Backend/ClientSdk/HarekatClient.cs','İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.', '''
Kuyruk temel akışı|Giriş yap ~ Tek hesapla kuyruğa gir ~ Bileti izle|Bilet ve durum gösterilir; atama olursa tek maç/sunucu bilgisi gelir.
Kuyruk iptali|Kuyruğa gir ~ Atama öncesi iptal et ~ Tekrar durum sorgula|İptal UI ve sunucu durumunda tutarlı; istemci bekliyor ekranında kilitlenmez.
Tim kuyruğu|10 üyeyi aynı timde hazır yap ~ Komutan kuyruğa girsin|Tim bölünmeden aynı eşleştirme akışını izler; destek yoksa eksik özellik raporlanır.
On birinci üye|10 kişilik time ek hesapla davet kodundan katıl|Üye sayısı 10'u aşmaz; anlaşılır ret gösterilir.
Yanlış davet kodu|Var olmayan davet koduyla katıl|Başka tim açığa çıkmadan hata gösterilir; mevcut tim üyeliği bozulmaz.
Bağlantı sırasında kopma|Sunucuya bağlanırken ağı 15 sn kes ~ Geri aç|Sonuç açık başarı/hata durumuna ulaşır; sonsuz yükleme/çift karakter oluşmaz.
Maç içinde kopma|Devam eden maçta ağı 15 sn kes ~ Geri aç|Belgelenmiş reconnect politikası uygulanır; politika yoksa karar açığı kaydı oluşturulur, başarılı varsayılmaz.
100 ms RTT|Şekillendiricide 100 ms RTT, 0 kayıp kur ~ 5 dk çatış|P95 komut/hasar gecikmesi ölçülür; çift ateş/çift eşya alımı olmaz.
200 ms RTT|Şekillendiricide 200 ms RTT kur ~ Hareket ve atış kaydet|Düzeltme sıçramaları ve eylem gecikmesi raporlanır; sunucu otoritesi korunur.
Yüzde 1 kayıp|100 ms RTT + yüzde 1 kayıpla 5 dk oyna|Güvenilir eylemler kaybolup çoğalmaz; kopma oranı kaydedilir.
Yüzde 5 kayıp|100 ms RTT + yüzde 5 kayıpla 5 dk oyna|Kötü bağlantı anlaşılır; sunucu çökmez, envanter çiftlenmez.
Jitter|100 ms RTT üzerine ±50 ms jitter ekle ~ 5 dk oyna|P95/P99 gecikme ve düzeltmeler ölçülür; zaman sırası ters hasar doğurmaz.
Sunucu yeniden başlama|Test maçındayken kontrollü sunucuyu yeniden başlat|İstemci açık hata/menüye dönüş alır; sonuçlar sahte galibiyet diye kaydedilmez.
Token süresi dolması|Kısa ömürlü test token ile bekle ~ Profil isteği yap|İstemci yenileme/yeniden giriş politikası uygular; token UI/logda açığa çıkmaz.
Aynı eşyaya eşzamanlı alma|İki test oyuncusu aynı yağmaya aynı anda F bassın|Sunucu tek geçerli sahip/dağıtım üretir; toplam miktar korunur.
Otoritesiz hasar|İstemci tarafı test fixture ile yerel can değerini değiştir ~ Sunucu snapshot bekle|Sunucu otoritesi yerel değişikliği maç sonucuna kabul etmez.
''','QA kabul hedefi')
# Gerçek rotalar: her vakanın yöntemi ve somut iş verisi ayrı.
api='Backend/Harekat.Api/Program.cs'
group('API','Backend uçları',api,'İzole IIS/MSSQL test ortamı; API kökü dağıtım kaydında; Swagger/ClientSdk şemasına uygun JSON; A/B oyuncuları, M moderatörü ve S test sunucusu. Üretim verisi kullanılmaz.', '''
GET /health|Kimliksiz GET /health gönder ~ Yanıtı oku|200; status=ok, service=Harekat.Api ve UTC alanı bulunur; bu yalnız API canlılığını gösterir.
POST /auth/register|Benzersiz test kullanıcı/e-posta ve geçerli parola ile kayıt gönder|Başarılı kayıt tek oyuncu oluşturur; dönen auth verisinde parola yer almaz.
POST /auth/login|Kayıtlı A hesabının doğru parolasını gönder ~ Yanlış parola ile tekrarla|Doğru bilgiyle token; yanlış bilgiyle başarısız yanıt; stack trace/parola açığa çıkmaz.
POST /auth/refresh|Geçerli refresh token ile yenile ~ Geçersiz token ile tekrarla|Geçerli akış yeni auth verisi üretir; geçersiz token kimlik doğrulamaz.
POST /auth/verify-email|A tokenı ve test doğrulama koduyla gönder ~ Yanlış kodla tekrarla|Geçerli işlem verified=true; yanlış kod doğrulanmış hesap üretmez.
POST /auth/logout|A tokenıyla çıkış yap ~ Eski refresh ile yenilemeyi dene|loggedOut=true; çıkış yapılan oturumun yenileme erişimi politikaya göre iptal edilir; sapma hata kaydıdır.
GET /players/me|A tokenıyla isteği gönder ~ Token olmadan tekrarla|A'nın profili döner; kimliksiz istek 401 alır.
GET /players/{username}|A kullanıcı adıyla kimliksiz iste ~ Olmayan adı sorgula|A'nın herkese açık profili döner; özel auth alanları yoktur; olmayan ad açık hata üretir.
POST /squads|A tokenıyla geçerli isimli tim kur|A komutan/üye olarak tek timde görünür; davet kodu kullanılabilir.
POST /squads/join|B hesabıyla A'nın davet kodunu gönder|B aynı timde görünür; mevcut üyeler kaybolmaz.
GET /squads/me|B tokenıyla sorgula ~ Timsiz hesapla sorgula|B'nin timi döner; timsiz hesap 404 döner.
GET /squads/{id:guid}|Bilinen tim GUID ile yetkili sorgu yap ~ Geçersiz GUID gönder|Geçerli timin üyeleri döner; geçersiz GUID rota eşleşmez ve 500 üretmez.
POST /squads/ready|A tokenıyla ready=true sorgu parametresi gönder ~ ready=false tekrarla|Hazır durumu değişir ve lobi Ready olayı yeni durumla tutarlıdır.
POST /squads/leave|B ile ayrıl ~ Tim listesine tekrar bak|left=true; B listeden çıkar; diğer üyeler korunur.
POST /matchmaking/queue|A tokenıyla geçerli QueueRequest gönder|Bir bilet kimliği ve gerçek durum döner; UI olmayan tahmini beklemeyi gerçek veri diye sunmaz.
DELETE /matchmaking/queue|Kuyruktaki A ile iptal isteği gönder|cancelled=true; A bekleyen eşleştirme girişinden çıkar.
GET /matchmaking/tickets/{id:guid}|A'nın biletini sorgula ~ Rastgele geçerli GUID sorgula|Bilet durumu döner; bilinmeyen GUID için 404.
GET /matches/{id:guid}|Atanmış maç kimliğini sorgula ~ Bilinmeyen GUID gönder|Maç kaydı döner; bilinmeyen GUID 404; token gereklidir.
POST /servers/register|RegisterServerRequest ile izole test sunucusunu kaydet|Sunucu kaydı alınır; üretilen anahtar yalnız yetkili laboratuvar kaydında tutulur; rota bugün AllowAnonymous olduğundan güvenlik kapısı ayrı incelenir.
POST /servers/heartbeat|Kayıtlı test sunucusuyla heartbeat gönder|Son canlılık ve sunucu durum alanları güncellenir; yanlış kimlik/anahtar uygulama servisi politikasına göre reddedilir.
GET /servers|Kimliksiz liste iste ~ Yanıt alanlarını kontrol et|Sunucu listesi gelir; server key gibi sırlar yanıtta bulunmaz.
POST /matches/{id:guid}/result yetkisiz|X-Server-Id ve X-Server-Key olmadan sonuç gönder|401; maç ve oyuncu XP kaydı değişmez.|P0
POST /matches/{id:guid}/result geçerli|S kimlik/anahtarıyla kendisine atanmış maç sonucunu gönder ~ Aynı sonucu tekrar gönder|Yetkili sonuç tek kez kaydedilir; tekrarda XP/kill çiftlenmesi olmaz; idempotency sapması hata olarak açılır.|P0
GET /leaderboards|metric=experience ve take=10 ile sorgula|En çok 10 kayıt ilgili metriğe göre sıralıdır; diğer gizli oyuncu alanları sızmaz.
GET /leaderboards/season|Bilinen season ve take=10 ile sorgula|Sezon puanı doğru sezonla sınırlı; kariyer toplamıyla karışmaz.
POST /friends/request|A ile B oyuncu kimliğine istek gönder|B için tek bekleyen arkadaşlık isteği oluşur.
POST /friends/{id:guid}/accept|B ile alınan istek kimliğini kabul et|A/B arkadaşlığı tutarlı görünür; ilgisiz kullanıcı kabul edemez.
GET /friends|A tokenıyla arkadaş listesini sorgula|Kabul edilen B görünür; başka hesabın özel istekleri görünmez.
GET /seasons/active|Aktif sezon fixture ile iste ~ Aktif sezonsuz fixture ile iste|Aktif sezon döner; aktif sezon yoksa 404.
GET /seasons/{number:int}/archive|Bilinen sezon numarasıyla arşiv iste|İstenen sezon kayıtları gelir; geçerli yeni sezon durumu arşivi değiştirmez.
GET /achievements/me|A tokenıyla başarımları sorgula|A'nın koşullarına göre kilitli/açılmış liste; kimliksiz 401.
GET /cosmetics/me|A tokenıyla kozmetikleri sorgula|A'nın sahiplik/kullanım bilgisi tutarlı; kimliksiz 401.
POST /cosmetics/equip|A'nın sahip olduğu kozmetiği kuşan ~ Sahip olmadığı kimliği dene|Sahip olunan kozmetik kuşanılır; olmayan sahiplik güç/ödül kazandırmaz.
POST /moderation/report|A ile B hakkında test raporu gönder|reported=true; moderasyon kuyruğunda bir kayıt; rapor tek başına otomatik suç hükmü değildir.
POST /moderation/ban|Normal A tokenıyla test B'yi banlamayı dene ~ M ile kontrollü tekrarla|A için 403; M için işlem yetki politikasıyla uygulanır; B test hesabıdır.|P0
POST /moderation/mute|Normal A tokenıyla dene ~ M ile test B üzerinde süreli mute uygula|A için 403; M işlemi uygular; süre/ana gerekçe denetlenebilir.
GET /moderation/reports|A ve M tokenlarıyla sırayla liste iste|A için 403; yalnız M moderasyon kayıtlarını görür.|P0
''')
group('ACC','Erişilebilirlik','Docs/CODEX_FAZ2.md','Windows istemci + portal/anket; klavye, ekran okuyucu, renk simülasyonu; test edilen ürün/sürüm açık kaydedilir.', '''
Klavye menü sırası|Fareyi bırak ~ Tab/Shift+Tab ile ana menüyü dolaş ~ Enter kullan|Erişilebilir menüde tüm eylemler ulaşılabilir; odak görünür ve mantıklıdır.
Odak tuzağı|Ayarlar aç ~ Son alandan Tab bas ~ Esc ile kapat|Odak aktif pencerede yönetilir; kapanınca açan kontrole döner; sonsuz tuzak olmaz.
Yalnız renk ayrımı|Dost/düşman, hasar ve hazır durumlarını gri tonla incele|Anlam metin/ikon/şekille de aktarılır; kritik ayrım yalnız kırmızı/yeşile bağlı değildir.
Büyütmede taşma|Web/anketi yüzde 200 yakınlaştır ~ 320 CSS px genişlikte kullan|Alanlar okunur; içerik kaybı ve zorunlu yatay kaydırma yoktur.
Anket etiketleri|Anketi ekran okuyucuyla form modunda gez|Her alanın adı/ölçeği ve hata mesajı duyurulur; zorunluluk anlaşılır.
Hareket hassasiyeti|İşletim sisteminde azaltılmış hareket aç ~ UI animasyonlarını izle|Desteklenen web yüzeylerinde gereksiz animasyon azaltılır; oyun kamera seçeneği eksikse öneri açılır.
Anket hata kurtarma|Zorunlu alanları boş gönder ~ İlk hatayı düzelt|Gönderim engellenir; ilk hata odağa gelir; doldurulmuş diğer alanlar kaybolmaz.
Ses olmadan kritik olay|Oyunu sessize al ~ Zone ve komuta devrini tetikle|Kritik olaylar görsel/metinsel olarak da fark edilir; yalnız sesle bilgi verilmez.
''','QA kabul hedefi')
group('WIN','Windows matrisi','Docs/CODEX_FAZ2.md','TestPlan cihaz kaydında Windows sürümü/build, GPU/sürücü, DPI, ekran ve güç profili doldurulur; kombinasyonlar ayrı koşul kimliği alır.', '''
DPI yüzde 100|1080p yüzde 100 DPI ile menü/HUD/anketi aç|Metin kırpılmaz; tıklama alanları görünür konumla eşleşir.
DPI yüzde 150|1440p yüzde 150 DPI ayarla ~ Uygulamayı yeniden aç|Menü ve HUD ekran sınırları içinde; imleç kayması olmaz.
DPI yüzde 200|4K yüzde 200 DPI ile ayarlar ve envanteri aç|Kritik butonlar erişilir; yazılar oransız küçülmez/kırpılmaz.
Karma DPI ekran|Yüzde 100 ve yüzde 150 iki monitör arasında pencere taşı|Boyut/odak düzelir; fare koordinatları farklı ölçekte sapmaz.
Monitör çıkarma|İkinci ekranda pencereli oyunu aç ~ Ekranı çıkar|Pencere erişilebilir birincil ekrana döner; siyah ekran kilidi olmaz.
NVIDIA sürücü ölçümü|QA aday cihazında kaydedilmiş NVIDIA sürümüyle 60 savaşan ölç|GPU/sürücü tam sürümü ve frametime eklenir; destek iddiası ancak gerçek koşumdan sonra yapılır.
AMD sürücü ölçümü|QA aday cihazında kaydedilmiş AMD sürümüyle aynı seed ölç|Aynı kalite/çözünürlükte artefakt, çökme ve frametime kaydı alınır.
Intel sürücü ölçümü|QA aday Intel GPU cihazında düşük kalite ölç|Başlatma/render hatası ve performans kaydedilir; destek garantisi varsayılmaz.
Uyku dönüşü|Menüdeyken Windows uykuya al ~ Uyandır ~ Test maçına gir|Ses/giriş/görüntü düzelir; çevrim içi oturum gerekiyorsa açık yeniden bağlantı akışı sunulur.
Standart kullanıcı kurulumu|Yönetici olmayan test kullanıcısıyla dağıtımı aç ~ Ayar kaydet|Gerekli kurulum izinleri açık belirtilir; günlük oynanışta gereksiz yükseltilmiş yetki beklenmez.
''','QA kabul hedefi')
# BalanceCalc deterministik karşılaştırmaları: model/oyun zaman farkını ayrı tut.
with (ROOT/'Tools/BalanceCalc/out/ttk.csv').open() as f: ttk=list(csv.DictReader(f))
for w in catalog['weapons']:
 c=next(t for t in ttk if t['weapon_id']==w['weapon_id'] and t['distance_m']=='10.0' and t['zone']=='body' and t['armor_level']=='0' and t['helmet_level']=='0')
 group('BAL','Denge', 'Tools/BalanceCalc/out/ttk.csv',f"Kontrollü hasar fixture; {w['display_name']}; hedef 100 can, zırh/kask yok, 10 m; tüm isabetler gövdeye, saçmalı silahta tüm saçmalar; kritik yok.",f"""
{w['display_name']}: BalanceCalc gövde TTK|10 kez hedef sıfırlayıp öldürme atışlarını say ~ İlk ve ölümcül isabet zamanını kaydet ~ Seyahat, girdi ve kare gecikmesini ayrı tut|Model {c['shots_to_kill']} atış ve {c['ttk_seconds']} sn ideal TTK; atış sayısı eşleşir; simülasyon zaman farkı en çok bir tick, insan/girdi gecikmesi model hatası diye yazılmaz.
""")
# Kaynak snapshot hash'leri, oyun testi geçişi yerine yalnız veri izlenebilirliği sağlar.
source_paths=sorted({r['source'] for r in rows}|{'Docs/CONTRACTS.md','Docs/MODUL_SPESIFIKASYONLARI.md','Tools/BalanceCalc/out/catalog.json'})
manifest={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in source_paths}
(ROOT/'QA/cases.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
(ROOT/'QA/source-snapshot.json').write_text(json.dumps({'date':'2026-10-05','timezone':'Europe/Istanbul','sources':manifest},ensure_ascii=False,indent=2)+'\n')
print(f'{len(rows)} senaryo; {len(set(r["module"] for r in rows))} modül')
