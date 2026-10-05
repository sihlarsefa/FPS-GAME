// Haberler: content/news/*.md → content/news/<slug>.json + index.json.
// İsteğe bağlı frontmatter: date (YYYY-AA-GG), title, summary.
import { readdirSync, readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { join, dirname, basename } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { renderMarkdown, parseFrontmatter, stripHtml, excerpt, DATE_RE } from './lib/markdown.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const newsDir = join(root, 'content', 'news');
const DEFAULT_DATE = '2026-10-05';

function parsePost(md, slug) {
  const { meta, body } = parseFrontmatter(md);
  const rendered = renderMarkdown(body);
  const title = String(meta.title || rendered.title || slug);
  const html = rendered.html.replace(/^<h1>[\s\S]*?<\/h1>\n?/, '');
  const summary = String(meta.summary || excerpt(stripHtml(html.split('\n')[0] || html), 180));
  const date = DATE_RE.test(String(meta.date || '')) ? String(meta.date) : DEFAULT_DATE;
  return { id: slug, slug, title, body: summary, summary, date, html };
}

export function buildNews() {
  if (!existsSync(newsDir)) mkdirSync(newsDir, { recursive: true });
  const files = readdirSync(newsDir).filter((f) => f.endsWith('.md')).sort();
  const posts = files.map((f) => {
    const slug = basename(f, '.md');
    const post = parsePost(readFileSync(join(newsDir, f), 'utf8'), slug);
    writeFileSync(join(newsDir, `${slug}.json`), JSON.stringify(post, null, 2));
    return { id: post.id, slug: post.slug, title: post.title, body: post.summary, date: post.date };
  });
  posts.sort((a, b) => b.date.localeCompare(a.date) || a.slug.localeCompare(b.slug));
  writeFileSync(join(newsDir, 'index.json'), JSON.stringify(posts, null, 2));
  return posts;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const posts = buildNews();
  console.log(`news build ok — ${posts.length} posts`);
}
