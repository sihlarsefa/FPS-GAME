import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, extname } from 'node:path';

const root = new URL('..', import.meta.url).pathname;
let errors = 0;

function walk(dir) {
  for (const name of readdirSync(dir)) {
    if (name === 'node_modules' || name === 'dist') continue;
    const p = join(dir, name);
    if (statSync(p).isDirectory()) walk(p);
    else if (['.js', '.mjs', '.css', '.html'].includes(extname(p))) check(p);
  }
}

function check(file) {
  const text = readFileSync(file, 'utf8');
  if (text.includes('\0')) {
    console.error('Binary/null in', file);
    errors++;
  }
  if (extname(file) === '.js' && text.includes('React') && !file.includes('README')) {
    console.error('Unexpected React reference in vanilla portal:', file);
    errors++;
  }
}

walk(root);
if (errors) {
  console.error(`lint failed: ${errors} issue(s)`);
  process.exit(1);
}
console.log('lint ok');
