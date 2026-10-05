import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import assert from 'node:assert/strict';

const root = new URL('..', import.meta.url);

// Static presence
for (const f of ['index.html', 'css/main.css', 'js/app.js', 'js/api.js', 'js/mock-data.js']) {
  readFileSync(new URL(f, root));
}

const mock = await import(pathToFileURL(new URL('js/mock-data.js', root).pathname).href);
assert.equal(mock.WEAPONS.length, 10);
assert.equal(mock.RANKS.length, 19);
assert.ok(mock.LOCATIONS.some((l) => l.name === 'Kuzgun Köyü'));

const auth = mock.mockAuth('TestUser', 'x');
assert.equal(auth.player.displayName, 'TestUser');
assert.ok(auth.token.startsWith('mock-jwt-'));

console.log('test ok — weapons', mock.WEAPONS.length, 'ranks', mock.RANKS.length);
