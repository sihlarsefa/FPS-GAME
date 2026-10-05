// HAREKÂT — Bağımlılıksız PNG kodlayıcı / çözücü (yalnızca node:zlib).
// encodePng: RGBA8 (düz, ön-çarpımsız) → PNG Buffer.
// decodePng: 8 bit RGB / RGBA PNG → { width, height, data: Uint8Array RGBA }.
import zlib from 'node:zlib';

const CRC_TABLE = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c >>> 0;
  }
  return t;
})();

function crc32(buf) {
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) c = CRC_TABLE[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function chunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length, 0);
  const td = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(td), 0);
  return Buffer.concat([len, td, crc]);
}

const SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

/**
 * @param {number} width
 * @param {number} height
 * @param {Uint8Array} rgba  width*height*4 bayt, düz alfa
 * @param {{ text?: Record<string,string> }} [opts]  tEXt anahtar/değer (ASCII/Latin-1)
 */
export function encodePng(width, height, rgba, opts = {}) {
  const stride = width * 4;
  const raw = Buffer.alloc((stride + 1) * height);
  // Satır başına "Up" (2) veya "Sub" (1) filtre: basit sezgisel — en küçük mutlak toplamı seç.
  const cand = [Buffer.alloc(stride), Buffer.alloc(stride), Buffer.alloc(stride)];
  for (let y = 0; y < height; y++) {
    const row = y * stride;
    const prev = y > 0 ? row - stride : -1;
    let best = 0;
    let bestSum = Infinity;
    for (let f = 0; f < 3; f++) {
      const out = cand[f];
      let sum = 0;
      for (let i = 0; i < stride; i++) {
        const v = rgba[row + i];
        let p = 0;
        if (f === 1) p = i >= 4 ? rgba[row + i - 4] : 0;
        else if (f === 2) p = prev >= 0 ? rgba[prev + i] : 0;
        const r = (v - p) & 0xff;
        out[i] = r;
        sum += r < 128 ? r : 256 - r;
      }
      if (sum < bestSum) {
        bestSum = sum;
        best = f;
      }
    }
    const o = y * (stride + 1);
    raw[o] = best;
    cand[best].copy(raw, o + 1);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8; // bit derinliği
  ihdr[9] = 6; // RGBA
  ihdr[10] = 0;
  ihdr[11] = 0;
  ihdr[12] = 0;
  const parts = [SIGNATURE, chunk('IHDR', ihdr)];
  for (const [k, v] of Object.entries(opts.text || {})) {
    parts.push(chunk('tEXt', Buffer.concat([Buffer.from(k, 'latin1'), Buffer.from([0]), Buffer.from(String(v), 'latin1')])));
  }
  parts.push(chunk('IDAT', zlib.deflateSync(raw, { level: 9 })));
  parts.push(chunk('IEND', Buffer.alloc(0)));
  return Buffer.concat(parts);
}

function paeth(a, b, c) {
  const p = a + b - c;
  const pa = Math.abs(p - a);
  const pb = Math.abs(p - b);
  const pc = Math.abs(p - c);
  if (pa <= pb && pa <= pc) return a;
  if (pb <= pc) return b;
  return c;
}

/** 8 bit, renk tipi 2 (RGB) veya 6 (RGBA), taramasız PNG'leri çözer. */
export function decodePng(buf) {
  if (!Buffer.isBuffer(buf)) buf = Buffer.from(buf);
  if (!buf.subarray(0, 8).equals(SIGNATURE)) throw new Error('PNG imzası yok');
  let pos = 8;
  let width = 0;
  let height = 0;
  let colorType = -1;
  let bitDepth = 0;
  let interlace = 0;
  const idat = [];
  while (pos < buf.length) {
    const len = buf.readUInt32BE(pos);
    const type = buf.toString('ascii', pos + 4, pos + 8);
    const data = buf.subarray(pos + 8, pos + 8 + len);
    if (type === 'IHDR') {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      bitDepth = data[8];
      colorType = data[9];
      interlace = data[12];
    } else if (type === 'IDAT') idat.push(data);
    else if (type === 'IEND') break;
    pos += 12 + len;
  }
  if (bitDepth !== 8 || (colorType !== 6 && colorType !== 2) || interlace !== 0) {
    throw new Error(`Desteklenmeyen PNG (bitDepth=${bitDepth}, colorType=${colorType}, interlace=${interlace})`);
  }
  const bpp = colorType === 6 ? 4 : 3;
  const stride = width * bpp;
  const raw = zlib.inflateSync(Buffer.concat(idat));
  const px = new Uint8Array(stride * height);
  for (let y = 0; y < height; y++) {
    const f = raw[y * (stride + 1)];
    const src = y * (stride + 1) + 1;
    const dst = y * stride;
    for (let i = 0; i < stride; i++) {
      const x = raw[src + i];
      const a = i >= bpp ? px[dst + i - bpp] : 0;
      const b = y > 0 ? px[dst - stride + i] : 0;
      const c = y > 0 && i >= bpp ? px[dst - stride + i - bpp] : 0;
      let v;
      switch (f) {
        case 0: v = x; break;
        case 1: v = x + a; break;
        case 2: v = x + b; break;
        case 3: v = x + ((a + b) >> 1); break;
        case 4: v = x + paeth(a, b, c); break;
        default: throw new Error(`Bilinmeyen PNG filtresi ${f}`);
      }
      px[dst + i] = v & 0xff;
    }
  }
  if (bpp === 4) return { width, height, data: px };
  const out = new Uint8Array(width * height * 4);
  for (let i = 0, j = 0; i < px.length; i += 3, j += 4) {
    out[j] = px[i];
    out[j + 1] = px[i + 1];
    out[j + 2] = px[i + 2];
    out[j + 3] = 255;
  }
  return { width, height, data: out };
}
