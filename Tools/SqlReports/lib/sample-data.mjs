/** Örnek KPI veri seti — MSSQL bağlantısı yokken kullanılır. */
export function buildSamplePayload(generatedAt = new Date()) {
  const iso = generatedAt.toISOString();
  return {
    mode: 'sample',
    generatedAt: iso,
    connection: null,
    kpis: {
      dau: 1842,
      wau: 9120,
      mau: 28450,
      onlineNow: 126,
      retentionD1: 42.5,
      retentionD7: 21.8,
      retentionD30: 9.4,
      avgMatchDurationMin: 18.4,
      avgMatchWaitSec: 38,
      fleetOccupancyPct: 61.2,
      suspectReports7d: 47
    },
    series: {
      dauTrend: [
        { label: 'D-6', value: 1610 },
        { label: 'D-5', value: 1722 },
        { label: 'D-4', value: 1688 },
        { label: 'D-3', value: 1901 },
        { label: 'D-2', value: 2014 },
        { label: 'D-1', value: 1955 },
        { label: 'Bugün', value: 1842 }
      ],
      matchDuration: [
        { label: '0-5dk', value: 42 },
        { label: '5-10dk', value: 180 },
        { label: '10-15dk', value: 410 },
        { label: '15-20dk', value: 620 },
        { label: '20-30dk', value: 390 },
        { label: '30dk+', value: 95 }
      ],
      weapons: [
        { label: 'ar_mpt76', value: 4200 },
        { label: 'ar_mpt55', value: 3100 },
        { label: 'smg_sar109t', value: 2800 },
        { label: 'dmr_knt76', value: 1900 },
        { label: 'sr_jng90', value: 1100 },
        { label: 'lmg_pmt76', value: 980 },
        { label: 'ar_g3a7', value: 870 },
        { label: 'sg_escort', value: 640 },
        { label: 'pistol_sar9', value: 520 },
        { label: 'pistol_tp9', value: 410 }
      ],
      ranks: [
        { label: 'Er', value: 8200 },
        { label: 'Onbaşı', value: 5400 },
        { label: 'Çavuş', value: 3900 },
        { label: 'Uzman', value: 4100 },
        { label: 'Astsubay', value: 3600 },
        { label: 'Subay', value: 2250 },
        { label: 'Üst subay', value: 1000 }
      ],
      waitBuckets: [
        { label: '0-15sn', value: 210 },
        { label: '15-45sn', value: 480 },
        { label: '45-90sn', value: 320 },
        { label: '90-180sn', value: 140 },
        { label: '180sn+', value: 55 }
      ],
      suspicionTrend: [
        { label: 'Pzt', value: 6 },
        { label: 'Sal', value: 8 },
        { label: 'Çar', value: 5 },
        { label: 'Per', value: 11 },
        { label: 'Cum', value: 9 },
        { label: 'Cmt', value: 4 },
        { label: 'Paz', value: 4 }
      ],
      placement: [
        { label: '1', value: 120 },
        { label: '2', value: 118 },
        { label: '3', value: 121 },
        { label: '4-6', value: 360 },
        { label: '7-10', value: 480 }
      ],
      servers: [
        { label: 'tr', value: 68 },
        { label: 'eu', value: 54 },
        { label: 'me', value: 41 }
      ]
    },
    tables: {
      topSquads: [
        { squad: 'Mavi-7', matches: 48, winPct: 31.2, avgPlace: 3.4 },
        { squad: 'Kırmızı-3', matches: 52, winPct: 28.8, avgPlace: 3.9 },
        { squad: 'Tim Anadolu', matches: 40, winPct: 27.5, avgPlace: 4.1 }
      ],
      weaponCompare: [
        { weapon: 'ar_mpt76', expectedTtk: 0.32, actualTtk: 0.29, verdict: 'guclu_outlier' },
        { weapon: 'sr_jng90', expectedTtk: 0.0, actualTtk: 0.12, verdict: 'izle' },
        { weapon: 'sg_escort', expectedTtk: 0.2, actualTtk: 0.21, verdict: 'uyumlu' }
      ]
    }
  };
}
