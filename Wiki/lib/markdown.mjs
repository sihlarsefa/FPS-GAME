export const escape = s => String(s ?? '').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
export const slug = s => String(s).replace(/ı/g,'i').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,'-').replace(/^-|-$/g,'');
// Safe subset: all source HTML escaped; links only to known generated pages/assets or http(s).
export function markdown(text, resolveLink=()=>null) {
 const inline = raw => {
   const links=[];
   let value=raw.replace(/(!?)\[([^\]]+)\]\(([^)]+)\)/g,(_,img,label,url)=>{const safe=resolveLink(url);const token=`WIKILINKTOKEN${links.length}END`;links.push(safe ? img ? `<img src="${escape(safe)}" alt="${escape(label)}" loading="lazy">` : `<a href="${escape(safe)}">${escape(label)}</a>` : escape(label));return token;});
   value=escape(value).replace(/`([^`]+)`/g,'<code>$1</code>').replace(/\*\*([^*]+)\*\*/g,'<strong>$1</strong>').replace(/\*([^*]+)\*/g,'<em>$1</em>');
   return value.replace(/WIKILINKTOKEN(\d+)END/g,(_,i)=>links[+i]||'');
 };
 const lines=text.replace(/\r/g,'').split('\n');const out=[];let list=null,code=null,codeLines=[],paragraph=[];
 const flush=()=>{if(paragraph.length){out.push(`<p>${inline(paragraph.join(' '))}</p>`);paragraph=[];}};
 const close=()=>{flush();if(list){out.push(`</${list}>`);list=null;}};
 for(let i=0;i<lines.length;i++){
  const line=lines[i];if(line.startsWith('```')){close();if(code!==null){out.push(`<pre><code>${escape(codeLines.join('\n'))}</code></pre>`);code=null;codeLines=[];}else code=line.slice(3);continue;}if(code!==null){codeLines.push(line);continue;}
  if(!line.trim()){close();continue;}
  const h=/^(#{1,6})\s+(.+)$/.exec(line);if(h){close();out.push(`<h${Math.min(6,h[1].length+1)}>${inline(h[2])}</h${Math.min(6,h[1].length+1)}>`);continue;}
  if(line.startsWith('|')&&/^\|[\s:|-]+\|\s*$/.test(lines[i+1]||'')){close();const cells=s=>s.trim().replace(/^\||\|$/g,'').split('|').map(s=>s.trim());const heads=cells(line);i++;const rows=[];while(lines[i+1]?.startsWith('|')){rows.push(cells(lines[++i]));}out.push(`<div class="table-scroll"><table><thead><tr>${heads.map(c=>`<th scope="col">${inline(c)}</th>`).join('')}</tr></thead><tbody>${rows.map(r=>`<tr>${r.map(c=>`<td>${inline(c)}</td>`).join('')}</tr>`).join('')}</tbody></table></div>`);continue;}
  const l=/^\s*(?:[-*]|\d+[.)])\s+(.+)$/.exec(line);if(l){flush();const tag=/^\s*\d/.test(line)?'ol':'ul';if(list!==tag){if(list)out.push(`</${list}>`);out.push(`<${tag}>`);list=tag;}out.push(`<li>${inline(l[1])}</li>`);continue;}
  if(line.startsWith('>')){close();out.push(`<blockquote>${inline(line.replace(/^>\s?/,''))}</blockquote>`);continue;}
  paragraph.push(line);
 }
 close();if(code!==null)out.push(`<pre><code>${escape(codeLines.join('\n'))}</code></pre>`);return out.join('\n');
}
