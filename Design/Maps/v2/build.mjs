import { mkdir, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = path.dirname(fileURLToPath(import.meta.url));
const point = ([x, y]) => ({ x, y });
const escape = value => String(value).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&apos;' })[c]);
const tiers = ['Low', 'Medium', 'High', 'Military'];
const tierNames = ['Düşük', 'Orta', 'Yüksek', 'Askerî'];
const terrain = [
  {
    id: 'AyazGecidi', name: 'Ayaz Geçidi', english: 'Frost Pass', seed: 4201, height: 220, water: 12,
    background: '#dae3df', contour: '#a4b8b0', accent: '#355e68',
    brief: 'Yüksek dağların arasında iki rotalı geçit. Radar sırtı erken görüş, kayak evi güvenli ikmal sağlar. Kar ve kaya siluetleri açık zemini tehlikeli kılar; beyaz yüzeylerde işaretlerin dış çizgisi korunur.',
    sound: ['Üst sırt: yönlü, seyrek rüzgâr; iletişimi maskelemeyecek dinamik aralık.', 'Kayak evi: ahşap gıcırtısı, iç/dış geçişte düşük geçiren filtre.', 'Radar üssü: düşük mekanik uğultu; yapı dışında hızla söner.', 'Kar yürüyüşü: kuru/sert kar için iki yüzey; hız ve duruşa göre örnek seçimi.', 'Geçit: kısa kaya yankısı; uzaktan gelen atışların yönü korunur.', 'Buz göleti: yalnızca kıyı su sesi; güvenli buz üstü yürüyüşü bu konseptte yok.'],
    locations: [
      ['Geçit Köyü', 0, -140, 10, 65, 1, 76, 'Merkez geçidin batısında kapalı ikmal. Taş duvarlar kısa hareketleri korur; iki çıkışla kuzey yol baskısından ayrıl.'],
      ['Kayak Evi', 0, -300, 265, 52, 1, 100, 'Ahşap avluda kısa menzil. Ana binaya sıkışmadan servis yoluna bir gözcü ayır; teras bütün geçidi görmez.'],
      ['Radar Üssü', 5, 275, 305, 55, 2, 118, 'En uzun görüş hattı. Alt sırt yaklaşımı ve anten kaidesi kör alan yaratır; çevre halkasından geri çekilme gerekir.'],
      ['Ayaz Karakolu', 1, 70, 360, 48, 2, 106, 'Kuzey kapısını denetler. İç avluya giren tim batı yamaç dönüşünü kaybeder; doğu çıkışı açık tutulur.'],
      ['Dağ İkmal Üssü', 2, 235, -175, 66, 3, 69, 'Tek askerî yağma çekirdeği. İki avlu ve servis çukuru güçlü ekipmanı üç yaklaşım riskiyle dengeler.'],
      ['Karaçam Sırtı', 6, -320, -120, 65, 0, 70, 'Düşük yağma, örtülü geçiş. Ağaçları tam görüş engeli varsayma; yamaç altı rota merkezden ayrılır.'],
      ['Eski Taş Ocağı', 3, 300, 55, 58, 1, 86, 'Basamaklı açık alan. Alt kazı örtüsü kullanılır; üst kenarda uzun süre sabit kalınmaz.'],
      ['Yayla Ağılı', 7, -210, -330, 52, 0, 61, 'Sakin başlangıç ve güney rotasyonu. Sınırlı yağma yüzünden on kişi tek binaya yığılmaz.'],
      ['Donuk Gözetleme', 9, -55, 200, 30, 2, 94, 'Kayak evi ile karakol arasında erken uyarı. Küçük kaya siperi tek girişli olmadığı için savunma sürekli döner.'],
      ['Güney Sığınağı', 8, 35, -345, 45, 1, 59, 'Geçitten çekilme durağı. Çatı kırıkları hedefi görünür kılar; duvar aralıklarıyla doğu üssüne bağlanır.']
    ],
    roads: [
      ['Geçit Yolu', 0, 8, [[0,-490],[0,-345],[-40,-180],[-45,0],[5,185],[70,360],[70,490]]],
      ['Kayak Evi Yolu', 1, 6, [[5,185],[-140,220],[-300,265]]],
      ['Radar Servisi', 1, 6, [[70,360],[160,370],[275,305]]],
      ['İkmal Yolu', 1, 7, [[-40,-180],[100,-210],[235,-175],[300,55],[275,305]]],
      ['Köy Bağlantısı', 1, 5, [[-45,0],[-140,10],[-240,-40],[-320,-120],[-210,-330],[0,-345]]],
      ['Gözetleme Yolu', 1, 4, [[5,185],[-55,200]]]
    ],
    lakes: [{ Center: point([345,-350]), Radius: 60, Depth: 3.2 }], rivers: [],
    sectors: [ ['S1','Kuzey geçit kapısı','Kirpi',70,470,106], ['S2','Güney geçit kapısı','Kirpi',0,-470,59], ['S3','Batı kaya düzlük','T-70',-405,245,100], ['S4','Doğu ikmal düzlüğü','T-70',385,-160,69] ]
  },
  {
    id: 'MaviLiman', name: 'Mavi Liman', english: 'Blue Harbour', seed: 4202, height: 120, water: 18,
    background: '#e4ddbb', contour: '#b4b792', accent: '#356f82',
    brief: 'Kıyı kasabası, liman ve zeytinlik arasında kısa rotasyonlar. İç sokaklar yakın menzile, sahil yolu uzun görüşe açılır. Deniz mevcut göl primitive’lerinin sınırda kesişen iki su diskiyle temsil edildiği bir tasarım önerisidir; liman yapıları için ayrı üretici gerekir.',
    sound: ['Liman: aralıklı halat/gövde sesi; sürekli metal gürültüsü adımı örtmez.', 'Deniz feneri: tepe rüzgârı, taş iç mekânda kısa yankı.', 'Zeytinlik: yumuşak yaprak sürtünmesi, az yoğunlukta böcek sesi.', 'Kıyı kasabası: dar sokak yansımaları; kapalı evlerde belirgin ses geçişi.', 'Sahil: kıyıya yaklaştıkça artan dalga; 60 m sonra belirgin azalır.', 'Sahil karakolu: sakin jeneratör; iki girişin ses bilgisi eşit tutulur.'],
    locations: [
      ['Kıyı Kasabası',0,60,-90,66,1,30,'Sokak halkası iki avluyu bağlar. Pazar çatısı deniz yönünü görür fakat batı sokak çıkışını göremez.'],
      ['Liman Depoları',2,245,-265,55,3,25,'Askerî yağma depolarda toplanır. Kıyı tarafı çıkmaz olduğu için iç yol ve kuru kanal üzerinden iki geri çekilme rotası gerekir.'],
      ['Deniz Feneri',5,260,145,35,2,47,'Tek kuleye bağımlı savunma engellenir; alçak servis yapısı alternatif siper sağlar. Tepeye araç çıkışı varsayılmaz.'],
      ['Zeytinlik',7,-290,155,76,0,43,'Geniş ama seyrek örtü. Teras duvarları rotasyonu böler; yağma küçük bakım kulübelerinde tutulur.'],
      ['Sahil Karakolu',1,45,315,55,2,41,'Kuzeyden geliş ile kıyı yolunu bağlar. Araç girişi ön avluda görünür; yaya için arka servis aralığı vardır.'],
      ['Eski Pazar',0,-130,-270,55,1,33,'Güney girişinden dengeli başlangıç. Tezgâhlar tam mermi koruması değildir; sağlam bina köşeleri okunur.'],
      ['Taş Teras',3,-300,-75,55,1,39,'Limanı uzaktan izler; aradaki düşük sırt görüşü keser. Alt basamak doğuya örtülü yaklaşım verir.'],
      ['Çam Korusu',6,-340,350,62,0,54,'Sessiz başlangıç. Sınırdan çok iç yamaca çıkış hedeflenir; güneydoğu açık geçişe erken hazırlık gerekir.'],
      ['Kanal Gözetleme',9,-65,105,28,2,38,'Dere geçişlerini duyurur. Dar siper iki farklı köprüye aynı anda hâkim olamaz.'],
      ['Güney Hanı',8,70,-395,42,1,27,'Güney intikalinden ilk durak. Duvar boşlukları pazar ve depolara eşit mesafede geçiş sunar.']
    ],
    roads: [
      ['Kıyı Yolu',0,8,[[0,-490],[70,-395],[60,-90],[60,100],[45,315],[45,490]]],
      ['Liman Servisi',1,7,[[70,-395],[245,-265],[210,-165],[60,-90]]],
      ['Fener Yolu',1,5,[[60,100],[170,180],[260,145]]],
      ['Zeytinlik Yolu',1,6,[[45,315],[-130,280],[-290,155],[-300,-75],[-130,-270],[70,-395]]],
      ['Pazar Yolu',1,6,[[60,-90],[-65,-115],[-130,-270]]],
      ['Koru Patikası',1,4,[[-290,155],[-370,245],[-340,350]]],
      ['Kanal Patikası',1,4,[[60,100],[-65,105]]]
    ],
    lakes: [{Center: point([475,-50]),Radius:245,Depth:5},{Center:point([485,405]),Radius:195,Depth:5}],
    rivers: [{Width:8,Depth:1.4,Points:[[-480,-170],[-320,-190],[-200,-175],[-80,-180],[60,-205],[195,-190],[245,-130]].map(point)}],
    sectors: [['S1','Kuzey sahil kapısı','Kirpi',45,470,41],['S2','Güney han kapısı','Kirpi',0,-470,27],['S3','Batı teras açıklığı','T-70',-420,-70,39],['S4','Zeytinlik açıklığı','T-70',-190,405,48]]
  }
];

export function densify(points, spacing = 8) {
  const out = [point(points[0])];
  for (let i=1;i<points.length;i++) { const a=points[i-1],b=points[i], n=Math.ceil(Math.hypot(b[0]-a[0],b[1]-a[1])/spacing); for(let j=1;j<=n;j++) out.push({x:+(a[0]+(b[0]-a[0])*j/n).toFixed(3),y:+(a[1]+(b[1]-a[1])*j/n).toFixed(3)}); }
  return out;
}
function bridges(roads,rivers) {
  const out=[];
  for(const road of roads) for(const river of rivers) for(let i=1;i<road.Points.length;i++) for(let j=1;j<river.Points.length;j++) {
    const a=road.Points[i-1],b=road.Points[i],c=river.Points[j-1],d=river.Points[j];
    const r={x:b.x-a.x,y:b.y-a.y},s={x:d.x-c.x,y:d.y-c.y},den=r.x*s.y-r.y*s.x;
    if(Math.abs(den)<1e-6)continue;
    const t=((c.x-a.x)*s.y-(c.y-a.y)*s.x)/den,u=((c.x-a.x)*r.y-(c.y-a.y)*r.x)/den;
    if(t<0||t>=1||u<0||u>=1)continue;
    const len=Math.hypot(r.x,r.y), sine=Math.abs(den)/(len*Math.hypot(s.x,s.y));
    out.push({Name:road.Name+' Köprüsü',Center:{x:a.x+t*r.x,y:a.y+t*r.y},Direction:{x:r.x/len,y:r.y/len},Length:Math.min((river.Width+18)/Math.max(.5,sine),60),Width:road.Width+1.5,DeckHeight:-1,Kind:road.Kind});
  }
  return out;
}
const cell = (x,z) => `${'ABCDEFGHIJ'[Math.min(9,Math.floor((x+500)/100))]}${Math.min(10,Math.floor((500-z)/100)+1)}`;
function svg(map,layout) {
  const px=x=>x+570, py=z=>570-z;
  const lines=p=>p.map(v=>`${px(v.x)},${py(v.y)}`).join(' ');
  const marks=layout.Locations.map((l,i)=>`<g data-location="${escape(l.Name)}"><circle cx="${px(l.Center.x)}" cy="${py(l.Center.y)}" r="${l.Radius}" fill="#fff" fill-opacity=".2" stroke="${map.accent}" stroke-dasharray="5 4"/><circle cx="${px(l.Center.x)}" cy="${py(l.Center.y)}" r="15" fill="${['#59634e','#3e6381','#725529','#833d35'][l.Tier]}"/><text x="${px(l.Center.x)}" y="${py(l.Center.y)+5}" fill="white" text-anchor="middle" font-size="13" font-weight="bold">${i+1}</text><text x="${px(l.Center.x)}" y="${py(l.Center.y)+l.Radius+20}" text-anchor="middle" font-size="14" font-weight="bold" paint-order="stroke" stroke="${map.background}" stroke-width="5">${escape(l.Name)}</text></g>`).join('');
  const sectors=map.sectors.map(([code,name,type,x,z])=>`<g><rect x="${px(x)-20}" y="${py(z)-14}" width="40" height="28" rx="2" fill="#f4dc90" stroke="#584b27"/><text x="${px(x)}" y="${py(z)+5}" font-size="13" text-anchor="middle">${code}</text><title>${escape(name)} · ${type}</title></g>`).join('');
  return `<svg xmlns="http://www.w3.org/2000/svg" width="1420" height="1220" viewBox="0 0 1420 1220" role="img" aria-labelledby="title desc"><title id="title">${map.name} — 100 m ölçekli pafta</title><desc id="desc">1000 × 1000 metre. Kuzey yukarı; 100 metre aralıklı A–J / 1–10 koordinat ağı. Numaralı on lokasyon; intikal sektörleri ve yağma kademeleri sağ lejantta.</desc><defs><pattern id="grid" width="100" height="100" patternUnits="userSpaceOnUse" x="70" y="70"><path d="M100 0H0V100" fill="none" stroke="#536e68" stroke-width=".8" opacity=".55"/></pattern><clipPath id="map"><rect x="70" y="70" width="1000" height="1000"/></clipPath></defs><rect width="1420" height="1220" fill="#f7f4e9"/><g font-family="Arial,sans-serif" fill="#233b39"><text x="70" y="37" font-size="27" font-weight="bold">HAREKÂT / ${map.name.toLocaleUpperCase('tr')}</text><text x="1110" y="37" font-size="14">TASARIM PAFTASI 02</text><g clip-path="url(#map)"><rect x="70" y="70" width="1000" height="1000" fill="${map.background}"/>${Array.from({length:12},(_,i)=>`<path d="M ${70+i*37} 70 Q ${440+i*20} 400 ${130+i*42} 1070 M ${1070-i*18} 70 Q ${650-i*10} 640 ${1070-i*27} 1070" fill="none" stroke="${map.contour}" stroke-width="2"/>`).join('')}${layout.Lakes.map(l=>`<circle cx="${px(l.Center.x)}" cy="${py(l.Center.y)}" r="${l.Radius}" fill="#a5c8cf" stroke="#6597a4"/>`).join('')}${layout.Rivers.map(r=>`<polyline points="${lines(r.Points)}" fill="none" stroke="#6597a4" stroke-width="${r.Width}"/>`).join('')}${layout.Roads.map(r=>`<polyline points="${lines(r.Points)}" fill="none" stroke="${r.Kind===0?'#776e60':'#ad906d'}" stroke-width="${r.Width}" stroke-linejoin="round"/><polyline points="${lines(r.Points)}" fill="none" stroke="#e7d9b7" stroke-width="1"/>`).join('')}<rect x="70" y="70" width="1000" height="1000" fill="url(#grid)"/>${marks}${sectors}${layout.Bridges.map(b=>`<rect x="${px(b.Center.x)-8}" y="${py(b.Center.y)-4}" width="16" height="8" fill="#302f28" transform="rotate(${-Math.atan2(b.Direction.y,b.Direction.x)*180/Math.PI} ${px(b.Center.x)} ${py(b.Center.y)})"/>`).join('')}</g><rect x="70" y="70" width="1000" height="1000" fill="none" stroke="#354e45" stroke-width="2"/>${Array.from({length:10},(_,i)=>`<text x="${120+i*100}" y="60" text-anchor="middle" font-size="16">${'ABCDEFGHIJ'[i]}</text><text x="${120+i*100}" y="1095" text-anchor="middle" font-size="16">${'ABCDEFGHIJ'[i]}</text><text x="48" y="${125+i*100}" text-anchor="middle" font-size="16">${i+1}</text>`).join('')}<path d="M1170 125V65 M1158 85L1170 65 1182 85" fill="none" stroke="#233b39" stroke-width="3"/><text x="1190" y="85" font-size="17">K / N</text><text x="1110" y="175" font-size="17" font-weight="bold">LOKASYON / YAĞMA</text>${layout.Locations.map((l,i)=>`<text x="1110" y="${207+i*38}" font-size="14">${i+1}. ${escape(l.Name)}</text><text x="1126" y="${222+i*38}" font-size="11">${cell(l.Center.x,l.Center.y)} · ${tierNames[l.Tier]} · ${l.TargetHeight} m</text>`).join('')}<text x="1110" y="620" font-size="17" font-weight="bold">İNTİKAL</text>${map.sectors.map(([c,n,t],i)=>`<text x="1110" y="${648+i*41}" font-size="13">${c} · ${t}</text><text x="1110" y="${665+i*41}" font-size="12">${n}</text>`).join('')}<text x="1110" y="860" font-size="12">Eş yükselti çizgileri şematiktir.</text><text x="1110" y="880" font-size="12">Konum ve yarıçaplar ölçeklidir.</text><text x="1110" y="900" font-size="12">1 SVG birimi = 1 metre.</text><text x="1110" y="920" font-size="12">Kurgu tatbikat coğrafyası.</text><path d="M70 1140H270 M70 1134V1146 M170 1134V1146 M270 1134V1146" stroke="#233b39" stroke-width="3"/><text x="70" y="1167" font-size="14">0</text><text x="155" y="1167" font-size="14">100</text><text x="250" y="1167" font-size="14">200 m</text><text x="450" y="1140" font-size="13">Dünya X = doğu · JSON Center.y = dünya Z = kuzey</text><text x="450" y="1166" font-size="13">${map.english} · HalfSize 500 · MaxHeight ${map.height} · WaterLevel ${map.water}</text></g></svg>`;
}
for(const map of terrain) {
  const dir=path.join(root,map.id);await mkdir(dir,{recursive:true});
  const layout={HalfSize:500,MaxHeight:map.height,WaterLevel:map.water,Seed:map.seed,Locations:map.locations.map(([Name,Kind,x,z,Radius,Tier,TargetHeight])=>({Name,Kind,Center:{x,y:z},Radius,Tier,FlattenRadius:Kind===6?18:Math.round(Radius*.68),IsMajor:Kind!==9,TargetHeight,ClearRadius:Kind===6?22:Radius})),Roads:map.roads.map(([Name,Kind,Width,Points])=>({Name,Kind,Width,Points:densify(Points)})),Lakes:map.lakes,Rivers:map.rivers.map(r=>({...r,Points:densify(r.Points.map(p=>[p.x,p.y]))})),Name:map.name,Bridges:[]};
  layout.Bridges=bridges(layout.Roads,layout.Rivers);
  const design={nameEnglish:map.english,grid:{cellMetres:100,columns:'ABCDEFGHIJ',rows:10,north:'+Z'},sectors:map.sectors.map(([id,name,transport,x,z,elevation])=>({id,name,transport,center:{x,y:z},radius:20,elevation})),elevationModel:'inverse-distance-squared; 30 m smoothing; location TargetHeight samples; design estimate only',roadGradeLimit:0.25,lootCounts:Object.fromEntries(tiers.map((t,i)=>[t,layout.Locations.filter(l=>l.Tier===i).length])),surfaceNotes:map.brief};
  await writeFile(path.join(dir,'layout.json'),JSON.stringify(layout,null,2)+'\n');
  await writeFile(path.join(dir,'design.json'),JSON.stringify(design,null,2)+'\n');
  await writeFile(path.join(dir,'pafta.svg'),svg(map,layout));
  await writeFile(path.join(dir,'TaktikNotlar.md'),`# ${map.name}\n\n${map.brief}\n\nKurgu tatbikat: Mavi/Kırmızı kuvvetler. Bu notlar oyun alanı tasarımı içindir.\n\n## Lokasyonlar\n\n`+map.locations.map((l,i)=>`### ${i+1}. ${l[0]} · ${cell(l[2],l[3])}\n\nMerkez (${l[2]}, ${l[3]}) m; yarıçap ${l[4]} m; tasarım kotu ${l[6]} m; yağma ${tiers[l[5]]}.\n\n${l[7]}\n`).join('\n')+'\n## İntikal sektörleri\n\n'+map.sectors.map(([c,n,t,x,z])=>`- **${c} / ${n} / ${t}:** (${x}, ${z}), ${cell(x,z)}. 20 m kontrol yarıçapı; T-70 için düz iniş ve rotor açıklığı Unity navmesh/fizik testi bekler. Kirpi sektörleri ana yol uçlarına bağlıdır.`).join('\n')+'\n\n## Yağma dağılımı\n\n'+Object.entries(design.lootCounts).map(([t,n])=>`- ${t}: ${n}/10 lokasyon (${n*10}%).`).join('\n')+'\n\nBu oranlar lokasyon sayısıdır; gerçek eşya düşme olasılığı veya nesne bütçesi değildir. LootCatalog kuralları değişmez. Military tek çekirdeğe sınırlı; High üç ayrı yönün riskli noktalarına dağıtılır.\n\n## Ses ortamı\n\n'+map.sound.map(s=>`- ${s}`).join('\n')+'\n\nSes erişilebilirliği: kritik intikal, alan daralması ve emir olayları metin/ikonla da verilir. Atmosfer kanalı ayrı ses ayarı; işitme eşikleri gerçek oyuncu testi bekler.\nAyrıntılı liste: [SesOrtami.md](SesOrtami.md).\n');
}
console.log('2 map layouts, design manifests, scaled SVGs and tactical notes built.');
