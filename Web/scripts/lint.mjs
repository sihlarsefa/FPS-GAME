import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, extname } from 'node:path';

const root = new URL('..', import.meta.url).pathname;
let errors = 0;

function walk(dir) {
  for (const name of readdirSync(dir)) {
    if (name === 'node_modules' || name === 'dist') continue;
    const p = join(dir, name);
    if (statSync(p).isDirectory()) walk(p);
    else if (['.js', '.mjs', '.css', '.html', '.json'].includes(extname(p))) check(p);
  }
}

function check(file) {
  const text = readFileSync(file, 'utf8');
  if (text.includes('\0')) {
    console.error('Binary/null in', file);
    errors++;
  }
  if (extname(file) === '.js' && /\bfrom\s+['"]react['"]/.test(text)) {
    console.error('Unexpected React import in vanilla portal:', file);
    errors++;
  }
  if (extname(file) === '.css') {
    // unbalanced braces
    const open = (text.match(/\{/g) || []).length;
    const close = (text.match(/\}/g) || []).length;
    if (open !== close) {
      console.error('Unbalanced CSS braces in', file, open, close);
      errors++;
    }
  }
  if (file.endsWith('web.config') || file.endsWith('.config')) {
    /* skip */
  }
}

walk(root);

// web.config presence + key rules
try {
  const cfg = readFileSync(join(root, 'web.config'), 'utf8');
  for (const needle of ['ApiProxy', 'SpaFallback', 'Content-Security-Policy', 'Strict-Transport-Security']) {
    if (!cfg.includes(needle)) {
      console.error('web.config missing', needle);
      errors++;
    }
  }
} catch (e) {
  console.error('web.config missing');
  errors++;
}

if (errors) {
  console.error(`lint failed: ${errors} issue(s)`);
  process.exit(1);
}
console.log('lint ok');
