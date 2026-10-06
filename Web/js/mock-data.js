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


export const MAPS = [
  {
    id: 'kuzgun',
    name: 'Kuzgun Vadisi',
    size: '1024×1024 m',
    mode: 'Battle Royale',
    blurb: 'Dağ vadisi, baraj ve köyler. Ana harekât alanı.',
    tactics: ['Baraj üstü sniper açıları', 'Köy içi yakın muharebe', 'Röle Tepesi erken loot'],
    locations: [
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
    ],
  },
  {
    id: 'ayaz',
    name: 'Ayaz Geçidi',
    size: '896×896 m',
    mode: 'Battle Royale',
    blurb: 'Karlı geçit, tünel ve sırt yolları. Görüş kısadır.',
    tactics: ['Tünel çıkışlarında pusu', 'Sırt hattında DMR', 'Kirpi ile geçit kontrolü'],
    locations: [
      { name: 'Karakol Kapısı', kind: 'Kapı', x: -60, z: 280 },
      { name: 'Buz Tüneli', kind: 'Tünel', x: 40, z: 40 },
      { name: 'Sırt Kampı', kind: 'Kamp', x: 220, z: -120 },
      { name: 'Donmuş Dere', kind: 'Dere', x: -180, z: -200 },
      { name: 'Radar Kulesi', kind: 'Kule', x: 160, z: 240 },
      { name: 'Çığ Yatağı', kind: 'Yamaç', x: -240, z: 80 },
    ],
  },
  {
    id: 'mavi',
    name: 'Mavi Liman',
    size: '960×960 m',
    mode: 'Battle Royale / Çatışma',
    blurb: 'Liman, konteyner sahası ve iskele. Dikey kapak bol.',
    tactics: ['Konteyner labirentinde SMG', 'İskele uzun koridor DMR', 'Vinç üstü gözetleme'],
    locations: [
      { name: 'Ana İskele', kind: 'İskele', x: 0, z: -280 },
      { name: 'Konteyner Sahası', kind: 'Depo', x: 180, z: -40 },
      { name: 'Gümrük', kind: 'Bina', x: -160, z: 60 },
      { name: 'Fener', kind: 'Kule', x: 260, z: -220 },
      { name: 'Tersane', kind: 'Atölye', x: -220, z: -100 },
      { name: 'Yakıt Deposu', kind: 'Depo', x: 80, z: 200 },
    ],
  },
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
  { rank: 1, playerId: 'p1', username: 'Kartal_07', militaryRank: 15, value: 48200, elo: 1840 },
  { rank: 2, playerId: 'p2', username: 'Bozkurt', militaryRank: 14, value: 40110, elo: 1760 },
  { rank: 3, playerId: 'p3', username: 'ÇelikTim', militaryRank: 13, value: 35500, elo: 1690 },
  { rank: 4, playerId: 'p4', username: 'Kuzgun', militaryRank: 10, value: 29800, elo: 1610 },
  { rank: 5, playerId: 'p5', username: 'Hilal', militaryRank: 5, value: 22100, elo: 1520 },
  { rank: 6, playerId: 'p6', username: 'MaviKuvvet', militaryRank: 8, value: 18750, elo: 1480 },
  { rank: 7, playerId: 'p7', username: 'KırmızıHat', militaryRank: 4, value: 14200, elo: 1410 },
  { rank: 8, playerId: 'p8', username: 'DereBoyu', militaryRank: 2, value: 9800, elo: 1340 },
  { rank: 9, playerId: 'p9', username: 'SırtGöz', militaryRank: 1, value: 6200, elo: 1280 },
  { rank: 10, playerId: 'p10', username: 'ErYeni', militaryRank: 0, value: 1200, elo: 1100 },
  { rank: 11, playerId: 'p11', username: 'BarajBek', militaryRank: 3, value: 8400, elo: 1320 },
  { rank: 12, playerId: 'p12', username: 'RöleGöz', militaryRank: 6, value: 16200, elo: 1450 },
];

export const PATCHES = [
  { v: '0.4.0', date: '2026-10-05', notes: ['Web portal v2 JWT entegrasyonu', 'Yönetici paneli rol menüsü', 'IIS web.config'] },
  { v: '0.3.0', date: '2026-10-01', notes: ['Topçu desteği dengelendi', 'Kirpi taşıma kapasitesi +1', 'Kuzgun Barajı loot düzeni'] },
  { v: '0.2.1', date: '2026-09-12', notes: ['MPT-76 geri tepme azaltıldı', 'Bot intikal yolu düzeltmesi', 'Rütbe XP eşikleri güncellendi'] },
  { v: '0.2.0', date: '2026-08-20', notes: ['10 kişilik tim lobi', 'T-70 / Kirpi intikal', 'İlk açık tatbikat'] },
];

export const NEWS = [
  { id: 'n1', slug: 'sezon-1', title: 'Sezon 1: Vadi Tatbikatı başladı', body: 'Yeni rozetler, sezon sıralaması ve kozmetik bere renkleri aktif.', date: '2026-09-01' },
  { id: 'n2', slug: 'tim-taktikleri', title: 'Tim taktikleri rehberi', body: 'Kama düzeni ve L şekli pusu — resmi rehber yayında.', date: '2026-09-10' },
  { id: 'n3', slug: 'mpt-rehberi', title: 'Silah rehberi: MPT ailesi', body: 'MPT-55 ile MPT-76 ne zaman seçilir?', date: '2026-09-18' },
];

export const SERVERS = [
  { id: 'gs-ist-01', endpoint: '10.0.1.11:7777', region: 'İstanbul', status: 'InMatch', currentPlayers: 48, maxPlayers: 60, cpu: 62, ram: 71, activeMatch: 'm-8841', port: 7777 },
  { id: 'gs-fra-02', endpoint: '10.0.2.22:7777', region: 'Frankfurt', status: 'InMatch', currentPlayers: 52, maxPlayers: 60, cpu: 74, ram: 68, activeMatch: 'm-8842', port: 7777 },
  { id: 'gs-ams-01', endpoint: '10.0.3.33:7777', region: 'Amsterdam', status: 'Idle', currentPlayers: 0, maxPlayers: 60, cpu: 12, ram: 34, activeMatch: null, port: 7777 },
  { id: 'gs-ist-03', endpoint: '10.0.1.13:7778', region: 'İstanbul', status: 'Full', currentPlayers: 60, maxPlayers: 60, cpu: 88, ram: 79, activeMatch: 'm-8840', port: 7778 },
];

export const SUSPECTS = [
  { playerId: 'x1', matchId: 'm-8841', totalScore: 92, generatedAt: '2026-10-05T12:00:00Z', findings: [{ ruleId: 'aim', rule: 'Aim', scoreContribution: 40, detail: 'İmkânsız isabet oranı' }] },
  { playerId: 'x2', matchId: 'm-8842', totalScore: 78, generatedAt: '2026-10-05T11:30:00Z', findings: [{ ruleId: 'wall', rule: 'Wall', scoreContribution: 35, detail: 'Duvar arkası isabet' }] },
  { playerId: 'x3', matchId: 'm-8840', totalScore: 71, generatedAt: '2026-10-05T10:10:00Z', findings: [{ ruleId: 'speed', rule: 'Speed', scoreContribution: 30, detail: 'Fiziksel hız aşımı' }] },
];

export const REPORTS = [
  { id: 'r1', reportedPlayerId: 'x1', reporterId: 'p1', reason: 'Hile şüphesi', matchId: 'm-8841', createdAt: '2026-10-05T12:05:00Z', status: 'Open' },
  { id: 'r2', reportedPlayerId: 'x2', reporterId: 'p3', reason: 'Toksik chat', matchId: 'm-8842', createdAt: '2026-10-05T11:40:00Z', status: 'Open' },
];

export const ACHIEVEMENTS = [
  { id: 'first_win', title: 'İlk Zafer', description: 'Bir maçta 1. ol', target: 1, progress: 1, unlocked: true },
  { id: 'sniper_100', title: 'Keskin Nişancı', description: '100 kafa vuruşu', target: 100, progress: 97, unlocked: false },
  { id: 'command', title: 'Komuta Devri', description: 'Tim komutanı olarak zafer', target: 1, progress: 1, unlocked: true },
  { id: 'kirpi', title: 'Kirpi Konvoyu', description: 'Kirpi ile 5 intikal', target: 5, progress: 3, unlocked: false },
  { id: 'dam', title: 'Baraj Baskını', description: 'Kuzgun Barajı\'nda 10 öldürme', target: 10, progress: 10, unlocked: true },
];

export const MATCH_DEMO = {
  id: 'demo',
  region: 'İstanbul',
  status: 'Finished',
  serverEndpoint: '10.0.1.11:7777',
  seasonNumber: 1,
  teams: [
    { squadId: 's1', squadName: 'Çelik Tim', playerIds: ['p1', 'p2'], botCount: 8, placement: 1 },
    { squadId: 's2', squadName: 'Kuzgunlar', playerIds: ['p3'], botCount: 9, placement: 2 },
    { squadId: 's3', squadName: 'Dere Tim', playerIds: ['p5'], botCount: 9, placement: 3 },
  ],
  killFeed: [
    { t: '02:14', killer: 'Kartal_07', victim: 'bot_12', weapon: 'MPT-76', headshot: false },
    { t: '05:40', killer: 'Bozkurt', victim: 'Hilal', weapon: 'JNG-90', headshot: true },
    { t: '11:02', killer: 'ÇelikTim', victim: 'MaviKuvvet', weapon: 'Topçu', headshot: false },
  ],
  landings: [
    { x: -100, z: 40, name: 'Kartal_07' },
    { x: 200, z: 160, name: 'Bozkurt' },
    { x: 20, z: -300, name: 'Hilal' },
  ],
  deaths: [
    { x: 120, z: 80, name: 'bot_12' },
    { x: -50, z: -20, name: 'Hilal' },
    { x: 260, z: -90, name: 'MaviKuvvet' },
  ],
};

function career(overrides = {}) {
  return {
    matches: 84, wins: 11, kills: 312, headshots: 97,
    bestPlacement: 1, totalDamage: 84200, longestSurvivalSeconds: 1420,
    experience: 6400, rank: 5, ...overrides,
  };
}

export function defaultPlayer(username = 'Tatbikatçı', role = 'Player') {
  return {
    id: 'p-local',
    username,
    email: `${username.toLowerCase()}@harekat.local`,
    stats: career(),
    rank: 5,
    eloRating: 1420,
    squadId: null,
    emailVerified: true,
    role,
    isOnline: true,
    region: 'tr',
    ownedCosmetics: ['camo_olive', 'beret_red'],
    equippedCamo: 'camo_olive',
    equippedBeret: 'beret_red',
    unlockedAchievements: ['first_win', 'command', 'dam'],
    seasonXp: 4200,
    recentMatches: [2, 5, 1, 8, 3, 4, 1, 6, 2, 7, 3, 1, 4, 9, 2, 5, 1, 3, 6, 2],
    bestWeaponId: 'ar_mpt76',
  };
}

/** @deprecated use defaultPlayer */
export function defaultProfile() {
  const p = defaultPlayer();
  return {
    id: p.id,
    displayName: p.username,
    rank: RANKS[p.rank],
    rankIndex: p.rank,
    xp: p.stats.experience,
    xpNext: (p.rank + 1) * 1600,
    stats: p.stats,
    bestWeapon: p.bestWeaponId,
    recentMatches: p.recentMatches,
  };
}

export let mockState = {
  player: null,
  squad: null,
  ticket: null,
  friends: [],
};

export function mockAuthResponse(username, role = 'Player') {
  const player = defaultPlayer(username, role);
  if (username.toLowerCase() === 'admin') player.role = 'Admin';
  if (username.toLowerCase() === 'mod') player.role = 'Moderator';
  mockState.player = player;
  const expiresAt = new Date(Date.now() + 3600_000).toISOString();
  return {
    accessToken: 'mock-jwt-' + btoa(unescape(encodeURIComponent(username))),
    refreshToken: 'mock-refresh-' + btoa(unescape(encodeURIComponent(username))),
    expiresAt,
    player,
  };
}

export function mockAuth(username, password) {
  if (!username || !password) throw Object.assign(new Error('Kullanıcı adı ve şifre gerekli'), { status: 400 });
  return mockAuthResponse(username);
}

export function mockRegister(username, password, email = '') {
  if (!username || !password) throw Object.assign(new Error('Kullanıcı adı ve şifre gerekli'), { status: 400 });
  return mockAuthResponse(username);
}

/** Legacy session object used by older page code during migration. */
export let session = {
  token: null,
  player: null,
  squad: null,
};

export function syncLegacySession(auth) {
  session.token = auth?.accessToken || null;
  session.player = auth?.player ? {
    ...defaultProfile(),
    displayName: auth.player.username,
    rank: RANKS[auth.player.rank] || RANKS[0],
    rankIndex: auth.player.rank,
    xp: auth.player.stats?.experience ?? 0,
    stats: auth.player.stats,
  } : null;
}
