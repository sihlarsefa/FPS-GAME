import { api } from './api.js';
import {
  WEAPONS, RANKS, LOCATIONS, PATCHES, NEWS, session, defaultProfile,
} from './mock-data.js';
import { t } from './i18n.js';

function esc(s) {
  return String(s ?? '').replace(/[&<>"']/g, (c) => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
  }[c]));
}

export async function renderHome() {
  return `
  <section class="hero" aria-labelledby="hero-title">
    <p class="pill">FPP · 10 KİŞİLİK TİM · KUZGUN VADİSİ</p>
    <h1 id="hero-title">HAREKÂT</h1>
    <p>Mavi ve Kırmızı kuvvetler Kuzgun Vadisi'nde tatbikatta. TSK rütbe zinciri, T-70 / Kirpi intikal, Türk silahları ve topçu desteğiyle hayatta kal.</p>
    <div class="hero-actions">
      <a class="btn primary" href="#/squad">${esc(t('home_cta'))}</a>
      <a class="btn ghost" href="#/leaderboards">${esc(t('home_secondary'))}</a>
    </div>
  </section>
  <section class="section grid cols-3">
    <article class="card"><h3>10 Kişilik Tim</h3><p>Komutan, keskin nişancı, makineli, sıhhiyeci, telsizci, bombacı ve piyade rolleri.</p></article>
    <article class="card"><h3>Komuta Zinciri</h3><p>Rütbe kıdemi maç içinde emir ve devir kurallarını belirler.</p></article>
    <article class="card"><h3>İntikal</h3><p>T-70 ve Kirpi ile sektörler arası hızlı yer değiştirme.</p></article>
    <article class="card"><h3>Türk Silahları</h3><p>MPT-76, SAR 9, JNG-90 ve daha fazlası — katalog değerleriyle.</p></article>
    <article class="card"><h3>Kuzgun Vadisi</h3><p>1024×1024 m harekât alanı; köy, karakol, üs, baraj, röle tepesi.</p></article>
    <article class="card"><h3>Topçu Desteği</h3><p>Telsizci çağrısıyla sınırlı mühimmatlı alan ateşi.</p></article>
  </section>`;
}

export async function renderLeaderboards() {
  const data = await api.leaderboards();
  const rows = (data.items || []).map((p, i) => `
    <tr>
      <td>${i + 1}</td>
      <td><a href="#/player/${encodeURIComponent(p.name)}">${esc(p.name)}</a></td>
      <td>${esc(p.rank)}</td>
      <td>${p.xp.toLocaleString('tr-TR')}</td>
      <td>${p.kills}</td>
      <td>${p.wins}</td>
    </tr>`).join('');
  return `
  <section class="section">
    <h2>Sıralamalar</h2>
    <div class="table-wrap">
      <table>
        <thead><tr><th>#</th><th>Oyuncu</th><th>Rütbe</th><th>XP</th><th>Öldürme</th><th>Zafer</th></tr></thead>
        <tbody>${rows}</tbody>
      </table>
    </div>
  </section>`;
}

export async function renderProfile() {
  const me = session.player || defaultProfile();
  const pct = Math.min(100, Math.round(((me.xp - (me.xpNext - 1600)) / 1600) * 100));
  return `
  <section class="section grid cols-2">
    <article class="card">
      <h2>Profil</h2>
      <p class="rank-badge"><img src="assets/ranks/rank-${me.rankIndex}.svg" alt="" width="28" height="28" /> ${esc(me.rank)}</p>
      <h3 style="margin-top:1rem">${esc(me.displayName)}</h3>
      <p class="muted">XP ${me.xp.toLocaleString('tr-TR')} / ${me.xpNext.toLocaleString('tr-TR')}</p>
      <div class="xp-bar" style="margin-top:.5rem" aria-label="XP çubuğu"><i style="--p:${pct}%"></i></div>
      <div class="grid cols-2" style="margin-top:1rem">
        <div><strong>${me.stats.matches}</strong><div class="muted">Maç</div></div>
        <div><strong>${me.stats.wins}</strong><div class="muted">Zafer</div></div>
        <div><strong>${me.stats.kills}</strong><div class="muted">Öldürme</div></div>
        <div><strong>${me.stats.headshots}</strong><div class="muted">Kafa</div></div>
      </div>
      <p style="margin-top:1rem"><a class="btn ghost" href="#/player/${encodeURIComponent(me.displayName)}">Paylaşılabilir kart</a></p>
    </article>
    <article class="card">
      <h2>Giriş / Kayıt</h2>
      <form class="form" id="authForm">
        <label>Kullanıcı adı<input name="username" required autocomplete="username" /></label>
        <label>Şifre<input name="password" type="password" required autocomplete="current-password" /></label>
        <div style="display:flex;gap:.5rem;flex-wrap:wrap">
          <button class="btn primary" type="submit" data-action="login">Giriş</button>
          <button class="btn ghost" type="submit" data-action="register">Kayıt</button>
        </div>
        <p class="muted" id="authMsg"></p>
      </form>
    </article>
  </section>
  <section class="section card">
    <h2>Son 20 Maç (sıralama)</h2>
    <div class="chart" aria-hidden="true">
      ${me.recentMatches.map((p) => `<span style="height:${Math.max(8, (11 - p) * 10)}%" title="#${p}"></span>`).join('')}
    </div>
    <p class="muted">Düşük çubuk = daha iyi sıralama (1. = zafer).</p>
  </section>`;
}

export async function renderSquad() {
  const sq = session.squad;
  return `
  <section class="section grid cols-2">
    <article class="card">
      <h2>Tim Kur</h2>
      <form class="form" id="createSquadForm">
        <label>Tim adı<input name="name" placeholder="Çelik Tim" required /></label>
        <button class="btn primary" type="submit">Kur</button>
      </form>
      <form class="form" id="joinSquadForm" style="margin-top:1.5rem">
        <label>Davet kodu<input name="code" placeholder="ABC123" required /></label>
        <button class="btn ghost" type="submit">Katıl</button>
      </form>
      <button class="btn primary" id="queueBtn" style="margin-top:1rem">Eşleşmeye Gir</button>
      <p class="muted" id="squadMsg"></p>
    </article>
    <article class="card">
      <h2>Aktif Tim</h2>
      ${sq ? `
        <p><strong>${esc(sq.name)}</strong></p>
        <p class="muted">Davet: <code>${esc(sq.inviteCode)}</code></p>
        <ul>${sq.members.map((m) => `<li>${esc(m.name)} — ${esc(m.role)}</li>`).join('')}</ul>
        <p class="muted">En fazla 10 kişi. Eksik koltuklar bot ile doldurulur.</p>
      ` : '<p class="muted">Henüz tim yok.</p>'}
    </article>
  </section>`;
}

export async function renderArsenal() {
  const cards = WEAPONS.map((w) => `
    <article class="card weapon-card">
      <h3>${esc(w.name)}</h3>
      <p><span class="pill">${esc(w.cat)}</span> <span class="pill">${esc(w.ammo)}</span></p>
      <div class="stat"><span class="muted">Hasar</span><b>${w.dmg}</b></div>
      <div class="stat"><span class="muted">Şarjör</span><b>${w.mag}</b></div>
      <div class="stat"><span class="muted">RPM</span><b>${w.rpm}</b></div>
      <div class="stat"><span class="muted">Menzil</span><b>${w.range} m</b></div>
      <div class="stat"><span class="muted">Kafa çarpanı</span><b>×${w.hs}</b></div>
    </article>`).join('');
  return `<section class="section"><h2>Silahlar</h2><div class="grid cols-3">${cards}</div></section>`;
}

export async function renderMap() {
  const half = 512;
  const toSvg = (x, z) => [((x + half) / (half * 2)) * 1000, ((half - z) / (half * 2)) * 1000];
  const marks = LOCATIONS.map((l) => {
    const [sx, sy] = toSvg(l.x, l.z);
    return `<g><circle cx="${sx}" cy="${sy}" r="10" fill="#e30a17" opacity=".85"/><text x="${sx + 14}" y="${sy + 4}" fill="#e8eef5" font-size="14" font-family="Oswald,sans-serif">${esc(l.name)}</text></g>`;
  }).join('');
  const grid = [];
  for (let i = 0; i <= 10; i++) {
    const p = i * 100;
    grid.push(`<line x1="${p}" y1="0" x2="${p}" y2="1000" stroke="#243040" stroke-width="1"/>`);
    grid.push(`<line x1="0" y1="${p}" x2="1000" y2="${p}" stroke="#243040" stroke-width="1"/>`);
    grid.push(`<text x="${p + 4}" y="16" fill="#8fa3b8" font-size="12">${String.fromCharCode(65 + Math.min(i, 9))}</text>`);
    grid.push(`<text x="4" y="${p + 14}" fill="#8fa3b8" font-size="12">${i + 1}</text>`);
  }
  return `
  <section class="section">
    <h2>Kuzgun Vadisi</h2>
    <p class="muted">1024×1024 m · grid 100 m · A–J / 1–10 · kuzey = +Z</p>
    <svg class="map-svg" viewBox="0 0 1000 1000" role="img" aria-label="Kuzgun Vadisi paftası">
      <rect width="1000" height="1000" fill="#0e1520"/>
      ${grid.join('')}
      <path d="M520 0 C510 120 480 250 500 400 S530 700 510 1000" fill="none" stroke="#3d7eff" stroke-width="8" opacity=".55"/>
      ${marks}
    </svg>
    <div class="grid cols-3" style="margin-top:1rem">
      ${LOCATIONS.map((l) => `<article class="card"><h3>${esc(l.name)}</h3><p class="muted">${esc(l.kind)} · (${l.x}, ${l.z})</p></article>`).join('')}
    </div>
  </section>`;
}

export async function renderPatches() {
  return `
  <section class="section">
    <h2>Yama Notları</h2>
    <div class="grid cols-2">
      ${PATCHES.map((p) => `
        <article class="card">
          <h3>${esc(p.v)} <span class="muted" style="font-weight:400">${esc(p.date)}</span></h3>
          <ul>${p.notes.map((n) => `<li>${esc(n)}</li>`).join('')}</ul>
        </article>`).join('')}
    </div>
  </section>`;
}

export async function renderNews() {
  return `
  <section class="section">
    <h2>Haberler & Topluluk</h2>
    <div class="grid cols-2">
      ${NEWS.map((n) => `<article class="card"><h3>${esc(n.title)}</h3><p>${esc(n.body)}</p></article>`).join('')}
      <article class="card">
        <h3>Rehberler</h3>
        <ul>
          <li><a href="#/arsenal">Silah rehberi</a></li>
          <li><a href="#/map">Harita taktikleri</a></li>
          <li><a href="#/season">Sezon başarımları</a></li>
        </ul>
      </article>
    </div>
  </section>`;
}

export async function renderSeason() {
  return `
  <section class="section grid cols-2">
    <article class="card">
      <h2>Sezon 1 — Vadi Tatbikatı</h2>
      <p class="muted">Sezonluk sıralama, ödül rozetleri, kozmetik bere renkleri.</p>
      <ul>
        <li>İlk Zafer</li>
        <li>Keskin Nişancı (100 kafa)</li>
        <li>Komuta Devri</li>
        <li>Kirpi Konvoyu</li>
        <li>Baraj Baskını</li>
      </ul>
    </article>
    <article class="card">
      <h2>Rütbe Nişanları</h2>
      <div class="grid cols-3">
        ${RANKS.slice(0, 12).map((r, i) => `
          <div class="rank-badge"><img src="assets/ranks/rank-${i}.svg" width="24" height="24" alt="" /> ${esc(r)}</div>
        `).join('')}
      </div>
      <p class="muted" style="margin-top:1rem">Stilize nişanlar — resmi armalar kopyalanmaz.</p>
    </article>
  </section>`;
}

export async function renderAdmin() {
  const servers = await api.servers();
  const suspects = await api.suspects();
  return `
  <section class="section">
    <h2>Yönetici Paneli</h2>
    <div class="alert">Sunucu filosu, aktif maçlar, raporlar ve şüpheli listesi (mock).</div>
    <div class="grid cols-2">
      <article class="card">
        <h3>Sunucu Filosu</h3>
        <div class="table-wrap">
          <table>
            <thead><tr><th>ID</th><th>Bölge</th><th>Oyuncu</th><th>Maç</th><th>Durum</th></tr></thead>
            <tbody>
              ${(servers.items || []).map((s) => `
                <tr>
                  <td>${esc(s.id)}</td><td>${esc(s.region)}</td>
                  <td>${s.players}/${s.capacity}</td><td>${esc(s.match)}</td>
                  <td><span class="pill ${s.status === 'live' ? 'ok' : s.status === 'full' ? 'warn' : ''}">${esc(s.status)}</span></td>
                </tr>`).join('')}
            </tbody>
          </table>
        </div>
        <div class="chart" style="margin-top:1rem">
          ${(servers.items || []).map((s) => `<span style="height:${(s.players / s.capacity) * 100}%"></span>`).join('')}
        </div>
      </article>
      <article class="card">
        <h3>Hile Şüphelileri</h3>
        <ul>
          ${(suspects.items || []).map((s) => `<li><strong>${esc(s.name)}</strong> — skor ${s.score}: ${esc(s.reason)}</li>`).join('')}
        </ul>
        <h3 style="margin-top:1.5rem">Oyuncu Ara / Yasakla</h3>
        <form class="form" id="banForm">
          <label>Oyuncu<input name="name" required /></label>
          <label>Sebep<input name="reason" placeholder="Rapor #..." /></label>
          <button class="btn primary" type="submit">Yasakla (mock)</button>
          <p class="muted" id="banMsg"></p>
        </form>
      </article>
    </div>
  </section>`;
}

export async function renderPlayer(name) {
  const board = await api.leaderboards();
  const hit = (board.items || []).find((p) => p.name === name) || {
    name, rank: 'Er', xp: 0, kills: 0, wins: 0,
  };
  const weapon = WEAPONS.find((w) => w.id === 'ar_mpt76');
  return `
  <section class="section card">
    <h2>Oyuncu Kartı — ${esc(hit.name)}</h2>
    <p class="rank-badge"><img src="assets/ranks/rank-5.svg" width="32" height="32" alt="" /> ${esc(hit.rank)}</p>
    <div class="grid cols-3" style="margin-top:1rem">
      <div><strong>${hit.xp.toLocaleString('tr-TR')}</strong><div class="muted">XP</div></div>
      <div><strong>${hit.kills}</strong><div class="muted">Öldürme</div></div>
      <div><strong>${hit.wins}</strong><div class="muted">Zafer</div></div>
    </div>
    <p style="margin-top:1rem">En iyi silah: <strong>${esc(weapon?.name || 'MPT-76')}</strong></p>
    <p><a class="btn ghost" href="#/match/demo">Örnek maç detayı</a></p>
  </section>`;
}

export async function renderMatch(id) {
  return `
  <section class="section">
    <h2>Maç ${esc(id)}</h2>
    <div class="grid cols-2">
      <article class="card">
        <h3>Tim Sıralaması</h3>
        <ol>
          <li>Çelik Tim — 1. (zafer)</li>
          <li>Kuzgunlar — 2.</li>
          <li>Dere Tim — 3.</li>
        </ol>
      </article>
      <article class="card">
        <h3>Öldürme Akışı</h3>
        <ul>
          <li>02:14 Kartal_07 → bot_12 (MPT-76)</li>
          <li>05:40 Bozkurt → Hilal (JNG-90 kafa)</li>
          <li>11:02 ÇelikTim → MaviKuvvet (topçu)</li>
        </ul>
      </article>
    </div>
    <article class="card" style="margin-top:1rem">
      <h3>Ölüm / İniş (örnek)</h3>
      <svg class="map-svg" viewBox="0 0 400 200" aria-label="Maç haritası noktaları">
        <rect width="400" height="200" fill="#0e1520"/>
        <circle cx="120" cy="80" r="6" fill="#3d7eff"/><text x="130" y="84" fill="#8fa3b8" font-size="10">iniş</text>
        <circle cx="260" cy="130" r="6" fill="#e30a17"/><text x="270" y="134" fill="#8fa3b8" font-size="10">ölüm</text>
      </svg>
    </article>
  </section>`;
}

export async function renderNotFound() {
  return `<section class="section card"><h2>Sayfa bulunamadı</h2><p><a href="#/">Ana sayfaya dön</a></p></section>`;
}
