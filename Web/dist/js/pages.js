import { api, WEAPONS, RANKS } from './api.js';
import { LOCATIONS, PATCHES, MATCH_DEMO } from './mock-data.js';
import { t } from './i18n.js';
import { esc, formatNumber, xpProgress, worldToSvg } from './util.js';
import { CONFIG } from './config.js';
import { pushCtaHtml } from './push.js';

function mapGrid(size = 1000) {
  const grid = [];
  for (let i = 0; i <= 10; i++) {
    const p = i * (size / 10);
    grid.push(`<line x1="${p}" y1="0" x2="${p}" y2="${size}" stroke="#243040" stroke-width="1"/>`);
    grid.push(`<line x1="0" y1="${p}" x2="${size}" y2="${p}" stroke="#243040" stroke-width="1"/>`);
    grid.push(`<text x="${p + 4}" y="16" fill="#8fa3b8" font-size="12">${String.fromCharCode(65 + Math.min(i, 9))}</text>`);
    grid.push(`<text x="4" y="${p + 14}" fill="#8fa3b8" font-size="12">${i + 1}</text>`);
  }
  return grid.join('');
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
      <a class="btn ghost" href="#/login">${esc(t('nav_login'))}</a>
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

export async function renderLogin() {
  const me = api.currentPlayer();
  if (me) {
    return `
    <section class="section card" style="max-width:480px">
      <h2>${esc(t('login_title'))}</h2>
      <p>${esc(me.username)} · ${esc(api.rankName(me.rank))} · ${esc(me.role)}</p>
      <button class="btn ghost" type="button" id="logoutBtn">${esc(t('logout'))}</button>
      <p class="muted" id="authMsg"></p>
    </section>`;
  }
  return `
  <section class="section card" style="max-width:480px">
    <h2>${esc(t('login_title'))}</h2>
    <form class="form" id="authForm" novalidate>
      <label>${esc(t('login_username'))}
input name="username" required minlength="3" autocomplete="username" aria-required="true" /></label>
      <label>${esc(t('login_email'))}<input name="email" type="email" autocomplete="email" data-register-only hidden /></label>
      <label>${esc(t('login_password'))}<input name="password" type="password" required minlength="6" autocomplete="current-password" aria-required="true" /></label>
      <div style="display:flex;gap:.5rem;flex-wrap:wrap">
        <button class="btn primary" type="submit" data-action="login">${esc(t('login_submit'))}</button>
        <button class="btn ghost" type="submit" data-action="register">${esc(t('register_submit'))}</button>
      </div>
      <p class="muted" id="authMsg" role="status"></p>
      <p class="muted">Mock: kullanıcı <code>admin</code> / <code>mod</code> yetki verir.</p>
    </form>
  </section>`;
}

export async function renderProfile() {
  let me = api.currentPlayer();
  if (api.isAuthed()) {
    try { me = await api.me(); } catch { /* keep session */ }
  }
  if (!me) {
    return `<section class="section card"><h2>${esc(t('profile_title'))}</h2><p class="muted"><a href="#/login">${esc(t('nav_login'))}</a></p></section>`;
  }
  const xp = xpProgress(me.stats?.experience ?? me.seasonXp ?? 0, me.rank);
  const weapon = WEAPONS.find((w) => w.id === (me.bestWeaponId || 'ar_mpt76'));
  const recent = me.recentMatches || [2, 5, 1, 8, 3, 4, 1, 6, 2, 7];
  return `
  <section class="section grid cols-2">
    <article class="card">
      <h2>${esc(t('profile_title'))}</h2>
      <p class="rank-badge"><img src="assets/ranks/rank-${me.rank}.svg" alt="" width="28" height="28" /> ${esc(api.rankName(me.rank))}</p>
      <h3 style="margin-top:1rem">${esc(me.username)}</h3>
      <p class="muted">XP ${formatNumber(me.stats?.experience, CONFIG.lang)} / ${formatNumber(xp.next, CONFIG.lang)} · Elo ${me.eloRating}</p>
      <div class="xp-bar" style="margin-top:.5rem" role="progressbar" aria-valuenow="${xp.pct}" aria-valuemin="0" aria-valuemax="100" aria-label="XP"><i style="--p:${xp.pct}%"></i></div>
      <div class="grid cols-2" style="margin-top:1rem">
        <div><strong>${me.stats?.matches ?? 0}</strong><div class="muted">Maç</div></div>
        <div><strong>${me.stats?.wins ?? 0}</strong><div class="muted">Zafer</div></div>
        <div><strong>${me.stats?.kills ?? 0}</strong><div class="muted">Öldürme</div></div>
        <div><strong>${me.stats?.headshots ?? 0}</strong><div class="muted">Kafa</div></div>
      </div>
      <p style="margin-top:1rem">${esc(t('profile_best_weapon'))}: <strong>${esc(weapon?.name || 'MPT-76')}</strong></p>
      <p><a class="btn ghost" href="#/player/${encodeURIComponent(me.username)}">Paylaşılabilir kart</a>
         <a class="btn ghost" href="#/achievements">${esc(t('nav_achievements'))}</a></p>
    </article>
    <article class="card">
      <h2>${esc(t('profile_career'))}</h2>
      <ul>
        <li>En iyi yerleşim: #${me.stats?.bestPlacement ?? '—'}</li>
        <li>Toplam hasar: ${formatNumber(me.stats?.totalDamage, CONFIG.lang)}</li>
        <li>En uzun hayatta kalma: ${me.stats?.longestSurvivalSeconds ?? 0}s</li>
        <li>Sezon XP: ${formatNumber(me.seasonXp, CONFIG.lang)}</li>
        <li>Bölge: ${esc(me.region)} · Rol: ${esc(me.role)}</li>
      </ul>
    </article>
  </section>
  <section class="section card">
    <h2>${esc(t('profile_recent'))}</h2>
    <div class="chart" aria-hidden="true">
      ${recent.map((p) => `<span style="height:${Math.max(8, (11 - p) * 10)}%" title="#${p}"></span>`).join('')}
    </div>
    <p class="muted">Düşük çubuk = daha iyi sıralama (1. = zafer). <a href="#/match/demo">Örnek maç</a></p>
  </section>`;
}

export async function renderSquad() {
  let sq = null;
  if (api.isAuthed()) {
    try { sq = await api.mySquad(); } catch { sq = null; }
  }
  const slots = Array.from({ length: 10 }, (_, i) => {
    const m = sq?.members?.[i] || (sq?.memberIds?.[i] ? { id: sq.memberIds[i], name: sq.memberIds[i].slice(0, 8), ready: (sq.readyMemberIds || []).includes(sq.memberIds[i]) } : null);
    if (!m) return `<li class="slot empty muted">— boş / bot —</li>`;
    return `<li class="slot ${m.ready ? 'ready' : ''}"><span>${esc(m.name || m.id)}</span> <span class="pill ${m.ready ? 'ok' : ''}">${m.ready ? 'HAZIR' : '…'}</span></li>`;
  }).join('');
  return `
  <section class="section grid cols-2">
    <article class="card">
      <h2>${esc(t('squad_create'))}</h2>
      <form class="form" id="createSquadForm">
        <label>Tim adı<input name="name" placeholder="Çelik Tim" required maxlength="32" /></label>
        <button class="btn primary" type="submit">${esc(t('squad_create'))}</button>
      </form>
      <form class="form" id="joinSquadForm" style="margin-top:1.5rem">
        <label>${esc(t('squad_invite'))}<input name="code" placeholder="ABC123" required maxlength="12" /></label>
        <button class="btn ghost" type="submit">${esc(t('squad_join'))}</button>
      </form>
      <p class="muted" id="squadMsg" role="status"></p>
      <p class="muted">Canlı: <span id="lobbyStatus">—</span></p>
    </article>
    <article class="card">
      <h2>${esc(t('squad_title'))}</h2>
      ${sq ? `
        <p><strong>${esc(sq.name)}</strong></p>
        <p class="muted">${esc(t('squad_invite'))}: <code id="inviteCode">${esc(sq.inviteCode)}</code></p>
        <p class="muted">Açık koltuk: ${sq.openSlots ?? (10 - (sq.memberIds?.length || 0))} · Tümü hazır: ${sq.allReady ? 'evet' : 'hayır'}</p>
        <h3>${esc(t('squad_members'))}</h3>
        <ul class="member-list" id="memberList">${slots}</ul>
        <div style="display:flex;gap:.5rem;flex-wrap:wrap;margin-top:1rem">
          <button class="btn primary" type="button" id="readyBtn">${esc(t('squad_ready'))}</button>
          <button class="btn ghost" type="button" id="unreadyBtn">${esc(t('squad_unready'))}</button>
          <button class="btn ghost" type="button" id="leaveSquadBtn">${esc(t('squad_leave'))}</button>
          <a class="btn primary" href="#/matchmaking">${esc(t('nav_matchmaking'))}</a>
        </div>
      ` : `<p class="muted">Henüz tim yok. ${api.isAuthed() ? '' : `<a href="#/login">${esc(t('nav_login'))}</a>`}</p>`}
      ${pushCtaHtml()}
    </article>
  </section>`;
}

export async function renderMatchmaking() {
  return `
  <section class="section card" style="max-width:560px">
    <h2>${esc(t('mm_title'))}</h2>
    <p class="muted">10 kişilik tim hazır olduğunda eşleşmeye gir. Eksik koltuklar bot ile doldurulur.</p>
    <form class="form" id="queueForm">
      <label>Bölge
        <select name="region">
          <option value="tr">Türkiye</option>
          <option value="eu">Avrupa</option>
        </select>
      </label>
      <label>Max ping (ms)<input name="maxPingMs" type="number" min="20" max="200" value="80" /></label>
      <div style="display:flex;gap:.5rem;flex-wrap:wrap">
        <button class="btn primary" type="submit">${esc(t('mm_queue'))}</button>
        <button class="btn ghost" type="button" id="cancelQueueBtn">${esc(t('mm_cancel'))}</button>
      </div>
    </form>
    <div id="mmStatus" class="alert" style="margin-top:1rem" role="status" aria-live="polite">
      Kuyruk bekleniyor.
    </div>
    <dl class="stat-dl">
      <div><dt>${esc(t('mm_elapsed'))}</dt><dd id="mmElapsed">—</dd></div>
      <div><dt>${esc(t('mm_eta'))}</dt><dd id="mmEta">—</dd></div>
      <div><dt>Durum</dt><dd id="mmState">Idle</dd></div>
    </dl>
  </section>`;
}

export async function renderLeaderboards(query = {}) {
  const season = query.season ? Number(query.season) : null;
  const page = Number(query.page) || 1;
  const take = 10;
  const data = season
    ? await api.seasonLeaderboard({ season, take, page })
    : await api.leaderboards({ take, page });
  const items = data.items || data || [];
  const total = data.total || items.length;
  const pages = Math.max(1, Math.ceil(total / take));
  const rows = items.map((p) => `
    <tr>
      <td>${p.rank ?? ''}</td>
      <td><a href="#/player/${encodeURIComponent(p.username || p.name)}">${esc(p.username || p.name)}</a></td>
      <td>${esc(api.rankName(p.militaryRank ?? 0))}</td>
      <td>${formatNumber(p.value ?? p.xp, CONFIG.lang)}</td>
      <td>${p.elo ?? '—'}</td>
    </tr>`).join('');
  return `
  <section class="section">
    <h2>${esc(t('lb_title'))}</h2>
    <form class="form inline-form" id="lbFilterForm">
      <label>${esc(t('lb_season'))}
select name="season">
          <option value="" ${!season ? 'selected' : ''}>Genel</option>
          <option value="1" ${season === 1 ? 'selected' : ''}>Sezon 1</option>
          <option value="0" ${season === 0 ? 'selected' : ''}>Sezon 0 (arşiv)</option>
        </select>
      </label>
      <button class="btn ghost" type="submit">Filtrele</button>
    </form>
    <div class="table-wrap" style="margin-top:1rem">
      <table>
        <thead><tr><th>#</th><th>Oyuncu</th><th>Rütbe</th><th>XP</th><th>Elo</th></tr></thead>
        <tbody>${rows}</tbody>
      </table>
    </div>
    <nav class="pager" aria-label="Sayfalama" style="margin-top:1rem;display:flex;gap:.5rem;align-items:center">
      <a class="btn ghost ${page <= 1 ? 'disabled' : ''}" href="#/leaderboards?page=${page - 1}${season != null ? `&season=${season}` : ''}" ${page <= 1 ? 'aria-disabled="true"' : ''}>‹</a>
      <span>${esc(t('lb_page'))} ${page} / ${pages}</span>
      <a class="btn ghost ${page >= pages ? 'disabled' : ''}" href="#/leaderboards?page=${page + 1}${season != null ? `&season=${season}` : ''}" ${page >= pages ? 'aria-disabled="true"' : ''}>›</a>
    </nav>
  </section>`;
}

export async function renderMatch(id) {
  let match = MATCH_DEMO;
  try { match = await api.match(id || 'demo'); } catch { /* demo */ }
  const landings = match.landings || MATCH_DEMO.landings;
  const deaths = match.deaths || MATCH_DEMO.deaths;
  const feed = match.killFeed || MATCH_DEMO.killFeed;
  const landingMarks = landings.map((p) => {
    const [sx, sy] = worldToSvg(p.x, p.z);
    return `<g><circle cx="${sx}" cy="${sy}" r="7" fill="#3d7eff"/><title>İniş: ${esc(p.name)}</title></g>`;
  }).join('');
  const deathMarks = deaths.map((p) => {
    const [sx, sy] = worldToSvg(p.x, p.z);
    return `<g><circle cx="${sx}" cy="${sy}" r="6" fill="#e30a17"/><title>Ölüm: ${esc(p.name)}</title></g>`;
  }).join('');
  const teams = (match.teams || []).slice().sort((a, b) => a.placement - b.placement);
  return `
  <section class="section">
    <h2>${esc(t('match_title'))} — ${esc(match.id)}</h2>
    <p class="muted">${esc(match.region)} · ${esc(match.status)} · Sezon ${match.seasonNumber}</p>
    <div class="grid cols-2">
      <article class="card">
        <h3>${esc(t('match_teams'))}</h3>
        <ol>${teams.map((tm) => `<li><strong>${esc(tm.squadName)}</strong> — #${tm.placement} (${tm.playerIds?.length || 0} oyuncu + ${tm.botCount} bot)</li>`).join('')}</ol>
      </article>
      <article class="card">
        <h3>${esc(t('match_feed'))}</h3>
        <ul>${feed.map((k) => `<li><span class="muted">${esc(k.t)}</span> ${esc(k.killer)} → ${esc(k.victim)} (${esc(k.weapon)}${k.headshot ? ' kafa' : ''})</li>`).join('')}</ul>
      </article>
    </div>
    <article class="card" style="margin-top:1rem">
      <h3>${esc(t('match_map'))}</h3>
      <p class="muted"><span class="pill" style="background:rgba(61,126,255,.2);color:#3d7eff">İniş</span>
         <span class="pill bad">Ölüm</span> · Kuzgun Vadisi SVG</p>
      <svg class="map-svg" viewBox="0 0 1000 1000" role="img" aria-label="Maç haritası iniş ve ölüm noktaları">
        <rect width="1000" height="1000" fill="#0e1520"/>
        ${mapGrid()}
        <path d="M520 0 C510 120 480 250 500 400 S530 700 510 1000" fill="none" stroke="#3d7eff" stroke-width="8" opacity=".35"/>
        ${landingMarks}${deathMarks}
      </svg>
    </article>
  </section>`;
}

export async function renderAchievements() {
  let items = [];
  try { items = await api.achievements(); } catch { items = []; }
  const cards = items.map((a) => {
    const pct = Math.min(100, Math.round((a.progress / a.target) * 100));
    return `
    <article class="card ${a.unlocked ? 'ach-unlocked' : ''}">
      <h3>${esc(a.title)} ${a.unlocked ? '<span class="pill ok">✓</span>' : ''}</h3>
      <p>${esc(a.description)}</p>
      <div class="xp-bar" style="margin-top:.75rem" role="progressbar" aria-valuenow="${pct}" aria-valuemin="0" aria-valuemax="100"><i style="--p:${pct}%"></i></div>
      <p class="muted" style="margin-top:.35rem">${a.progress} / ${a.target}</p>
    </article>`;
  }).join('');
  return `<section class="section"><h2>${esc(t('ach_title'))}</h2><div class="grid cols-3">${cards || '<p class="muted">Giriş gerekli.</p>'}</div></section>`;
}

export async function renderAdmin() {
  const me = api.currentPlayer();
  if (!api.canModerate(me)) {
    return `<section class="section card"><h2>${esc(t('admin_title'))}</h2><p class="muted">Yetki gerekli (Admin / Moderator). Mock: <code>admin</code> ile giriş yap.</p></section>`;
  }
  const [servers, suspects, reports, health, tel, perf] = await Promise.all([
    api.servers().catch(() => []),
    api.suspects(50).catch(() => []),
    api.reports().catch(() => []),
    api.health().catch(() => ({ status: 'down' })),
    api.telemetryHealth().catch(() => ({ status: 'down' })),
    api.telemetryPerf().catch(() => ({})),
  ]);
  const serverRows = (Array.isArray(servers) ? servers : servers.items || []).map((s) => `
    <tr>
      <td>${esc(s.id)}</td>
      <td>${esc(s.region)}</td>
      <td>${s.currentPlayers ?? s.players ?? 0}/${s.maxPlayers ?? s.capacity ?? 60}</td>
      <td>${esc(s.activeMatch || s.match || '—')}</td>
      <td>${s.port ?? (String(s.endpoint || '').split(':')[1] || '—')}</td>
      <td>CPU ${s.cpu ?? '—'}% · RAM ${s.ram ?? '—'}%</td>
      <td><span class="pill">${esc(s.status)}</span></td>
    </tr>`).join('');
  return `
  <section class="section">
    <h2>${esc(t('admin_title'))}</h2>
    <p class="muted">Rol: ${esc(me.role)}</p>
    <div class="admin-tabs" role="tablist">
      <a href="#admin-servers" class="pill">${esc(t('admin_servers'))}</a>
      <a href="#admin-reports" class="pill">${esc(t('admin_reports'))}</a>
      <a href="#admin-suspects" class="pill">${esc(t('admin_suspects'))}</a>
      <a href="#admin-health" class="pill">${esc(t('admin_health'))}</a>
    </div>
    <div class="grid cols-2" style="margin-top:1rem">
      <article class="card" id="admin-servers">
        <h3>${esc(t('admin_servers'))}</h3>
        <div class="table-wrap">
          <table>
            <thead><tr><th>ID</th><th>Bölge</th><th>Oyuncu</th><th>Maç</th><th>Port</th><th>CPU/RAM</th><th>Durum</th></tr></thead>
            <tbody>${serverRows}</tbody>
          </table>
        </div>
      </article>
      <article class="card" id="admin-health">
        <h3>${esc(t('admin_health'))}</h3>
        <ul>
          <li>API: <span class="pill ${health.status === 'Healthy' || health.ok ? 'ok' : 'warn'}">${esc(health.status || '—')}</span></li>
          <li>Telemetry: <span class="pill">${esc(tel.status || '—')}</span></li>
          <li>Ingest/s: ${perf.ingestPerSec ?? '—'} · Queue: ${perf.queueDepth ?? '—'} · ${perf.queueMs ?? '—'} ms</li>
        </ul>
        <h3 style="margin-top:1rem">${esc(t('admin_search'))}</h3>
        <form class="form" id="adminSearchForm">
          <label>Kullanıcı<input name="username" required /></label>
          <button class="btn ghost" type="submit">Ara</button>
          <p class="muted" id="adminSearchMsg"></p>
        </form>
        <form class="form" id="banForm" style="margin-top:1rem">
          <label>Oyuncu ID<input name="playerId" required /></label>
          <label>Sebep<input name="reason" required /></label>
          <label>Süre (saat)<input name="durationHours" type="number" min="1" placeholder="kalıcı için boş" /></label>
          <div style="display:flex;gap:.5rem">
            <button class="btn primary" type="submit" data-action="ban">${esc(t('admin_ban'))}</button>
            <button class="btn ghost" type="submit" data-action="mute">${esc(t('admin_mute'))}</button>
          </div>
          <p class="muted" id="banMsg" role="status"></p>
        </form>
      </article>
      <article class="card" id="admin-reports">
        <h3>${esc(t('admin_reports'))}</h3>
        <ul>${(reports || []).map((r) => `<li><strong>${esc(r.reportedPlayerId)}</strong> — ${esc(r.reason)} <span class="muted">${esc(r.status || '')}</span></li>`).join('') || '<li class="muted">Boş</li>'}</ul>
      </article>
      <article class="card" id="admin-suspects">
        <h3>${esc(t('admin_suspects'))}</h3>
        <ul>${(suspects || []).map((s) => `<li><strong>${esc(s.playerId)}</strong> skor ${s.totalScore ?? s.score}: ${esc(s.findings?.[0]?.detail || s.reason || '')}</li>`).join('')}</ul>
      </article>
    </div>
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
  return `<section class="section"><h2>${esc(t('nav_arsenal'))}</h2><div class="grid cols-3">${cards}</div></section>`;
}

export async function renderMap() {
  const marks = LOCATIONS.map((l) => {
    const [sx, sy] = worldToSvg(l.x, l.z);
    return `<g><circle cx="${sx}" cy="${sy}" r="10" fill="#e30a17" opacity=".85"/><text x="${sx + 14}" y="${sy + 4}" fill="#e8eef5" font-size="14" font-family="Oswald,sans-serif">${esc(l.name)}</text></g>`;
  }).join('');
  return `
  <section class="section">
    <h2>Kuzgun Vadisi</h2>
    <p class="muted">1024×1024 m · grid 100 m · A–J / 1–10 · kuzey = +Z</p>
    <svg class="map-svg" viewBox="0 0 1000 1000" role="img" aria-label="Kuzgun Vadisi paftası">
      <rect width="1000" height="1000" fill="#0e1520"/>
      ${mapGrid()}
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
    <h2>${esc(t('nav_patches'))}</h2>
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
  let posts = [];
  try {
    const res = await fetch('content/news/index.json', { cache: 'no-cache' });
    if (res.ok) posts = await res.json();
  } catch { /* fallback */ }
  if (!posts.length) posts = await api.news();
  return `
  <section class="section">
    <h2>${esc(t('news_title'))}</h2>
    <div class="grid cols-2">
      ${posts.map((n) => `
        <article class="card">
          <h3><a href="#/news/${encodeURIComponent(n.slug || n.id)}">${esc(n.title)}</a></h3>
          <p class="muted">${esc(n.date || '')}</p>
          <p>${esc(n.body || n.summary || '')}</p>
        </article>`).join('')}
    </div>
  </section>`;
}

export async function renderNewsPost(slug) {
  let post = null;
  try {
    const res = await fetch(`content/news/${slug}.json`, { cache: 'no-cache' });
    if (res.ok) post = await res.json();
  } catch { /* ignore */ }
  if (!post) {
    const all = await api.news();
    post = all.find((n) => n.slug === slug || n.id === slug);
  }
  if (!post) return renderNotFound();
  return `
  <section class="section card" style="max-width:720px">
    <p><a href="#/news">← ${esc(t('news_title'))}</a></p>
    <h2>${esc(post.title)}</h2>
    <p class="muted">${esc(post.date || '')}</p>
    <div class="prose">${post.html || `<p>${esc(post.body || '')}</p>`}</div>
  </section>`;
}

export async function renderSeason() {
  const season = await api.activeSeason().catch(() => ({ number: 1, name: 'Vadi Tatbikatı', isActive: true }));
  return `
  <section class="section grid cols-2">
    <article class="card">
      <h2>Sezon ${season.number} — ${esc(season.name)}</h2>
      <p class="muted">${season.isActive ? 'Aktif' : 'Kapalı'} · <a href="#/archive">${esc(t('nav_archive'))}</a></p>
      <ul>
        <li>İlk Zafer</li><li>Keskin Nişancı (100 kafa)</li>
        <li>Komuta Devri</li><li>Kirpi Konvoyu</li><li>Baraj Baskını</li>
      </ul>
      <p><a class="btn ghost" href="#/achievements">${esc(t('nav_achievements'))}</a></p>
    </article>
    <article class="card">
      <h2>Rütbe Nişanları</h2>
      <div class="grid cols-3">
        ${RANKS.slice(0, 12).map((r, i) => `
          <div class="rank-badge"><img src="assets/ranks/rank-${i}.svg" width="24" height="24" alt="" /> ${esc(r)}</div>
        `).join('')}
      </div>
    </article>
  </section>`;
}

export async function renderArchive(query = {}) {
  const number = Number(query.season ?? 0);
  const rows = await api.seasonArchive(number).catch(() => []);
  return `
  <section class="section">
    <h2>${esc(t('archive_title'))}</h2>
    <form class="form inline-form" id="archiveForm">
      <label>Sezon<input name="season" type="number" min="0" value="${number}" /></label>
      <button class="btn ghost" type="submit">Yükle</button>
    </form>
    <div class="table-wrap" style="margin-top:1rem">
      <table>
        <thead><tr><th>#</th><th>Oyuncu</th><th>XP</th><th>Rozet</th></tr></thead>
        <tbody>${(rows || []).map((r) => `
          <tr><td>${r.placement}</td><td>${esc(r.username)}</td><td>${formatNumber(r.seasonXp, CONFIG.lang)}</td><td>${esc(r.rewardBadge)}</td></tr>
        `).join('')}</tbody>
      </table>
    </div>
  </section>`;
}

export async function renderCompare(query = {}) {
  const a = query.a || 'Kartal_07';
  const b = query.b || 'Bozkurt';
  const [pa, pb] = await Promise.all([
    api.player(a).catch(() => null),
    api.player(b).catch(() => null),
  ]);
  const cell = (p) => p ? `
    <article class="card">
      <h3>${esc(p.username)}</h3>
      <p class="rank-badge"><img src="assets/ranks/rank-${p.rank}.svg" width="24" height="24" alt="" /> ${esc(api.rankName(p.rank))}</p>
      <ul>
        <li>XP: ${formatNumber(p.stats?.experience, CONFIG.lang)}</li>
        <li>Elo: ${p.eloRating}</li>
        <li>Kills: ${p.stats?.kills}</li>
        <li>Wins: ${p.stats?.wins}</li>
        <li>HS: ${p.stats?.headshots}</li>
      </ul>
    </article>` : '<article class="card"><p class="muted">Bulunamadı</p></article>';
  return `
  <section class="section">
    <h2>${esc(t('compare_title'))}</h2>
    <form class="form inline-form" id="compareForm">
      <label>A<input name="a" value="${esc(a)}" required /></label>
      <label>B<input name="b" value="${esc(b)}" required /></label>
      <button class="btn primary" type="submit">Karşılaştır</button>
    </form>
    <div class="grid cols-2" style="margin-top:1rem">${cell(pa)}${cell(pb)}</div>
  </section>`;
}

export async function renderPlayer(name) {
  let p;
  try { p = await api.player(name); } catch {
    p = { username: name, rank: 0, stats: {}, eloRating: 0, seasonXp: 0 };
  }
  const weapon = WEAPONS.find((w) => w.id === (p.bestWeaponId || 'ar_mpt76'));
  return `
  <section class="section card">
    <h2>Oyuncu Kartı — ${esc(p.username)}</h2>
    <p class="rank-badge"><img src="assets/ranks/rank-${p.rank || 0}.svg" width="32" height="32" alt="" /> ${esc(api.rankName(p.rank || 0))}</p>
    <div class="grid cols-3" style="margin-top:1rem">
      <div><strong>${formatNumber(p.stats?.experience ?? p.seasonXp, CONFIG.lang)}</strong><div class="muted">XP</div></div>
      <div><strong>${p.stats?.kills ?? 0}</strong><div class="muted">Öldürme</div></div>
      <div><strong>${p.stats?.wins ?? 0}</strong><div class="muted">Zafer</div></div>
    </div>
    <p style="margin-top:1rem">${esc(t('profile_best_weapon'))}: <strong>${esc(weapon?.name || 'MPT-76')}</strong></p>
    <p>
      <a class="btn ghost" href="#/match/demo">Örnek maç</a>
      <a class="btn ghost" href="#/compare?a=${encodeURIComponent(p.username)}&b=Bozkurt">${esc(t('nav_compare'))}</a>
    </p>
  </section>`;
}

export async function renderNotFound() {
  return `<section class="section card"><h2>${esc(t('not_found'))}</h2><p><a href="#/">Ana sayfaya dön</a></p></section>`;
}
