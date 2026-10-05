export const WEAPONS = [
  { id: 'pistol_sar9', name: 'SAR 9', cat: 'Tabanca', ammo: '9 mm', dmg: 28, mag: 15, rpm: 375, range: 120, hs: 2.0 },
  { id: 'pistol_tp9', name: 'Canik TP9', cat: 'Tabanca', ammo: '9 mm', dmg: 26, mag: 18, rpm: 400, range: 120, hs: 2.0 },
  { id: 'smg_sar109t', name: 'SAR 109T', cat: 'Hafif Makineli', ammo: '9 mm', dmg: 22, mag: 30, rpm: 800, range: 180, hs: 1.8 },
  { id: 'ar_mpt55', name: 'MPT-55', cat: 'Piyade Tüfeği', ammo: '5.56 mm', dmg: 26, mag: 30, rpm: 750, range: 450, hs: 2.0 },
  { id: 'ar_mpt76', name: 'MPT-76', cat: 'Piyade Tüfeği', ammo: '7.62 mm', dmg: 36, mag: 20, rpm: 600, range: 600, hs: 2.0 },
  { id: 'ar_g3a7', name: 'G3A7', cat: 'Piyade Tüfeği', ammo: '7.62 mm', dmg: 38, mag: 20, rpm: 550, range: 600, hs: 2.0 },
  { id: 'dmr_knt76', name: 'KNT-76', cat: 'Nişancı Tüfeği', ammo: '7.62 mm', dmg: 52, mag: 10, rpm: 214, range: 800, hs: 2.2 },
  { id: 'sr_jng90', name: 'JNG-90', cat: 'Keskin Nişancı', ammo: '7.62 mm', dmg: 90, mag: 5, rpm: 43, range: 1000, hs: 2.5 },
  { id: 'lmg_pmt76', name: 'PMT-76', cat: 'Makineli Tüfek', ammo: '7.62 mm', dmg: 34, mag: 100, rpm: 650, range: 600, hs: 1.8 },
  { id: 'sg_escort', name: 'Escort', cat: 'Pompalı', ammo: '12 Kalibre', dmg: 20, mag: 7, rpm: 71, range: 70, hs: 1.5 },
];

export const RANKS = [
  'Er', 'Onbaşı', 'Çavuş', 'Sözleşmeli Er', 'Uzman Onbaşı', 'Uzman Çavuş',
  'Astsubay Çavuş', 'Astsubay Kıdemli Çavuş', 'Astsubay Üstçavuş', 'Astsubay Kıdemli Üstçavuş',
  'Astsubay Başçavuş', 'Astsubay Kıdemli Başçavuş', 'Asteğmen', 'Teğmen', 'Üsteğmen',
  'Yüzbaşı', 'Binbaşı', 'Yarbay', 'Albay',
];

export const LOCATIONS = [
  { name: 'Kuzgun Köyü', kind: 'Köy', x: -118, z: 22 },
  { name: 'Yamaç Köyü', kind: 'Köy', x: 208, z: 178 },
  { name: 'Sınır Karakolu', kind: 'Karakol', x: 140, z: 392 },
  { name: 'İleri Üs Bölgesi', kind: 'İleri Üs', x: 262, z: -92 },
  { name: 'Taş Ocağı', kind: 'Ocak', x: -282, z: -150 },
  { name: 'Kuzgun Barajı', kind: 'Baraj', x: 20, z: -345 },
  { name: 'Röle Tepesi', kind: 'Röle', x: -322, z: 302 },
  { name: 'Çam Sırtı', kind: 'Orman', x: 318, z: 318 },
  { name: 'Ağıl', kind: 'Çiftlik', x: -196, z: -330 },
  { name: 'Yıkık Köy', kind: 'Harabe', x: 178, z: -298 },
];

export const LEADERBOARD = [
  { name: 'Kartal_07', rank: 'Yüzbaşı', xp: 48200, kills: 912, wins: 41 },
  { name: 'Bozkurt', rank: 'Üsteğmen', xp: 40110, kills: 780, wins: 33 },
  { name: 'ÇelikTim', rank: 'Teğmen', xp: 35500, kills: 640, wins: 28 },
  { name: 'Kuzgun', rank: 'Astsubay Başçavuş', xp: 29800, kills: 510, wins: 21 },
  { name: 'Hilal', rank: 'Uzman Çavuş', xp: 22100, kills: 390, wins: 15 },
  { name: 'MaviKuvvet', rank: 'Astsubay Üstçavuş', xp: 18750, kills: 310, wins: 12 },
  { name: 'KırmızıHat', rank: 'Uzman Onbaşı', xp: 14200, kills: 240, wins: 9 },
  { name: 'DereBoyu', rank: 'Çavuş', xp: 9800, kills: 180, wins: 6 },
  { name: 'SırtGöz', rank: 'Onbaşı', xp: 6200, kills: 110, wins: 3 },
  { name: 'ErYeni', rank: 'Er', xp: 1200, kills: 28, wins: 0 },
];

export const PATCHES = [
  { v: '0.3.0', date: '2026-10-01', notes: ['Topçu desteği dengelendi', 'Kirpi taşıma kapasitesi +1', 'Kuzgun Barajı loot düzeni'] },
  { v: '0.2.1', date: '2026-09-12', notes: ['MPT-76 geri tepme azaltıldı', 'Bot intikal yolu düzeltmesi', 'Rütbe XP eşikleri güncellendi'] },
  { v: '0.2.0', date: '2026-08-20', notes: ['10 kişilik tim lobi', 'T-70 / Kirpi intikal', 'İlk açık tatbikat'] },
];

export const NEWS = [
  { id: 'n1', title: 'Sezon 1: Vadi Tatbikatı başladı', body: 'Yeni rozetler, sezon sıralaması ve kozmetik bere renkleri aktif.' },
  { id: 'n2', title: 'Tim taktikleri rehberi', body: 'Kama düzeni ve L şekli pusu — resmi rehber yayında.' },
  { id: 'n3', title: 'Silah rehberi: MPT ailesi', body: 'MPT-55 ile MPT-76 ne zaman seçilir?' },
];

export const SERVERS = [
  { id: 'gs-ist-01', region: 'İstanbul', players: 48, capacity: 60, match: 'm-8841', status: 'live' },
  { id: 'gs-fra-02', region: 'Frankfurt', players: 52, capacity: 60, match: 'm-8842', status: 'live' },
  { id: 'gs-ams-01', region: 'Amsterdam', players: 12, capacity: 60, match: '—', status: 'idle' },
  { id: 'gs-ist-03', region: 'İstanbul', players: 60, capacity: 60, match: 'm-8840', status: 'full' },
];

export const SUSPECTS = [
  { name: 'AimBot_x', score: 92, reason: 'İmkânsız isabet oranı' },
  { name: 'WallPeek', score: 78, reason: 'Duvar arkası isabet' },
  { name: 'SpeedRun', score: 71, reason: 'Fiziksel hız aşımı' },
];

export function defaultProfile() {
  return {
    id: 'p-local',
    displayName: 'Tatbikatçı',
    rank: 'Uzman Çavuş',
    rankIndex: 5,
    xp: 6400,
    xpNext: 7800,
    stats: { matches: 84, wins: 11, kills: 312, headshots: 97, bestPlacement: 1, damage: 84200 },
    bestWeapon: 'ar_mpt76',
    recentMatches: [2, 5, 1, 8, 3, 4, 1, 6, 2, 7, 3, 1, 4, 9, 2, 5, 1, 3, 6, 2],
  };
}

export let session = {
  token: null,
  player: null,
  squad: null,
};

export function mockAuth(username, password) {
  if (!username || !password) throw new Error('Kullanıcı adı ve şifre gerekli');
  const player = { ...defaultProfile(), displayName: username };
  session.token = 'mock-jwt-' + btoa(username);
  session.player = player;
  return { token: session.token, player };
}

export function mockRegister(username, password) {
  return mockAuth(username, password || 'demo');
}
