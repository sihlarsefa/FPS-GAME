import { cpSync, mkdirSync, rmSync, writeFileSync, readFileSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildNews } from './news-build.mjs';
import { buildPatchnotes } from './patchnotes-build.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const dist = join(root, 'dist');
rmSync(dist, { recursive: true, force: true });
mkdirSync(dist, { recursive: true });

buildNews();
buildPatchnotes();

const items = [
  'index.html', 'css', 'js', 'assets', 'i18n', 'content',
  'manifest.webmanifest', 'sw.js', 'web.config', 'README.md',
];
for (const item of items) {
  const src = join(root, item);
  if (!existsSync(src)) continue;
  cpSync(src, join(dist, item), { recursive: true });
}

const stamp = new Date().toISOString();
writeFileSync(join(dist, 'build-info.json'), JSON.stringify({
  builtAt: stamp,
  stack: 'html-css-js',
  version: JSON.parse(readFileSync(join(root, 'package.json'), 'utf8')).version,
  features: ['jwt-api', 'admin', 'iis', 'i18n', 'pwa-push', 'theme', 'news-md', 'patchnotes-rss', 'download', 'teams', 'achievements-catalog'],
}, null, 2));

const html = readFileSync(join(dist, 'index.html'), 'utf8');
if (!html.includes('js/app.js')) throw new Error('build missing app.js');
if (!existsSync(join(dist, 'web.config'))) throw new Error('build missing web.config');
if (!existsSync(join(dist, 'i18n', 'tr.json'))) throw new Error('build missing i18n');
if (!existsSync(join(dist, 'content', 'patchnotes', 'rss.xml'))) throw new Error('build missing patchnotes RSS');
if (!existsSync(join(dist, 'content', 'data', 'download.json'))) throw new Error('build missing download.json');
console.log('build ok → Web/dist');
