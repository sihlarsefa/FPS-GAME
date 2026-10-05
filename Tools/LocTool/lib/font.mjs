/** Unicode script buckets used when LegacyRuntime.ttf binary is not on disk. */
export function classifyChar(c) {
  const cp = c.codePointAt(0);
  if (cp >= 0x0600 && cp <= 0x06FF || cp >= 0x0750 && cp <= 0x077F || cp >= 0x08A0 && cp <= 0x08FF
    || cp >= 0xFB50 && cp <= 0xFDFF || cp >= 0xFE70 && cp <= 0xFEFF) return 'arabic';
  if (c === 'Ə' || c === 'ə') return 'azerbaijani-ext';
  if (/[çğıöşüÇĞİÖŞÜ]/.test(c)) return 'turkish-latin';
  if (cp > 0x024F && cp < 0x0600) return 'latin-extended-other';
  if (cp <= 0x024F) return 'basic-latin-and-latin1';
  return 'other';
}

/**
 * Unity LegacyRuntime (built-in Arial-class bitmap/dynamic font) historically
 * covers Western + Turkish Latin but not Arabic shaping glyphs or some AZ letters.
 * This is a conservative review list when the exact TTF is unavailable.
 */
export function legacyRuntimeLikelyGaps(characters) {
  const missing = [];
  const byScript = {};
  for (const c of characters) {
    const script = classifyChar(c);
    byScript[script] = (byScript[script] || 0) + 1;
    if (script === 'arabic' || script === 'azerbaijani-ext' || script === 'other') missing.push(c);
  }
  return {
    assumption: 'LegacyRuntime ≈ Latin/Turkish UI font; Arabic and AZ Ə/ə need a dedicated face (e.g. Noto).',
    byScript,
    likelyMissing: missing,
    likelyMissingCount: missing.length,
    recommendation: 'Use TextMeshPro + Noto Sans (LAT/AZ) + Noto Naskh Arabic (AR). Do not ship HUD on LegacyRuntime for ar locale.'
  };
}

// Reads Unicode cmap formats 4/12 from an SFNT TTF/OTF. No font installation or mutation.
export function fontCoverage(buffer, characters) {
  const need = (offset, length) => { if (offset < 0 || offset + length > buffer.length) throw new Error('Truncated font'); };
  need(0, 12);
  const signature = buffer.toString('ascii', 0, 4);
  if (signature !== 'OTTO' && buffer.readUInt32BE(0) !== 0x00010000) throw new Error('Expected single SFNT TTF/OTF (TTC/WOFF unsupported)');
  const count = buffer.readUInt16BE(4); let cmap;
  for (let i = 0; i < count; i++) { const p = 12 + i * 16; need(p, 16); if (buffer.toString('ascii', p, p + 4) === 'cmap') { cmap = buffer.readUInt32BE(p + 8); need(cmap, buffer.readUInt32BE(p + 12)); } }
  if (cmap === undefined) throw new Error('No cmap table');
  need(cmap, 4); const maps = [];
  for (let i = 0; i < buffer.readUInt16BE(cmap + 2); i++) {
    const p = cmap + 4 + i * 8; need(p, 8);
    const platform = buffer.readUInt16BE(p), encoding = buffer.readUInt16BE(p + 2), offset = cmap + buffer.readUInt32BE(p + 4); need(offset, 2);
    if (!(platform === 0 || platform === 3 && (encoding === 1 || encoding === 10))) continue;
    const format = buffer.readUInt16BE(offset);
    if (format === 4 || format === 12) maps.push({ format, offset });
  }
  if (!maps.length) throw new Error('No supported Unicode cmap format 4/12');
  function has(code) {
    return maps.some(({ format, offset: o }) => {
      if (format === 12) {
        need(o, 16); const groups = buffer.readUInt32BE(o + 12); need(o + 16, groups * 12);
        for (let i = 0; i < groups; i++) { const p = o + 16 + i * 12, a = buffer.readUInt32BE(p), b = buffer.readUInt32BE(p + 4); if (code >= a && code <= b) return buffer.readUInt32BE(p + 8) + code - a !== 0; }
      } else if (code <= 65535) {
        need(o, 14); const n = buffer.readUInt16BE(o + 6) / 2; need(o + 14, n * 8 + 2);
        const ends = o + 14, starts = ends + n * 2 + 2, deltas = starts + n * 2, ranges = deltas + n * 2;
        for (let i = 0; i < n; i++) {
          const end = buffer.readUInt16BE(ends + i * 2), start = buffer.readUInt16BE(starts + i * 2);
          if (code < start || code > end) continue;
          const delta = buffer.readInt16BE(deltas + i * 2), range = buffer.readUInt16BE(ranges + i * 2);
          if (!range) return ((code + delta) & 65535) !== 0;
          const pos = ranges + i * 2 + range + (code - start) * 2; need(pos, 2); const glyph = buffer.readUInt16BE(pos);
          return glyph !== 0 && ((glyph + delta) & 65535) !== 0;
        }
      }
      return false;
    });
  }
  const missing = characters.filter(c => !has(c.codePointAt(0)));
  return { checked: characters.length, missing, covered: characters.length - missing.length, formats: [...new Set(maps.map(m => m.format))] };
}
