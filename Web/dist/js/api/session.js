/** Session storage is tab-scoped; never persist passwords or server keys. */
export function createSession(storage, key = 'harekat.session.v2') {
  let value = null;
  let revision = 0;
  try { value = JSON.parse(storage?.getItem(key) || 'null'); } catch { /* unavailable or corrupt storage */ }
  if (!value?.accessToken || !value?.refreshToken || !Number.isFinite(Date.parse(value?.expiresAt))) value = null;
  const save = () => { try { if (value) storage?.setItem(key, JSON.stringify(value)); else storage?.removeItem(key); } catch { /* memory-only fallback */ } };
  return {
    get: () => value,
    revision: () => revision,
    set(next) {
      if (!next?.accessToken || !next?.refreshToken || !Number.isFinite(Date.parse(next.expiresAt))) throw new TypeError('Invalid auth response');
      value = next; revision++; save();
    },
    clear() { value = null; revision++; save(); },
    updatePlayer(player) { if (value) { value = { ...value, player }; save(); } },
  };
}
export const canModerate = (player) => ['Admin', 'Moderator'].includes(player?.role);
