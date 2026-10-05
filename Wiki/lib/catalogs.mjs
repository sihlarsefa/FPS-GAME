export function parseWeapons(source) {
  const chunks = source.split('new WeaponDefinitionData(').slice(1);
  return chunks.map(chunk => {
    const id = /^WeaponIds\.(\w+), WeaponCategory\.(\w+)/.exec(chunk);
    if (!id) throw new Error('Unsupported weapon constructor');
    const number = name => { const m = new RegExp(`\\b${name}\\s*[:=]\\s*(-?[\\d.]+)f?`).exec(chunk); if (!m) throw new Error(`${id[1]} missing ${name}`); return +m[1]; };
    const interval = /fireIntervalSeconds:\s*IntervalFromRpm\(([\d.]+)f\)/.exec(chunk);
    const fire_interval_seconds = interval ? 60 / +interval[1] : number('fireIntervalSeconds');
    return {weapon_id:id[1], category:id[2], display_name:/DisplayName\s*=\s*"([^"]+)"/.exec(chunk)?.[1], damage:number('damage'),magazine_size:number('magazineSize'),fire_interval_seconds,rpm:60/fire_interval_seconds,reload_duration_seconds:number('reloadDurationSeconds'),range_m:number('range'),headshot_multiplier:number('headshotMultiplier'),ammo_type:/AmmoType = AmmoType\.(\w+)/.exec(chunk)?.[1],falloff_start:number('FalloffStart'),falloff_end:number('FalloffEnd'),min_damage_factor:number('MinDamageFactor'),pellet_count:/PelletCount\s*=\s*(\d+)/.exec(chunk)?.[1] ? +/PelletCount\s*=\s*(\d+)/.exec(chunk)[1] : 1};
  });
}
export function parseRanks(source) {
  const array = name => { const m = new RegExp(`${name}\\s*=\\s*\\{([\\s\\S]*?)\\};`).exec(source); if(!m) throw new Error(`Rank array ${name} missing`); return m[1].replace(/\/\/[^\n]*/g,''); };
  const strings = name => [...array(name).matchAll(/"([^"]+)"/g)].map(m=>m[1]);
  const names=strings('FullNames'), shorts=strings('ShortNames'), xp=array('ExperienceThresholds').split(',').map(s=>+s.trim());
  if(names.length!==xp.length||names.length!==shorts.length||!names.length) throw new Error('Rank arrays differ');
  return names.map((name,i)=>({id:`rank-${i}`,name,short:shorts[i],xp:xp[i],index:i}));
}
export function parseItems(source) { return [...source.matchAll(/\bAdd(Ammo|Medical|Boost|Throwable|Armor|Backpack|Weapon)\((?:ItemIds|WeaponIds)\.(\w+),\s*"([^"]+)"([^;]*);/g)].map(m=>({id:m[2],name:m[3],category:m[1],parameters:m[4].replace(/^,\s*/,'').trim()})); }
export function parseRoles(source) {
  const names = [...source.matchAll(/case TeamRole\.(\w+): return "([^"]+)";/g)].slice(0,7);
  return names.map(([_,id,name])=>{
    const m = id==='Rifleman' ? /default: \/\/ Piyade([\s\S]*?)break;/.exec(source) : new RegExp(`case TeamRole\\.${id}:([\\s\\S]*?)break;`).exec(source);
    if(!m)throw new Error(`Missing loadout ${id}`);
    const body=m[1];
    return {id,name,weapons:[...new Set([...body.matchAll(/WeaponIds\.(\w+)/g)].map(m=>m[1]))],vest:+/VestLevel = (\d+)/.exec(body)[1],helmet:+/HelmetLevel = (\d+)/.exec(body)[1],backpack:+/BackpackLevel = (\d+)/.exec(body)[1],items:[...body.matchAll(/\.Add\((?:useMpt76 \? )?ItemIds\.(\w+)(?: : ItemIds\.(\w+))?, (\d+)\)/g)].map(m=>({id:m[1],alternative:m[2]||null,count:+m[3]}))};
  });
}
export function verifyBalance(weapons, snapshot) {
  if (snapshot.weapons.length !== weapons.length) throw new Error('BalanceCalc weapon count is stale');
  const close = (a, b) => Math.abs(a - b) <= Math.max(0.02, Math.abs(b) * 0.001);
  for (const w of weapons) {
    const before = snapshot.weapons.find(x => x.weapon_id === w.weapon_id);
    if (!before) throw new Error(`BalanceCalc missing ${w.weapon_id}`);
    for (const k of Object.keys(w)) {
      const ok = typeof w[k] === 'number' ? close(w[k], before[k]) : before[k] === w[k];
      if (!ok) throw new Error(`BalanceCalc stale: ${w.weapon_id}.${k}`);
    }
  }
}
export function parseCsv(text) {
 const out=[];let row=[],field='',quoted=false;for(let i=0;i<text.length;i++){let c=text[i];if(c==='"'){if(quoted&&text[i+1]==='"'){field+='"';i++;}else quoted=!quoted;}else if(c===','&&!quoted){row.push(field);field='';}else if(c==='\n'&&!quoted){row.push(field.replace(/\r$/,''));if(row.some(Boolean))out.push(row);row=[];field='';}else field+=c;}if(quoted)throw new Error('Unclosed CSV');if(field||row.length){row.push(field.replace(/\r$/,''));out.push(row);}const head=out.shift();return out.map(r=>{if(r.length!==head.length)throw new Error('CSV width');return Object.fromEntries(head.map((h,i)=>[h,r[i]]));});
}
