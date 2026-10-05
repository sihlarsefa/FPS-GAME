namespace Harekat.Domain.Catalogs;

/// <summary>30+ başarım tanımları.</summary>
public static class AchievementCatalog
{
    public sealed record Definition(string Id, string Title, string Description, string Metric, int Target);

    public static readonly IReadOnlyList<Definition> All =
    [
        new("first_blood", "İlk Kan", "İlk öldürmeni yap", "kills", 1),
        new("first_victory", "İlk Zafer", "İlk maçını kazan", "wins", 1),
        new("sharpshooter_10", "Keskin Nişancı I", "10 kafa vuruşu yap", "headshots", 10),
        new("sharpshooter_100", "Keskin Nişancı II", "100 kafa vuruşu yap", "headshots", 100),
        new("sharpshooter_500", "Keskin Nişancı III", "500 kafa vuruşu yap", "headshots", 500),
        new("veteran_10", "Kıdemli Asker I", "10 maç oyna", "matches", 10),
        new("veteran_50", "Kıdemli Asker II", "50 maç oyna", "matches", 50),
        new("veteran_100", "Kıdemli Asker III", "100 maç oyna", "matches", 100),
        new("veteran_500", "Kıdemli Asker IV", "500 maç oyna", "matches", 500),
        new("killer_25", "Avcı I", "25 öldürme yap", "kills", 25),
        new("killer_100", "Avcı II", "100 öldürme yap", "kills", 100),
        new("killer_500", "Avcı III", "500 öldürme yap", "kills", 500),
        new("killer_1000", "Avcı IV", "1000 öldürme yap", "kills", 1000),
        new("champion_5", "Şampiyon I", "5 galibiyet al", "wins", 5),
        new("champion_25", "Şampiyon II", "25 galibiyet al", "wins", 25),
        new("champion_100", "Şampiyon III", "100 galibiyet al", "wins", 100),
        new("damage_10k", "Hasar Ustası I", "10.000 hasar ver", "damage", 10000),
        new("damage_100k", "Hasar Ustası II", "100.000 hasar ver", "damage", 100000),
        new("survivor_600", "Hayatta Kalan I", "10 dakika hayatta kal", "survival", 600),
        new("survivor_1200", "Hayatta Kalan II", "20 dakika hayatta kal", "survival", 1200),
        new("top3_10", "Podyum", "10 kez ilk 3'e gir", "top3", 10),
        new("command_transfer", "Komuta Devri", "Tim lideri olarak 5 maç bitir", "leader_matches", 5),
        new("squad_builder", "Tim Kurucu", "Bir tim kur", "squads_created", 1),
        new("social_butterfly", "Sosyal Asker", "5 arkadaş edin", "friends", 5),
        new("season_grinder", "Sezon Askeri", "Sezonda 5000 XP kazan", "season_xp", 5000),
        new("rank_onbasi", "Onbaşı", "Onbaşı rütbesine ulaş", "rank", 1),
        new("rank_cavus", "Çavuş", "Çavuş rütbesine ulaş", "rank", 2),
        new("rank_astsubay", "Astsubay", "Astsubay Çavuş rütbesine ulaş", "rank", 6),
        new("rank_officer", "Subay", "Asteğmen rütbesine ulaş", "rank", 12),
        new("rank_albay", "Albay", "Albay rütbesine ulaş", "rank", 18),
        new("cosmetic_collector", "Koleksiyoncu", "5 kozmetik ürün aç", "cosmetics", 5),
        new("bot_slayer", "Bot Avcısı", "Bot doldurulmuş maçta kazan", "bot_wins", 1),
        new("region_warrior", "Bölge Askeri", "Tercih ettiğin bölgede 20 maç oyna", "region_matches", 20),
        new("elo_climber", "Elo Tırmanışı", "Elo 1500'e ulaş", "elo", 1500),
        new("perfect_squad", "Tam Tim", "10 kişilik dolu timle maça gir", "full_squad_matches", 1)
    ];
}
