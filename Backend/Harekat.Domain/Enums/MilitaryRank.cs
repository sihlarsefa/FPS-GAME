namespace Harekat.Domain.Enums;

/// <summary>
/// TSK Kara Kuvvetleri rütbeleri (küçükten büyüğe). Sayısal değer kıdem sırasıdır; komuta zinciri buna göre işler.
/// Unity MilitaryRank ile birebir aynı olmalıdır.
/// </summary>
public enum MilitaryRank
{
    Er = 0,
    Onbasi = 1,
    Cavus = 2,
    SozlesmeliEr = 3,
    UzmanOnbasi = 4,
    UzmanCavus = 5,
    AstsubayCavus = 6,
    AstsubayKidemliCavus = 7,
    AstsubayUstcavus = 8,
    AstsubayKidemliUstcavus = 9,
    AstsubayBascavus = 10,
    AstsubayKidemliBascavus = 11,
    Astegmen = 12,
    Tegmen = 13,
    Ustegmen = 14,
    Yuzbasi = 15,
    Binbasi = 16,
    Yarbay = 17,
    Albay = 18
}
