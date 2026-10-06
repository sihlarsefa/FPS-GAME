using System;
using Project.Core.Domain;

namespace Project.Presentation.UI
{
    /// <summary>Yükleme aşamaları (sıra = zaman sırası).</summary>
    public enum LoadPhase
    {
        SceneLoad = 0,
        WorldGen = 1,
        WorldContent = 2,
        BotSpawn = 3,
        Finalize = 4,
    }

    /// <summary>
    /// Gerçek yükleme aşamalarından tek bir 0..1 ilerleme üretir (saf mantık). Her aşamanın toplam içindeki payı sabittir;
    /// değer yalnızca artar (geri sarmaz) — aşamalar sırayla bildirilmese de çubuk geriye gitmez.
    /// </summary>
    public sealed class LoadingProgressModel
    {
        // Aşama başlangıçları ve payları: sahne %0-30, dünya %30-65, ganimet/araç %65-75, tim/bot %75-92, son %92-100.
        private static readonly float[] Start = { 0f, 0.30f, 0.65f, 0.75f, 0.92f };
        private static readonly float[] Share = { 0.30f, 0.35f, 0.10f, 0.17f, 0.08f };

        public float Value { get; private set; }
        public LoadPhase Phase { get; private set; }

        public void Reset()
        {
            Value = 0f;
            Phase = LoadPhase.SceneLoad;
        }

        /// <summary>Aşamanın alt ilerlemesini (0..1) bildirir; toplam ilerlemeyi döndürür.</summary>
        public float Report(LoadPhase phase, float fraction01)
        {
            var i = (int)phase;
            if (i < 0 || i >= Start.Length)
                return Value;

            var f = fraction01 < 0f ? 0f : fraction01 > 1f ? 1f : fraction01;
            if (float.IsNaN(fraction01))
                f = 0f;

            var overall = Start[i] + Share[i] * f;
            if (overall > Value)
            {
                Value = overall;
                Phase = phase;
            }
            else if (phase > Phase)
            {
                Phase = phase;
            }

            return Value;
        }

        /// <summary>Aşamanın ekranda görünen Türkçe adı.</summary>
        public static string Label(LoadPhase phase)
        {
            switch (phase)
            {
                case LoadPhase.SceneLoad: return "Harekât sahası yükleniyor";
                case LoadPhase.WorldGen: return "Arazi ve yapılar kuruluyor";
                case LoadPhase.WorldContent: return "Ganimet ve araçlar yerleştiriliyor";
                case LoadPhase.BotSpawn: return "Birlikler sevk ediliyor";
                default: return "Son hazırlıklar";
            }
        }
    }

    /// <summary>Türkçe taktik ipuçları (silah kullanımı, zırh, bölge, tim telsizi).</summary>
    public static class LoadingTips
    {
        private static readonly string[] Tips =
        {
            // Silah kullanımı (0-13)
            "İpucu: Uzun seri ateşte namlu yükselir; 3-5 mermilik kısa serilerle atış yaparak isabeti artır.",
            "İpucu: Nişan alırken (sağ tık) hareket yavaşlar ama isabet artar; açık alanda koşarken ateş etme.",
            "İpucu: Şarjörün bitmesini bekleme; ateş kesintisinde R ile doldurmak ölümcül boşluğu önler.",
            "İpucu: Kovan sesini dinle — şarjörü boşalan düşman yeniden doldururken en zayıf andadır.",
            "İpucu: Mermi uzun mesafede düşer; keskin nişancı tüfeğiyle uzak hedefte nişan noktasını yukarı al.",
            "İpucu: Pompalı tüfek yakın mesafede yıkıcıdır, ama 15 metreden sonra saçılma etkisini kaybeder.",
            "İpucu: Dürbünlü silahta kör nokta oluşur; yakın dövüşte yan silaha geçmek çoğu zaman daha hızlıdır.",
            "İpucu: Susturucu ateş sesini ve namlu alevini azaltır; konumun daha geç belli olur.",
            "İpucu: Silahı yeniden doldururken siper al — animasyon boyunca ateş edemezsin.",
            "İpucu: Her silahın geri tepmesi farklıdır; poligonda kendi tarzına uygun silahı bul.",
            "İpucu: Bomba atmadan önce pimi çek ve bekle; havada patlayan bomba siperdeki düşmanı çıkarır.",
            "İpucu: Kafadan isabetler kask seviyesine göre çok daha ölümcüldür; nişanı omuz hizasından yukarı tut.",
            "İpucu: Yan silahına geçmek çoğu zaman şarjör doldurmaktan hızlıdır; acil durumda tabancaya geç.",
            "İpucu: Duvarlar ve ince kapılar mermiyi tam durdurmaz; ağır kalibreler sığ siperleri deler.",
            // Zırh ve sağlık (14-21)
            "İpucu: Zırh yüksek kalibreli mermiye karşı daha az koruma sağlar; seviye 3 zırh bile delinebilir.",
            "İpucu: Kask kafa hasarını azaltır ama kalıcı değildir; yıpranınca değiştir.",
            "İpucu: Yaralıyken hareket etmeden önce H ile sarılmak kanamayı durdurur; sarılırken siper al.",
            "İpucu: Tıbbi çanta sağlığı yavaşça doldurur; takviye (J) ise kısa süreli güç verir. İkisini karıştırma.",
            "İpucu: Zırhlı yeleğin dayanıklılığı mermi başına düşer; çıplak bölgeye gelen isabet zırhı atlar.",
            "İpucu: Yerdeki yaralı arkadaşını sürüklemek yerine önce dumanla çevir, sonra kaldır.",
            "İpucu: Yük ağırlığı hızını düşürür; kullanmayacağın silah ve mühimmatı yerde bırak.",
            "İpucu: Ganimet toplarken etrafı dinle; envanter açıkken savunmasızsın.",
            // Bölge ve harita (22-31)
            "İpucu: Harekât alanı daralır — mavi bölgenin dışında kalan asker sürekli hasar alır.",
            "İpucu: Bölge kapanırken merkeze değil, çemberin yönüne bakan siperlere ilerle.",
            "İpucu: Çemberin kenarında kalan takımlar sıkışır; erken konumlan ve yüksek zemini tut.",
            "İpucu: M ile tam haritayı aç; işaretlediğin nokta tim emirlerinde hedef olur.",
            "İpucu: Yoldan gitmek hızlıdır ama açıktır; nehir yatağı ve ağaçlık hat seni gizler.",
            "İpucu: Yüksek zemin görüş ve menzil verir; ancak silüetini gökyüzüne karşı belli eder.",
            "İpucu: Su altı kısa süreli güvenli kaçış sağlar ama silahın kullanılamaz; oksijene dikkat et.",
            "İpucu: İkmal sandığı atıldığında etrafında çatışma çıkar; başkaları yetişmeden önce yaklaşma.",
            "İpucu: Kirpi zırhlı aracına F ile binebilirsin; gürültüsü çok uzaktan duyulur.",
            "İpucu: Z ile yüzüstü yatmak seni uzak mesafeden neredeyse görünmez yapar.",
            // Tim ve telsiz (32-39)
            "İpucu: F1 takip, F2 mevzi tut, F3 nişan noktasına taarruz, F4 toplan emirlerini verir.",
            "İpucu: Telsizci ya da tim komutanıysan V tuşuyla nişan noktasına topçu atışı isteyebilirsin.",
            "İpucu: Komutan şehit düşerse komuta en kıdemli askere geçer; tim harekâta devam eder.",
            "İpucu: Q ve E ile siperin arkasından yana eğilerek ateş edebilirsin; vücudunun çoğu korunur.",
            "İpucu: Telsizden gelen \"temas\" uyarısını ciddiye al; yön sana düşmanın hangi taraftan geldiğini söyler.",
            "İpucu: Dağınık tim savunmasızdır; birlik içinde 20 metreden fazla açılma, ateş altında birbirinizi örtün.",
            "İpucu: Timdeki yaralıyı kaldırmak için yanına git ve kaldırma tuşunu basılı tut; ateş altındaysan önce sis at.",
            "İpucu: Keşfi ve ateşi paylaşın: biri baskı atarken diğeri yandan dolaşırsa düşmanı sıkıştırırsın.",
        };

        public static int Count => Tips.Length;

        /// <summary>İpucu metni (indeks çevrimlidir; negatif de güvenlidir).</summary>
        public static string Get(int index)
        {
            var n = Tips.Length;
            return Tips[((index % n) + n) % n];
        }

        /// <summary>Bir önceki ipucundan farklı sonraki indeksi seçer (rastgele 0..1 değerinden).</summary>
        public static int Next(int previous, float random01)
        {
            var n = Tips.Length;
            var step = 1 + (int)Math.Floor((random01 < 0f ? 0f : random01 >= 1f ? 0.9999f : random01) * (n - 1));
            return ((previous + step) % n + n) % n;
        }
    }

    /// <summary>Harekât brifingi metni (harita + mod + tim sayısı).</summary>
    public static class LoadingBriefing
    {
        public static string ModeName(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Training: return "Atış Poligonu";
                case GameMode.Skirmish: return "Çatışma";
                case GameMode.HostageRescue: return "Rehine Kurtarma";
                default: return "Tim Battle Royale";
            }
        }

        /// <summary>Harita adı (boşsa varsayılan).</summary>
        public static string MapTitle(string mapName)
        {
            return string.IsNullOrEmpty(mapName) ? MapCatalog.KuzgunName : mapName;
        }

        /// <summary>İntikal brifingi (çok satırlı). teamCount/teamSize &lt;= 0 ise tim satırı yazılmaz.</summary>
        public static string Build(string mapName, GameMode mode, int teamCount, int teamSize)
        {
            var sb = new System.Text.StringBuilder(160);
            sb.Append("Bölge: ").Append(MapTitle(mapName)).Append('\n');
            sb.Append("Görev: ").Append(ModeName(mode)).Append('\n');
            if (mode == GameMode.BattleRoyale && teamCount > 0 && teamSize > 0)
            {
                sb.Append("Kuvvet: ").Append(teamCount).Append(" tim x ").Append(teamSize).Append(" asker (")
                  .Append(teamCount * teamSize).Append(" personel)\n");
                sb.Append("İntikal: Helikopter ve Kirpi ile hava/kara indirme\n");
                sb.Append("Hedef: Son ayakta kalan tim olmak");
            }
            else if (mode == GameMode.Training)
            {
                sb.Append("Hedef: Silah kullanımı ve hareket eğitimi");
            }
            else if (mode == GameMode.HostageRescue)
            {
                sb.Append("Hedef: Rehineleri güvenle tahliye et");
            }
            else
            {
                sb.Append("Hedef: Bölgeyi ele geçir");
            }

            return sb.ToString();
        }
    }
}
