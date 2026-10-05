import { cpSync, mkdirSync, rmSync, writeFileSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

const root = new URL('..', import.meta.url).pathname;
const dist = join(root, 'dist');
rmSync(dist, { recursive: true, force: true });
mkdirSync(dist, { recursive: true });

for (const item of ['index.html', 'css', 'js', 'assets', 'manifest.webmanifest', 'sw.js']) {
  cpSync(join(root, item), join(dist, item), { recursive: true });
}

const stamp = new Date().toISOString();
writeFileSync(join(dist, 'build-info.json'), JSON.stringify({ builtAt: stamp, stack: 'html-css-js' }, null, 2));
const html = readFileSync(join(dist, 'index.html'), 'utf8');
if (!html.includes('js/app.js')) throw new Error('build missing app.js');
console.log('build ok → Web/dist');
