namespace Harekat.Domain.Catalogs;

/// <summary>Kozmetik katalog — Design/Progression/cosmetics.json tohumu (46).</summary>
public static class CosmeticCatalog
{
    public sealed record Item(string Id, string Name, string Slot, int UnlockXp);

    public static readonly IReadOnlyList<Item> All =
    [
        new("camo_standard", "Standart Kamuflaj", "camo", 0),
        new("camo_forest", "Orman Kamuflajı", "camo", 5000),
        new("camo_mountain", "Dağ Kamuflajı", "camo", 7500),
        new("camo_desert", "Çöl Kamuflajı", "camo", 2000),
        new("camo_snow", "Kış Kamuflajı", "camo", 20000),
        new("camo_urban", "Şehir Kamuflajı", "camo", 10000),
        new("camo_night", "Gece Operasyonu", "camo", 35000),
        new("camo_coast", "Kıyı Kamuflajı", "camo", 12000),
        new("beret_green", "Yeşil Bere", "beret", 0),
        new("beret_maroon", "Bordo Bere", "beret", 3000),
        new("beret_black", "Siyah Bere", "beret", 8000),
        new("beret_navy", "Lacivert Bere", "beret", 15000),
        new("beret_gold", "Altın Bere", "beret", 50000),
        new("beret_steel", "Çelik Bere", "beret", 0),
        new("beret_frost", "Ayaz Beresi", "beret", 0),
        new("armband_kartal", "Kartal Kol Bandı", "armband", 0),
        new("armband_bozkurt", "Bozkurt Kol Bandı", "armband", 1500),
        new("armband_simsek", "Şimşek Kol Bandı", "armband", 1500),
        new("armband_yildirim", "Yıldırım Kol Bandı", "armband", 1500),
        new("armband_kilic", "Kılıç Kol Bandı", "armband", 1500),
        new("armband_kaplan", "Kaplan Kol Bandı", "armband", 1500),
        new("armband_pars", "Pars Kol Bandı", "armband", 1500),
        new("armband_atmaca", "Atmaca Kol Bandı", "armband", 1500),
        new("armband_season1", "Sezon 1 Kol Bandı", "armband", 0),
        new("skin_mpt76_olive", "MPT-76 Zeytin Kaplama", "weapon_skin", 4000),
        new("skin_mpt76_desert", "MPT-76 Çöl Kaplama", "weapon_skin", 8000),
        new("skin_jng90_snow", "JNG-90 Kar Kaplama", "weapon_skin", 10000),
        new("skin_jng90_night", "JNG-90 Gece Kaplama", "weapon_skin", 18000),
        new("skin_pmt76_steel", "PMT-76 Çelik Kaplama", "weapon_skin", 9000),
        new("skin_g3_woodland", "G3A7 Orman Kaplama", "weapon_skin", 6000),
        new("skin_sar9_black", "SAR 9 Siyah", "weapon_skin", 2500),
        new("skin_escort_copper", "Escort Bakır", "weapon_skin", 7000),
        new("skin_knt76_ridge", "KNT-76 Sırt Kaplama", "weapon_skin", 11000),
        new("skin_mpt55_coast", "MPT-55 Kıyı Kaplama", "weapon_skin", 0),
        new("pose_salute", "Askeri Selam", "victory_pose", 0),
        new("pose_kneel_rifle", "Diz Çöküş", "victory_pose", 6000),
        new("pose_radio", "Telsiz Çağrısı", "victory_pose", 9000),
        new("pose_flag_plant", "Bayrak Dikme", "victory_pose", 0),
        new("pose_binoculars", "Dürbün Tarama", "victory_pose", 14000),
        new("frame_plain", "Düz Çerçeve", "emblem_frame", 0),
        new("frame_bronze", "Bronz Çerçeve", "emblem_frame", 4000),
        new("frame_silver", "Gümüş Çerçeve", "emblem_frame", 12000),
        new("frame_gold", "Altın Çerçeve", "emblem_frame", 30000),
        new("frame_season1", "Sezon 1 Çerçeve", "emblem_frame", 0),
        new("frame_archive_s1_top10", "S1 İlk 10 Rozeti", "emblem_frame", 0),
        new("frame_archive_s1_top100", "S1 İlk 100 Rozeti", "emblem_frame", 0)
    ];
}
