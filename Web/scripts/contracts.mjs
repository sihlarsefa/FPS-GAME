import { readFileSync, writeFileSync } from 'node:fs';
import assert from 'node:assert/strict';
import { ENDPOINTS, LOBBY_PATH } from '../js/api/endpoints.js';
const root = new URL('../../', import.meta.url);
const read = p => readFileSync(new URL(p, root), 'utf8');
export function checkRoutes() {
  const files = { api: 'Backend/Harekat.Api/Program.cs', telemetry: 'Backend/Harekat.Telemetry/src/Harekat.Telemetry.Api/Program.cs' };
  for (const [service, file] of Object.entries(files)) {
    const source = read(file);
    const actual = [...source.matchAll(/app\.Map(Get|Post|Delete|Patch|Put)\("([^"]+)"/g)].map(m => `${m[1].toUpperCase()} ${m[2]}`);
    if (service === 'api') { actual.push('GET /metrics'); assert.ok(source.includes(`MapHub<LobbyHub>("${LOBBY_PATH}")`)); }
    const declared = Object.values(ENDPOINTS).filter(e => (e.service || 'api') === service).map(e => `${e.method} ${e.path}`);
    assert.deepEqual(declared.sort(), actual.sort(), `Endpoint drift in ${file}`);
  }
}
function jsType(type) {
  type = type.trim();
  if (type.endsWith('?')) return `(${jsType(type.slice(0, -1))}|null)`;
  const generic = /^(?:IReadOnlyList|List)<(.+)>$/.exec(type);
  if (generic) return `Array<${jsType(generic[1])}>`;
  if (type.startsWith('IReadOnlyDictionary<')) return 'Object<string, number>';
  if (['int', 'float', 'double', 'long', 'MilitaryRank', 'MatchEventType', 'ReviewQueueStatus'].includes(type)) return 'number';
  if (['string', 'Guid', 'DateTimeOffset'].includes(type)) return 'string';
  if (type === 'bool') return 'boolean';
  return type;
}
export function generatedTypes() {
  const sources = ['Backend/Harekat.Application/Dtos/Dtos.cs', 'Backend/Harekat.Domain/ValueObjects/CareerStats.cs', 'Backend/Harekat.Telemetry/src/Harekat.Telemetry.Application/Contracts/Dtos.cs'];
  let out = '// Generated from backend DTOs by scripts/contracts.mjs --write; check snapshot before deployment.\n';
  const typedef = (name, fields) => `/**\n * @typedef {Object} ${name}\n${fields.map(([type, name]) => ` * @property {${jsType(type)}} ${name[0].toLowerCase() + name.slice(1)}`).join('\n')}\n */\n`;
  for (const file of sources) {
    const source = read(file); out += `// ${file}\n`;
    for (const m of source.matchAll(/public sealed record (\w+)\(([\s\S]*?)\);/g)) {
      const fields = [...m[2].matchAll(/(?:^|,)\s*([\w?<>]+)\s+(\w+)(?:\s*=\s*[^,]+)?/g)].map(f => [f[1], f[2]]);
      out += typedef(m[1], fields);
    }
    // Classes here have no nested class declarations; property bodies do not affect this split.
    for (const m of source.matchAll(/public sealed class (\w+)([\s\S]*?)(?=public sealed class|$)/g)) {
      const fields = [...m[2].matchAll(/public (?:required )?([\w?<> ,]+?) (\w+) \{ get;/g)].map(f => [f[1], f[2]]);
      out += typedef(m[1], fields);
    }
  }
  return out + 'export {};\n';
}
checkRoutes();
const target = new URL('../js/api/types.js', import.meta.url);
if (process.argv.includes('--write')) writeFileSync(target, generatedTypes());
else assert.equal(readFileSync(target, 'utf8'), generatedTypes(), 'DTO drift; run node scripts/contracts.mjs --write after reviewing changes.');
console.log(`contracts ok — ${Object.keys(ENDPOINTS).length} HTTP routes + lobby hub; DTO snapshot matches`);
