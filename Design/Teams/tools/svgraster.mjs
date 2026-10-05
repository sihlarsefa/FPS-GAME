// HAREKÂT — Bağımlılıksız SVG → RGBA tarayıcı (rasterizer).
//
// Amaç: Design/Teams altındaki amblem / rütbe / kol bandı SVG'lerini, sistemde rsvg-convert,
// Inkscape, ImageMagick veya tarayıcı olmasa bile (ör. çıplak Windows Server) PNG'ye çevirmek.
// Yalnızca node:* modülleri kullanılır; npm paketi yoktur.
//
// Desteklenen "HAREKÂT amblem profili" (validate.mjs bu profili denetler):
//   Öğeler : svg, g, path, rect, circle, ellipse, line, polyline, polygon, use, defs, clipPath,
//            title, desc, metadata (son üçü yok sayılır)
//   Boya   : fill, stroke (#rgb/#rgba/#rrggbb/#rrggbbaa, rgb()/rgba(), temel adlar, none, currentColor)
//   Çizgi  : stroke-width, stroke-linecap (butt/round/square), stroke-linejoin (miter/round/bevel),
//            stroke-miterlimit
//   Diğer  : opacity (grup katmanı ile doğru), fill-opacity, stroke-opacity, fill-rule, clip-rule,
//            clip-path="url(#id)", transform (matrix/translate/scale/rotate/skewX/skewY),
//            display="none", visibility, style="..." içindeki aynı özellikler,
//            viewBox + preserveAspectRatio
//   YOK    : gradyan / desen (url() dolgusu), text, filter, mask, stroke-dasharray, marker, image,
//            CSS <style> blokları, yüzde uzunluklar.
//
// Kenar yumuşatma: her piksel satırı SS alt satırda örneklenir, yatayda tam (analitik) kapsama.
// Şekiller Skia/Chrome gibi "kapsama × renk" ile tek tek bindirilir (src-over, ön-çarpımlı).

const SS = 16; // dikey alt örnek sayısı
const FLAT_TOL = 0.05; // eğri düzleştirme toleransı (cihaz pikseli)

// ------------------------------------------------------------------ XML

const ENTITIES = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'" };

function decodeEntities(s) {
  return s.replace(/&(#x[0-9a-fA-F]+|#[0-9]+|[a-zA-Z]+);/g, (m, e) => {
    if (e[0] === '#') {
      const code = e[1] === 'x' || e[1] === 'X' ? parseInt(e.slice(2), 16) : parseInt(e.slice(1), 10);
      return Number.isFinite(code) ? String.fromCodePoint(code) : m;
    }
    return ENTITIES[e] ?? m;
  });
}

/** Küçük, hoşgörülü XML ayrıştırıcı (SVG için yeterli). */
export function parseXml(src) {
  const root = { name: '#root', attrs: {}, children: [], text: '', parent: null };
  let cur = root;
  let i = 0;
  const n = src.length;
  while (i < n) {
    const lt = src.indexOf('<', i);
    if (lt < 0) {
      cur.text += decodeEntities(src.slice(i));
      break;
    }
    if (lt > i) cur.text += decodeEntities(src.slice(i, lt));
    if (src.startsWith('<!--', lt)) {
      const e = src.indexOf('-->', lt + 4);
      if (e < 0) throw new Error('XML: kapanmamış yorum');
      i = e + 3;
      continue;
    }
    if (src.startsWith('<![CDATA[', lt)) {
      const e = src.indexOf(']]>', lt);
      if (e < 0) throw new Error('XML: kapanmamış CDATA');
      cur.text += src.slice(lt + 9, e);
      i = e + 3;
      continue;
    }
    if (src.startsWith('<?', lt)) {
      const e = src.indexOf('?>', lt);
      if (e < 0) throw new Error('XML: kapanmamış işlem talimatı');
      i = e + 2;
      continue;
    }
    if (src.startsWith('<!', lt)) {
      const e = src.indexOf('>', lt);
      if (e < 0) throw new Error('XML: kapanmamış bildirim');
      i = e + 1;
      continue;
    }
    if (src[lt + 1] === '/') {
      const gt = src.indexOf('>', lt);
      if (gt < 0) throw new Error('XML: kapanmamış bitiş etiketi');
      const name = src.slice(lt + 2, gt).trim();
      if (cur.name !== name) throw new Error(`XML: beklenen </${cur.name}>, bulunan </${name}>`);
      cur = cur.parent;
      i = gt + 1;
      continue;
    }
    let j = lt + 1;
    while (j < n && !/[\s/>]/.test(src[j])) j++;
    const el = { name: src.slice(lt + 1, j), attrs: {}, children: [], text: '', parent: cur };
    let selfClose = false;
    for (;;) {
      while (j < n && /\s/.test(src[j])) j++;
      if (j >= n) throw new Error('XML: beklenmeyen dosya sonu');
      if (src[j] === '/' && src[j + 1] === '>') {
        selfClose = true;
        j += 2;
        break;
      }
      if (src[j] === '>') {
        j++;
        break;
      }
      let k = j;
      while (k < n && !/[\s=/>]/.test(src[k])) k++;
      const an = src.slice(j, k);
      while (k < n && /\s/.test(src[k])) k++;
      if (src[k] !== '=') throw new Error(`XML: '${an}' özniteliğinde '=' yok`);
      k++;
      while (k < n && /\s/.test(src[k])) k++;
      const q = src[k];
      if (q !== '"' && q !== "'") throw new Error(`XML: '${an}' değeri tırnaksız`);
      const e = src.indexOf(q, k + 1);
      if (e < 0) throw new Error('XML: kapanmamış tırnak');
      el.attrs[an] = decodeEntities(src.slice(k + 1, e));
      j = e + 1;
    }
    cur.children.push(el);
    if (!selfClose) cur = el;
    i = j;
  }
  if (cur !== root) throw new Error(`XML: kapanmamış <${cur.name}>`);
  const svg = root.children.find((c) => c.name === 'svg');
  if (!svg) throw new Error('SVG kök öğesi yok');
  return svg;
}

// ------------------------------------------------------------------ Renk / stil

const NAMED = {
  black: [0, 0, 0], white: [255, 255, 255], red: [255, 0, 0], green: [0, 128, 0], blue: [0, 0, 255],
  yellow: [255, 255, 0], gray: [128, 128, 128], grey: [128, 128, 128], silver: [192, 192, 192],
  orange: [255, 165, 0], navy: [0, 0, 128], maroon: [128, 0, 0], olive: [128, 128, 0], teal: [0, 128, 128],
  purple: [128, 0, 128], lime: [0, 255, 0], aqua: [0, 255, 255], fuchsia: [255, 0, 255],
};

/** "#rrggbb" vb. → {r,g,b,a} (0..1) | null (none) | {url} */
export function parseColor(v, current) {
  if (v == null) return null;
  v = String(v).trim();
  if (v === '' || v === 'none' || v === 'transparent') return null;
  if (v === 'currentColor') return current ? parseColor(current, null) : { r: 0, g: 0, b: 0, a: 1 };
  if (v.startsWith('url(')) return { url: v };
  if (v[0] === '#') {
    let h = v.slice(1);
    if (h.length === 3 || h.length === 4) h = [...h].map((c) => c + c).join('');
    if (!/^[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$/.test(h)) throw new Error(`Geçersiz renk ${v}`);
    const r = parseInt(h.slice(0, 2), 16);
    const g = parseInt(h.slice(2, 4), 16);
    const b = parseInt(h.slice(4, 6), 16);
    const a = h.length === 8 ? parseInt(h.slice(6, 8), 16) / 255 : 1;
    return { r: r / 255, g: g / 255, b: b / 255, a };
  }
  const m = /^rgba?\(([^)]*)\)$/i.exec(v);
  if (m) {
    const p = m[1].split(/[\s,/]+/).filter(Boolean);
    const ch = (s) => (s.endsWith('%') ? (parseFloat(s) / 100) * 255 : parseFloat(s));
    const a = p[3] == null ? 1 : p[3].endsWith('%') ? parseFloat(p[3]) / 100 : parseFloat(p[3]);
    return { r: ch(p[0]) / 255, g: ch(p[1]) / 255, b: ch(p[2]) / 255, a };
  }
  const nm = NAMED[v.toLowerCase()];
  if (nm) return { r: nm[0] / 255, g: nm[1] / 255, b: nm[2] / 255, a: 1 };
  throw new Error(`Bilinmeyen renk ${v}`);
}

const INHERITED = [
  'fill', 'stroke', 'stroke-width', 'stroke-linecap', 'stroke-linejoin', 'stroke-miterlimit',
  'fill-opacity', 'stroke-opacity', 'fill-rule', 'clip-rule', 'visibility', 'color',
];
const LOCAL = ['opacity', 'clip-path', 'display'];
export const STYLE_PROPS = new Set([...INHERITED, ...LOCAL]);

const INITIAL_STYLE = {
  fill: '#000000', stroke: 'none', 'stroke-width': '1', 'stroke-linecap': 'butt', 'stroke-linejoin': 'miter',
  'stroke-miterlimit': '4', 'fill-opacity': '1', 'stroke-opacity': '1', 'fill-rule': 'nonzero',
  'clip-rule': 'nonzero', visibility: 'visible', color: '#000000',
};

function parseStyleAttr(s) {
  const out = {};
  if (!s) return out;
  for (const decl of s.split(';')) {
    const c = decl.indexOf(':');
    if (c < 0) continue;
    out[decl.slice(0, c).trim()] = decl.slice(c + 1).trim();
  }
  return out;
}

function computeStyle(el, parentStyle) {
  const st = {};
  for (const k of INHERITED) st[k] = parentStyle[k];
  for (const k of LOCAL) st[k] = undefined;
  const css = parseStyleAttr(el.attrs.style);
  for (const k of STYLE_PROPS) {
    let v = css[k] ?? el.attrs[k];
    if (v == null) continue;
    v = String(v).trim();
    if (v === 'inherit') continue;
    st[k] = v;
  }
  return st;
}

function num(v, def = 0) {
  if (v == null || v === '') return def;
  const s = String(v).trim();
  if (s.endsWith('%')) throw new Error(`Yüzde uzunluk desteklenmiyor: ${s}`);
  const f = parseFloat(s);
  return Number.isFinite(f) ? f : def;
}

// ------------------------------------------------------------------ Dönüşümler

const IDENT = [1, 0, 0, 1, 0, 0];

function mul(m, n) {
  return [
    m[0] * n[0] + m[2] * n[1],
    m[1] * n[0] + m[3] * n[1],
    m[0] * n[2] + m[2] * n[3],
    m[1] * n[2] + m[3] * n[3],
    m[0] * n[4] + m[2] * n[5] + m[4],
    m[1] * n[4] + m[3] * n[5] + m[5],
  ];
}

export function parseTransform(s) {
  let m = IDENT;
  if (!s) return m;
  const re = /(matrix|translate|scale|rotate|skewX|skewY)\s*\(([^)]*)\)/g;
  let r;
  let consumed = '';
  while ((r = re.exec(s))) {
    consumed += r[0];
    const a = r[2].split(/[\s,]+/).filter(Boolean).map(Number);
    let t;
    switch (r[1]) {
      case 'matrix': t = a.slice(0, 6); break;
      case 'translate': t = [1, 0, 0, 1, a[0] || 0, a[1] || 0]; break;
      case 'scale': t = [a[0], 0, 0, a.length > 1 ? a[1] : a[0], 0, 0]; break;
      case 'rotate': {
        const ang = (a[0] * Math.PI) / 180;
        const c = Math.cos(ang);
        const sn = Math.sin(ang);
        t = [c, sn, -sn, c, 0, 0];
        if (a.length >= 3) t = mul(mul([1, 0, 0, 1, a[1], a[2]], t), [1, 0, 0, 1, -a[1], -a[2]]);
        break;
      }
      case 'skewX': t = [1, 0, Math.tan((a[0] * Math.PI) / 180), 1, 0, 0]; break;
      case 'skewY': t = [1, Math.tan((a[0] * Math.PI) / 180), 0, 1, 0, 0]; break;
      default: t = IDENT;
    }
    if (t.some((v) => !Number.isFinite(v))) throw new Error(`Geçersiz transform: ${r[0]}`);
    m = mul(m, t);
  }
  if (!consumed && s.trim()) throw new Error(`Çözülemeyen transform: ${s}`);
  return m;
}

const ap = (m, x, y) => [m[0] * x + m[2] * y + m[4], m[1] * x + m[3] * y + m[5]];
const maxScale = (m) => Math.sqrt(Math.max(m[0] * m[0] + m[1] * m[1], m[2] * m[2] + m[3] * m[3]));
const detScale = (m) => Math.sqrt(Math.abs(m[0] * m[3] - m[1] * m[2]));

// ------------------------------------------------------------------ Yol (path) → cihaz uzayında çokgenler

function tokenizePath(d) {
  const out = [];
  let i = 0;
  const n = d.length;
  const isNumStart = (c) => /[0-9.+-]/.test(c);
  while (i < n) {
    const c = d[i];
    if (/[\s,]/.test(c)) { i++; continue; }
    if (/[MmLlHhVvCcSsQqTtAaZz]/.test(c)) { out.push(c); i++; continue; }
    if (isNumStart(c)) {
      const re = /[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?/y;
      re.lastIndex = i;
      const m = re.exec(d);
      if (!m) throw new Error(`Yol verisi çözülemedi: '${d.slice(i, i + 12)}'`);
      out.push(m[0]);
      i = re.lastIndex;
      continue;
    }
    throw new Error(`Yol verisinde beklenmeyen karakter '${c}'`);
  }
  return out;
}

/** Yay bayrakları ("0"/"1") boşluksuz yazılabildiği için ayrı okunur. */
function splitArcFlags(tokens) {
  const out = [];
  let cmd = null;
  let argIdx = 0;
  for (let i = 0; i < tokens.length; i++) {
    const t = tokens[i];
    if (/^[A-Za-z]$/.test(t)) { cmd = t; argIdx = 0; out.push(t); continue; }
    if (cmd === 'A' || cmd === 'a') {
      const slot = argIdx % 7;
      if ((slot === 3 || slot === 4) && t.length > 1 && (t[0] === '0' || t[0] === '1')) {
        // "01" veya "110.5" gibi birleşik bayrak + sayı
        out.push(t[0]);
        argIdx++;
        tokens.splice(i + 1, 0, t.slice(1));
        continue;
      }
    }
    out.push(t);
    argIdx++;
  }
  return out;
}

class PathBuilder {
  constructor(ctm) {
    this.m = ctm;
    this.subpaths = [];
    this.cur = null;
    this.scale = maxScale(ctm) || 1;
  }
  moveTo(x, y) {
    const [dx, dy] = ap(this.m, x, y);
    this.cur = { pts: [dx, dy], closed: false };
    this.subpaths.push(this.cur);
  }
  lineTo(x, y) {
    if (!this.cur) this.moveTo(x, y);
    const [dx, dy] = ap(this.m, x, y);
    this.cur.pts.push(dx, dy);
  }
  close() {
    if (this.cur) this.cur.closed = true;
  }
  cubic(x0, y0, x1, y1, x2, y2, x3, y3) {
    const p0 = ap(this.m, x0, y0);
    const p1 = ap(this.m, x1, y1);
    const p2 = ap(this.m, x2, y2);
    const p3 = ap(this.m, x3, y3);
    const ddx = Math.max(Math.hypot(p0[0] - 2 * p1[0] + p2[0], p0[1] - 2 * p1[1] + p2[1]),
      Math.hypot(p1[0] - 2 * p2[0] + p3[0], p1[1] - 2 * p2[1] + p3[1]));
    const n = Math.min(512, Math.max(1, Math.ceil(Math.sqrt((0.75 * ddx * 6) / (8 * FLAT_TOL) / 0.75))));
    for (let i = 1; i <= n; i++) {
      const t = i / n;
      const u = 1 - t;
      const a = u * u * u;
      const b = 3 * u * u * t;
      const c = 3 * u * t * t;
      const d = t * t * t;
      this.cur.pts.push(a * p0[0] + b * p1[0] + c * p2[0] + d * p3[0], a * p0[1] + b * p1[1] + c * p2[1] + d * p3[1]);
    }
  }
  quad(x0, y0, x1, y1, x2, y2) {
    const p0 = ap(this.m, x0, y0);
    const p1 = ap(this.m, x1, y1);
    const p2 = ap(this.m, x2, y2);
    const dd = Math.hypot(p0[0] - 2 * p1[0] + p2[0], p0[1] - 2 * p1[1] + p2[1]);
    const n = Math.min(512, Math.max(1, Math.ceil(Math.sqrt(dd / (4 * FLAT_TOL)))));
    for (let i = 1; i <= n; i++) {
      const t = i / n;
      const u = 1 - t;
      this.cur.pts.push(u * u * p0[0] + 2 * u * t * p1[0] + t * t * p2[0], u * u * p0[1] + 2 * u * t * p1[1] + t * t * p2[1]);
    }
  }
  arc(x1, y1, rx, ry, phiDeg, fa, fs, x2, y2) {
    // SVG 1.1 F.6.5 uç nokta → merkez dönüşümü
    if (x1 === x2 && y1 === y2) return;
    rx = Math.abs(rx);
    ry = Math.abs(ry);
    if (rx === 0 || ry === 0) { this.lineTo(x2, y2); return; }
    const phi = (phiDeg * Math.PI) / 180;
    const cp = Math.cos(phi);
    const sp = Math.sin(phi);
    const dx2 = (x1 - x2) / 2;
    const dy2 = (y1 - y2) / 2;
    const x1p = cp * dx2 + sp * dy2;
    const y1p = -sp * dx2 + cp * dy2;
    const lam = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry);
    if (lam > 1) { const s = Math.sqrt(lam); rx *= s; ry *= s; }
    const num = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p;
    const den = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
    let co = Math.sqrt(Math.max(0, num / den));
    if (fa === fs) co = -co;
    const cxp = (co * rx * y1p) / ry;
    const cyp = (-co * ry * x1p) / rx;
    const cx = cp * cxp - sp * cyp + (x1 + x2) / 2;
    const cy = sp * cxp + cp * cyp + (y1 + y2) / 2;
    const ang = (ux, uy, vx, vy) => {
      const a = Math.atan2(ux * vy - uy * vx, ux * vx + uy * vy);
      return a;
    };
    const t1 = ang(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
    let dt = ang((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);
    if (!fs && dt > 0) dt -= 2 * Math.PI;
    else if (fs && dt < 0) dt += 2 * Math.PI;
    const rDev = Math.max(rx, ry) * this.scale;
    const step = rDev > FLAT_TOL ? 2 * Math.acos(Math.max(-1, 1 - FLAT_TOL / rDev)) : Math.PI / 2;
    const n = Math.min(1024, Math.max(2, Math.ceil(Math.abs(dt) / step)));
    for (let i = 1; i <= n; i++) {
      const t = t1 + (dt * i) / n;
      const ex = rx * Math.cos(t);
      const ey = ry * Math.sin(t);
      const px = i === n ? x2 : cp * ex - sp * ey + cx;
      const py = i === n ? y2 : sp * ex + cp * ey + cy;
      const [ddx, ddy] = ap(this.m, px, py);
      this.cur.pts.push(ddx, ddy);
    }
  }
}

export function pathToSubpaths(d, ctm) {
  const pb = new PathBuilder(ctm);
  const tk = splitArcFlags(tokenizePath(d || ''));
  let i = 0;
  let cx = 0;
  let cy = 0;
  let sx = 0;
  let sy = 0;
  let lcx = null; // son kübik kontrol
  let lcy = null;
  let lqx = null; // son karesel kontrol
  let lqy = null;
  let cmd = null;
  const nextNum = () => {
    if (i >= tk.length || /^[A-Za-z]$/.test(tk[i])) throw new Error(`Yol verisi eksik argüman (${cmd})`);
    return parseFloat(tk[i++]);
  };
  const hasNum = () => i < tk.length && !/^[A-Za-z]$/.test(tk[i]);
  while (i < tk.length) {
    if (/^[A-Za-z]$/.test(tk[i])) cmd = tk[i++];
    else if (cmd == null) throw new Error('Yol verisi komutla başlamalı');
    const rel = cmd === cmd.toLowerCase();
    const C = cmd.toUpperCase();
    let resetC = true;
    let resetQ = true;
    switch (C) {
      case 'M': {
        let x = nextNum();
        let y = nextNum();
        if (rel) { x += cx; y += cy; }
        pb.moveTo(x, y);
        cx = sx = x;
        cy = sy = y;
        cmd = rel ? 'l' : 'L';
        break;
      }
      case 'L': {
        let x = nextNum();
        let y = nextNum();
        if (rel) { x += cx; y += cy; }
        pb.lineTo(x, y);
        cx = x;
        cy = y;
        break;
      }
      case 'H': {
        let x = nextNum();
        if (rel) x += cx;
        pb.lineTo(x, cy);
        cx = x;
        break;
      }
      case 'V': {
        let y = nextNum();
        if (rel) y += cy;
        pb.lineTo(cx, y);
        cy = y;
        break;
      }
      case 'C': case 'S': {
        let x1;
        let y1;
        if (C === 'C') {
          x1 = nextNum(); y1 = nextNum();
          if (rel) { x1 += cx; y1 += cy; }
        } else if (lcx != null) {
          x1 = 2 * cx - lcx; y1 = 2 * cy - lcy;
        } else {
          x1 = cx; y1 = cy;
        }
        let x2 = nextNum();
        let y2 = nextNum();
        let x = nextNum();
        let y = nextNum();
        if (rel) { x2 += cx; y2 += cy; x += cx; y += cy; }
        if (!pb.cur) pb.moveTo(cx, cy);
        pb.cubic(cx, cy, x1, y1, x2, y2, x, y);
        lcx = x2; lcy = y2; resetC = false;
        cx = x; cy = y;
        break;
      }
      case 'Q': case 'T': {
        let x1;
        let y1;
        if (C === 'Q') {
          x1 = nextNum(); y1 = nextNum();
          if (rel) { x1 += cx; y1 += cy; }
        } else if (lqx != null) {
          x1 = 2 * cx - lqx; y1 = 2 * cy - lqy;
        } else {
          x1 = cx; y1 = cy;
        }
        let x = nextNum();
        let y = nextNum();
        if (rel) { x += cx; y += cy; }
        if (!pb.cur) pb.moveTo(cx, cy);
        pb.quad(cx, cy, x1, y1, x, y);
        lqx = x1; lqy = y1; resetQ = false;
        cx = x; cy = y;
        break;
      }
      case 'A': {
        const rx = nextNum();
        const ry = nextNum();
        const rot = nextNum();
        const fa = nextNum() !== 0;
        const fs = nextNum() !== 0;
        let x = nextNum();
        let y = nextNum();
        if (rel) { x += cx; y += cy; }
        if (!pb.cur) pb.moveTo(cx, cy);
        pb.arc(cx, cy, rx, ry, rot, fa, fs, x, y);
        cx = x; cy = y;
        break;
      }
      case 'Z': {
        pb.close();
        cx = sx;
        cy = sy;
        // Z sonrası yeni çizim aynı başlangıçtan yeni alt yol açar
        if (hasNum()) throw new Error('Z sonrasında argüman olamaz');
        pb.cur = null;
        const startPt = ap(ctm, sx, sy);
        pb.pendingStart = startPt;
        break;
      }
      default:
        throw new Error(`Bilinmeyen yol komutu ${cmd}`);
    }
    if (resetC) { lcx = null; lcy = null; }
    if (resetQ) { lqx = null; lqy = null; }
    if (C === 'Z') {
      // Z'den sonra M gelmeden çizim gelirse alt yol (sx,sy)'den başlar
      if (i < tk.length && !/^[Mm]$/.test(tk[i])) pb.moveTo(sx, sy);
    }
  }
  return pb.subpaths;
}

function shapeToPathData(el) {
  const a = el.attrs;
  switch (el.name) {
    case 'path': return a.d || '';
    case 'rect': {
      const x = num(a.x);
      const y = num(a.y);
      const w = num(a.width);
      const h = num(a.height);
      if (w <= 0 || h <= 0) return '';
      let rx = a.rx != null ? num(a.rx) : null;
      let ry = a.ry != null ? num(a.ry) : null;
      if (rx == null && ry == null) { rx = 0; ry = 0; } else if (rx == null) rx = ry; else if (ry == null) ry = rx;
      rx = Math.min(Math.max(rx, 0), w / 2);
      ry = Math.min(Math.max(ry, 0), h / 2);
      if (rx === 0 || ry === 0) return `M${x} ${y}H${x + w}V${y + h}H${x}Z`;
      return `M${x + rx} ${y}H${x + w - rx}A${rx} ${ry} 0 0 1 ${x + w} ${y + ry}V${y + h - ry}` +
        `A${rx} ${ry} 0 0 1 ${x + w - rx} ${y + h}H${x + rx}A${rx} ${ry} 0 0 1 ${x} ${y + h - ry}` +
        `V${y + ry}A${rx} ${ry} 0 0 1 ${x + rx} ${y}Z`;
    }
    case 'circle': {
      const cx = num(a.cx);
      const cy = num(a.cy);
      const r = num(a.r);
      if (r <= 0) return '';
      return `M${cx - r} ${cy}A${r} ${r} 0 1 0 ${cx + r} ${cy}A${r} ${r} 0 1 0 ${cx - r} ${cy}Z`;
    }
    case 'ellipse': {
      const cx = num(a.cx);
      const cy = num(a.cy);
      const rx = num(a.rx);
      const ry = num(a.ry);
      if (rx <= 0 || ry <= 0) return '';
      return `M${cx - rx} ${cy}A${rx} ${ry} 0 1 0 ${cx + rx} ${cy}A${rx} ${ry} 0 1 0 ${cx - rx} ${cy}Z`;
    }
    case 'line':
      return `M${num(a.x1)} ${num(a.y1)}L${num(a.x2)} ${num(a.y2)}`;
    case 'polyline':
    case 'polygon': {
      const p = (a.points || '').trim().split(/[\s,]+/).filter(Boolean).map(Number);
      if (p.length < 4) return '';
      let s = `M${p[0]} ${p[1]}`;
      for (let k = 2; k + 1 < p.length; k += 2) s += `L${p[k]} ${p[k + 1]}`;
      return el.name === 'polygon' ? s + 'Z' : s;
    }
    default:
      return '';
  }
}

// ------------------------------------------------------------------ Kapsama tarayıcı

/**
 * Kapalı çokgenleri (Float64 dizileri [x0,y0,x1,y1,...]) tarar; her piksel satırı için
 * onRow(y, cov, x0, x1) çağrılır (cov[x] ∈ [0,1]).
 */
function scanFill(contours, rule, W, H, onRow) {
  const edges = [];
  let minY = Infinity;
  let maxY = -Infinity;
  let minX = Infinity;
  let maxX = -Infinity;
  for (const p of contours) {
    const n = p.length >> 1;
    if (n < 3) continue;
    for (let k = 0; k < n; k++) {
      const x0 = p[2 * k];
      const y0 = p[2 * k + 1];
      const k1 = (k + 1) % n;
      const x1 = p[2 * k1];
      const y1 = p[2 * k1 + 1];
      if (x0 < minX) minX = x0;
      if (x0 > maxX) maxX = x0;
      if (y0 < minY) minY = y0;
      if (y0 > maxY) maxY = y0;
      if (y0 === y1) continue;
      if (y0 < y1) edges.push({ ya: y0, yb: y1, xa: x0, k: (x1 - x0) / (y1 - y0), dir: 1 });
      else edges.push({ ya: y1, yb: y0, xa: x1, k: (x0 - x1) / (y0 - y1), dir: -1 });
    }
  }
  if (!edges.length) return;
  edges.sort((a, b) => a.ya - b.ya);
  const yStart = Math.max(0, Math.floor(minY));
  const yEnd = Math.min(H - 1, Math.ceil(maxY));
  const xStart = Math.max(0, Math.floor(minX));
  const xEnd = Math.min(W - 1, Math.ceil(maxX));
  if (yStart > yEnd || xStart > xEnd) return;
  const acc = new Float32Array(W + 2);
  const diff = new Float32Array(W + 2);
  const wgt = 1 / SS;
  let active = [];
  let ei = 0;
  const xs = [];
  const evenOdd = rule === 'evenodd';
  for (let py = yStart; py <= yEnd; py++) {
    acc.fill(0, xStart, xEnd + 2);
    diff.fill(0, xStart, xEnd + 2);
    let any = false;
    for (let s = 0; s < SS; s++) {
      const yc = py + (s + 0.5) / SS;
      while (ei < edges.length && edges[ei].ya <= yc) active.push(edges[ei++]);
      let w = 0;
      for (let q = 0; q < active.length; q++) if (active[q].yb > yc) active[w++] = active[q];
      active.length = w;
      if (!active.length) continue;
      xs.length = 0;
      for (const e of active) xs.push({ x: e.xa + (yc - e.ya) * e.k, d: e.dir });
      xs.sort((a, b) => a.x - b.x);
      let wind = 0;
      let start = 0;
      for (const c of xs) {
        const was = evenOdd ? (wind & 1) !== 0 : wind !== 0;
        wind += c.d;
        const now = evenOdd ? (wind & 1) !== 0 : wind !== 0;
        if (!was && now) start = c.x;
        else if (was && !now) {
          let x0 = Math.max(start, 0);
          const x1 = Math.min(c.x, W);
          if (x1 <= x0) continue;
          any = true;
          const i0 = Math.floor(x0);
          const i1 = Math.floor(x1);
          if (i0 === i1) acc[i0] += (x1 - x0) * wgt;
          else {
            acc[i0] += (i0 + 1 - x0) * wgt;
            diff[i0 + 1] += wgt;
            diff[i1] -= wgt;
            if (i1 < W) acc[i1] += (x1 - i1) * wgt;
          }
        }
      }
    }
    if (!any) continue;
    let run = 0;
    for (let x = xStart; x <= xEnd + 1 && x < W; x++) {
      run += diff[x];
      acc[x] += run;
    }
    onRow(py, acc, xStart, Math.min(W - 1, xEnd + 1));
  }
}

// ------------------------------------------------------------------ Çizgi (stroke) → çokgen birleşimi

function signedArea(p) {
  let a = 0;
  const n = p.length >> 1;
  for (let i = 0; i < n; i++) {
    const j = (i + 1) % n;
    a += p[2 * i] * p[2 * j + 1] - p[2 * j] * p[2 * i + 1];
  }
  return a / 2;
}

function pushOriented(out, p) {
  const a = signedArea(p);
  if (Math.abs(a) < 1e-12) return;
  if (a < 0) {
    const r = [];
    for (let i = (p.length >> 1) - 1; i >= 0; i--) r.push(p[2 * i], p[2 * i + 1]);
    out.push(r);
  } else out.push(p);
}

function circlePoly(cx, cy, r) {
  const step = r > FLAT_TOL ? 2 * Math.acos(Math.max(-1, 1 - FLAT_TOL / r)) : Math.PI / 2;
  const n = Math.min(256, Math.max(8, Math.ceil((2 * Math.PI) / step)));
  const p = [];
  for (let i = 0; i < n; i++) {
    const t = (i / n) * 2 * Math.PI;
    p.push(cx + r * Math.cos(t), cy + r * Math.sin(t));
  }
  return p;
}

function strokeOutline(subpaths, hw, cap, join, miterLimit) {
  const out = [];
  for (const sp of subpaths) {
    const raw = sp.pts;
    const pts = [];
    for (let i = 0; i < raw.length; i += 2) {
      const n = pts.length;
      if (n && Math.abs(pts[n - 2] - raw[i]) < 1e-9 && Math.abs(pts[n - 1] - raw[i + 1]) < 1e-9) continue;
      pts.push(raw[i], raw[i + 1]);
    }
    let closed = sp.closed;
    let n = pts.length >> 1;
    if (closed && n > 1 && Math.abs(pts[0] - pts[2 * n - 2]) < 1e-9 && Math.abs(pts[1] - pts[2 * n - 1]) < 1e-9) {
      pts.length -= 2;
      n--;
    }
    if (n === 1) {
      if (cap === 'round') out.push(circlePoly(pts[0], pts[1], hw));
      else if (cap === 'square') {
        const x = pts[0];
        const y = pts[1];
        out.push([x - hw, y - hw, x + hw, y - hw, x + hw, y + hw, x - hw, y + hw]);
      }
      continue;
    }
    if (n < 2) continue;
    if (n === 2 && closed) closed = false; // iki noktalı kapalı yol: gidiş-dönüş, uçlar yine birleşim olur
    const segCount = closed ? n : n - 1;
    const dirs = [];
    for (let s = 0; s < segCount; s++) {
      const a = s;
      const b = (s + 1) % n;
      const dx = pts[2 * b] - pts[2 * a];
      const dy = pts[2 * b + 1] - pts[2 * a + 1];
      const L = Math.hypot(dx, dy);
      dirs.push([dx / L, dy / L]);
    }
    for (let s = 0; s < segCount; s++) {
      const a = s;
      const b = (s + 1) % n;
      let ax = pts[2 * a];
      let ay = pts[2 * a + 1];
      let bx = pts[2 * b];
      let by = pts[2 * b + 1];
      const [dx, dy] = dirs[s];
      if (!closed && cap === 'square') {
        if (s === 0) { ax -= dx * hw; ay -= dy * hw; }
        if (s === segCount - 1) { bx += dx * hw; by += dy * hw; }
      }
      const nx = -dy * hw;
      const ny = dx * hw;
      pushOriented(out, [ax + nx, ay + ny, bx + nx, by + ny, bx - nx, by - ny, ax - nx, ay - ny]);
    }
    if (!closed && cap === 'round') {
      out.push(circlePoly(pts[0], pts[1], hw));
      out.push(circlePoly(pts[2 * n - 2], pts[2 * n - 1], hw));
    }
    const first = closed ? 0 : 1;
    const last = closed ? n - 1 : n - 2;
    for (let v = first; v <= last; v++) {
      const d0 = dirs[(v - 1 + segCount) % segCount];
      const d1 = dirs[v % segCount];
      const vx = pts[2 * v];
      const vy = pts[2 * v + 1];
      const cross = d0[0] * d1[1] - d0[1] * d1[0];
      const dot = d0[0] * d1[0] + d0[1] * d1[1];
      if (Math.abs(cross) < 1e-9 && dot > 0) continue;
      if (join === 'round') {
        out.push(circlePoly(vx, vy, hw));
        continue;
      }
      const s = cross > 0 ? -1 : 1;
      const n0x = -d0[1];
      const n0y = d0[0];
      const n1x = -d1[1];
      const n1y = d1[0];
      const p0x = vx + s * hw * n0x;
      const p0y = vy + s * hw * n0y;
      const p1x = vx + s * hw * n1x;
      const p1y = vy + s * hw * n1y;
      const c = n0x * n1x + n0y * n1y;
      if (join === 'miter' && 1 + c > 1e-9 && Math.sqrt(2 / (1 + c)) <= miterLimit) {
        const mx = vx + (s * hw * (n0x + n1x)) / (1 + c);
        const my = vy + (s * hw * (n0y + n1y)) / (1 + c);
        pushOriented(out, [vx, vy, p0x, p0y, mx, my, p1x, p1y]);
      } else {
        pushOriented(out, [vx, vy, p0x, p0y, p1x, p1y]);
      }
    }
  }
  return out;
}

// ------------------------------------------------------------------ Tuval ve çizim

class Layer {
  constructor(w, h) {
    this.w = w;
    this.h = h;
    this.px = new Float32Array(w * h * 4); // ön-çarpımlı RGBA
  }
}

function paintCoverage(layer, contours, rule, color, alpha, clip) {
  const { w, h, px } = layer;
  const cr = color.r;
  const cg = color.g;
  const cb = color.b;
  const ca = color.a * alpha;
  if (ca <= 0) return;
  scanFill(contours, rule, w, h, (y, cov, x0, x1) => {
    const row = y * w;
    for (let x = x0; x <= x1; x++) {
      let c = cov[x];
      if (c <= 0) continue;
      if (c > 1) c = 1;
      if (clip) c *= clip[row + x];
      const a = ca * c;
      if (a <= 0) continue;
      const i = (row + x) * 4;
      const ia = 1 - a;
      px[i] = cr * a + px[i] * ia;
      px[i + 1] = cg * a + px[i + 1] * ia;
      px[i + 2] = cb * a + px[i + 2] * ia;
      px[i + 3] = a + px[i + 3] * ia;
    }
  });
}

function compositeLayer(dst, src, opacity) {
  const d = dst.px;
  const s = src.px;
  for (let i = 0; i < d.length; i += 4) {
    const sa = s[i + 3] * opacity;
    if (sa <= 0) continue;
    const ia = 1 - sa;
    d[i] = s[i] * opacity + d[i] * ia;
    d[i + 1] = s[i + 1] * opacity + d[i + 1] * ia;
    d[i + 2] = s[i + 2] * opacity + d[i + 2] * ia;
    d[i + 3] = sa + d[i + 3] * ia;
  }
}

const SHAPES = new Set(['path', 'rect', 'circle', 'ellipse', 'line', 'polyline', 'polygon']);
const CONTAINERS = new Set(['g', 'svg', 'a']);
const SKIP = new Set(['title', 'desc', 'metadata', 'defs', 'clipPath', 'symbol']);
export const SUPPORTED_ELEMENTS = new Set([...SHAPES, ...CONTAINERS, ...SKIP, 'use']);

function collectIds(el, map) {
  if (el.attrs.id) map.set(el.attrs.id, el);
  for (const c of el.children) collectIds(c, map);
  return map;
}

function refId(v) {
  if (!v) return null;
  const m = /^\s*url\(\s*['"]?#([^'")\s]+)['"]?\s*\)\s*$/.exec(v);
  return m ? m[1] : null;
}

function viewBoxTransform(svgEl, W, H) {
  const vb = (svgEl.attrs.viewBox || '').trim().split(/[\s,]+/).filter(Boolean).map(Number);
  if (vb.length !== 4 || vb[2] <= 0 || vb[3] <= 0) {
    const w = num(svgEl.attrs.width, W);
    const h = num(svgEl.attrs.height, H);
    return [W / w, 0, 0, H / h, 0, 0];
  }
  const [minx, miny, vw, vh] = vb;
  const par = (svgEl.attrs.preserveAspectRatio || 'xMidYMid meet').trim().split(/\s+/);
  if (par[0] === 'none') return [W / vw, 0, 0, H / vh, -minx * (W / vw), -miny * (H / vh)];
  const slice = par[1] === 'slice';
  const s = slice ? Math.max(W / vw, H / vh) : Math.min(W / vw, H / vh);
  const al = par[0];
  const ax = al.includes('xMin') ? 0 : al.includes('xMax') ? 1 : 0.5;
  const ay = al.includes('YMin') ? 0 : al.includes('YMax') ? 1 : 0.5;
  const tx = (W - vw * s) * ax - minx * s;
  const ty = (H - vh * s) * ay - miny * s;
  return [s, 0, 0, s, tx, ty];
}

/** SVG kök öğesinin viewBox en/boy oranı (genişlik / yükseklik). */
export function svgAspect(text) {
  const svg = parseXml(text);
  const vb = (svg.attrs.viewBox || '').trim().split(/[\s,]+/).filter(Boolean).map(Number);
  if (vb.length === 4 && vb[2] > 0 && vb[3] > 0) return vb[2] / vb[3];
  const w = num(svg.attrs.width, 0);
  const h = num(svg.attrs.height, 0);
  return w > 0 && h > 0 ? w / h : 1;
}

/**
 * SVG metnini RGBA'ya çizer.
 * @returns {{ width:number, height:number, data:Uint8Array, warnings:string[] }}
 */
export function renderSvg(text, { width, height } = {}) {
  const svg = parseXml(text);
  const aspect = svgAspect(text);
  const W = Math.max(1, Math.round(width || (height ? height * aspect : 128)));
  const H = Math.max(1, Math.round(height || W / aspect));
  const ids = collectIds(svg, new Map());
  const warnings = new Set();
  const root = new Layer(W, H);
  const rootCtm = viewBoxTransform(svg, W, H);

  const clipMask = (id, ctm, parentClip) => {
    const cp = ids.get(id);
    if (!cp || cp.name !== 'clipPath') {
      warnings.add(`clip-path #${id} bulunamadı`);
      return parentClip;
    }
    if (cp.attrs.clipPathUnits === 'objectBoundingBox') warnings.add('clipPathUnits=objectBoundingBox desteklenmiyor');
    const m = mul(ctm, parseTransform(cp.attrs.transform));
    const mask = new Float32Array(W * H);
    const addShape = (el, mm, st) => {
      if (el.name === 'use') {
        const ref = ids.get((el.attrs.href || el.attrs['xlink:href'] || '').replace(/^#/, ''));
        if (!ref) return;
        const um = mul(mul(mm, parseTransform(el.attrs.transform)), [1, 0, 0, 1, num(el.attrs.x), num(el.attrs.y)]);
        addShape(ref, um, computeStyle(el, st));
        return;
      }
      if (!SHAPES.has(el.name)) {
        if (!SKIP.has(el.name)) warnings.add(`clipPath içinde desteklenmeyen <${el.name}>`);
        return;
      }
      const s2 = computeStyle(el, st);
      if (s2.display === 'none') return;
      const em = mul(mm, parseTransform(el.attrs.transform));
      const sub = pathToSubpaths(shapeToPathData(el), em);
      const contours = sub.map((s) => s.pts);
      scanFill(contours, s2['clip-rule'] === 'evenodd' ? 'evenodd' : 'nonzero', W, H, (y, cov, x0, x1) => {
        const row = y * W;
        for (let x = x0; x <= x1; x++) {
          const c = Math.min(1, cov[x]);
          if (c <= 0) continue;
          const i = row + x;
          mask[i] = mask[i] + c - mask[i] * c;
        }
      });
    };
    const base = { ...INITIAL_STYLE };
    for (const ch of cp.children) addShape(ch, m, base);
    if (parentClip) for (let i = 0; i < mask.length; i++) mask[i] *= parentClip[i];
    return mask;
  };

  const drawShape = (el, st, ctm, target, clip) => {
    const d = shapeToPathData(el);
    if (!d) return;
    const sub = pathToSubpaths(d, ctm);
    if (!sub.length) return;
    const fill = parseColor(st.fill, st.color);
    const stroke = parseColor(st.stroke, st.color);
    if (fill && fill.url) { warnings.add(`desteklenmeyen dolgu ${fill.url}`); }
    if (stroke && stroke.url) { warnings.add(`desteklenmeyen çizgi boyası ${stroke.url}`); }
    const fo = Math.min(1, Math.max(0, num(st['fill-opacity'], 1)));
    const so = Math.min(1, Math.max(0, num(st['stroke-opacity'], 1)));
    if (fill && !fill.url && el.name !== 'line' && el.name !== 'polyline' ? true : fill && !fill.url && el.name === 'polyline') {
      paintCoverage(target, sub.map((s) => s.pts), st['fill-rule'] === 'evenodd' ? 'evenodd' : 'nonzero', fill, fo, clip);
    }
    if (stroke && !stroke.url) {
      const sw = num(st['stroke-width'], 1) * detScale(ctm);
      if (sw > 0) {
        const contours = strokeOutline(sub, sw / 2, st['stroke-linecap'] || 'butt', st['stroke-linejoin'] || 'miter',
          Math.max(1, num(st['stroke-miterlimit'], 4)));
        paintCoverage(target, contours, 'nonzero', stroke, so, clip);
      }
    }
  };

  const walk = (el, parentStyle, ctm, target, clip, depth) => {
    if (depth > 64) throw new Error('SVG çok derin (use döngüsü?)');
    if (SKIP.has(el.name)) return;
    if (!SUPPORTED_ELEMENTS.has(el.name)) {
      warnings.add(`desteklenmeyen öğe <${el.name}> atlandı`);
      return;
    }
    const st = computeStyle(el, parentStyle);
    if (st.display === 'none') return;
    let m = ctm;
    if (el.name === 'svg' && depth > 0) {
      const x = num(el.attrs.x);
      const y = num(el.attrs.y);
      warnings.add('iç içe <svg> basit çeviri olarak çizildi');
      m = mul(m, [1, 0, 0, 1, x, y]);
    }
    if (el.attrs.transform) m = mul(m, parseTransform(el.attrs.transform));
    if (el.name === 'use') {
      const href = (el.attrs.href || el.attrs['xlink:href'] || '').replace(/^#/, '');
      const ref = ids.get(href);
      if (!ref) { warnings.add(`use #${href} bulunamadı`); return; }
      m = mul(m, [1, 0, 0, 1, num(el.attrs.x), num(el.attrs.y)]);
    }
    let myClip = clip;
    const cid = refId(st['clip-path']);
    if (cid) myClip = clipMask(cid, m, clip);
    const op = Math.min(1, Math.max(0, num(st.opacity, 1)));
    if (op <= 0) return;
    let dst = target;
    if (op < 1) dst = new Layer(W, H);
    if (el.name === 'use') {
      const ref = ids.get((el.attrs.href || el.attrs['xlink:href'] || '').replace(/^#/, ''));
      walk(ref, st, m, dst, myClip, depth + 1);
    } else if (CONTAINERS.has(el.name)) {
      for (const ch of el.children) walk(ch, st, m, dst, myClip, depth + 1);
    } else if (SHAPES.has(el.name)) {
      if (st.visibility !== 'hidden' && st.visibility !== 'collapse') drawShape(el, st, m, dst, myClip);
    }
    if (dst !== target) compositeLayer(target, dst, op);
  };

  const rootStyle = { ...INITIAL_STYLE };
  for (const ch of svg.children) walk(ch, computeStyle(svg, rootStyle), rootCtm, root, null, 1);

  const out = new Uint8Array(W * H * 4);
  const p = root.px;
  for (let i = 0; i < out.length; i += 4) {
    const a = p[i + 3];
    if (a <= 0) continue;
    const aa = Math.min(1, a);
    out[i] = Math.max(0, Math.min(255, Math.round((p[i] / aa) * 255)));
    out[i + 1] = Math.max(0, Math.min(255, Math.round((p[i + 1] / aa) * 255)));
    out[i + 2] = Math.max(0, Math.min(255, Math.round((p[i + 2] / aa) * 255)));
    out[i + 3] = Math.round(aa * 255);
  }
  return { width: W, height: H, data: out, warnings: [...warnings] };
}

/**
 * Profil denetimi: desteklenmeyen öğe/öznitelikleri listeler (çizmeden).
 * @returns {{ problems: string[], counts: Record<string, number>, viewBox: number[]|null }}
 */
export function inspectSvg(text) {
  const svg = parseXml(text);
  const problems = [];
  const counts = {};
  const ids = collectIds(svg, new Map());
  const BAD_ATTR = ['stroke-dasharray', 'filter', 'mask', 'marker-start', 'marker-mid', 'marker-end'];
  const visit = (el) => {
    counts[el.name] = (counts[el.name] || 0) + 1;
    if (!SUPPORTED_ELEMENTS.has(el.name)) problems.push(`desteklenmeyen öğe <${el.name}>`);
    const css = parseStyleAttr(el.attrs.style);
    const all = { ...el.attrs, ...css };
    for (const k of BAD_ATTR) if (all[k] != null && all[k] !== 'none') problems.push(`<${el.name}> ${k} desteklenmiyor`);
    for (const k of ['fill', 'stroke', 'color']) {
      if (all[k] == null) continue;
      try {
        const c = parseColor(all[k], null);
        if (c && c.url) problems.push(`<${el.name}> ${k}=${all[k]} (gradyan/desen) desteklenmiyor`);
      } catch (e) {
        problems.push(`<${el.name}> ${k}: ${e.message}`);
      }
    }
    for (const k of ['x', 'y', 'width', 'height', 'r', 'rx', 'ry', 'cx', 'cy', 'stroke-width']) {
      if (el.name === 'svg' && (k === 'width' || k === 'height')) continue;
      if (all[k] != null && String(all[k]).trim().endsWith('%')) problems.push(`<${el.name}> ${k} yüzde uzunluk`);
    }
    if (all.transform) {
      try { parseTransform(all.transform); } catch (e) { problems.push(e.message); }
    }
    const cid = refId(all['clip-path']);
    if (all['clip-path'] && all['clip-path'] !== 'none' && (!cid || !ids.has(cid))) problems.push(`clip-path çözülemedi: ${all['clip-path']}`);
    if (el.name === 'use') {
      const href = (el.attrs.href || el.attrs['xlink:href'] || '').replace(/^#/, '');
      if (!ids.has(href)) problems.push(`use #${href} bulunamadı`);
    }
    if (el.name === 'path') {
      try { pathToSubpaths(el.attrs.d || '', IDENT); } catch (e) { problems.push(`path d: ${e.message}`); }
    }
    for (const c of el.children) visit(c);
  };
  visit(svg);
  const vb = (svg.attrs.viewBox || '').trim().split(/[\s,]+/).filter(Boolean).map(Number);
  return { problems, counts, viewBox: vb.length === 4 ? vb : null };
}
