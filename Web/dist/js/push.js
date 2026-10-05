/**
 * PWA push notification scaffolding for squad invites.
 * Uses Push API when available; mock mode shows an in-page permission UI.
 */
import { CONFIG } from './config.js';
import { t } from './i18n.js';

const VAPID_PUBLIC_PLACEHOLDER = 'BMockVapidKeyForHarekatPortalDevelopmentOnlyReplaceInProd0123456789';

export async function requestPushPermission() {
  if (!('Notification' in window)) return { ok: false, reason: 'unsupported' };
  const perm = await Notification.requestPermission();
  return { ok: perm === 'granted', permission: perm };
}

export async function subscribePush() {
  if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
    return { ok: false, reason: 'unsupported' };
  }
  const reg = await navigator.serviceWorker.ready;
  try {
    const sub = await reg.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlBase64ToUint8Array(VAPID_PUBLIC_PLACEHOLDER),
    });
    const json = sub.toJSON();
    localStorage.setItem('harekat_push_sub', JSON.stringify(json));
    return { ok: true, subscription: json, mock: CONFIG.useMock };
  } catch (err) {
    // Invalid VAPID in mock/dev is expected — store a local stub.
    const stub = { endpoint: 'mock://push/squad-invite', keys: { p256dh: 'mock', auth: 'mock' } };
    localStorage.setItem('harekat_push_sub', JSON.stringify(stub));
    return { ok: true, subscription: stub, mock: true, warning: String(err.message || err) };
  }
}

export function getStoredSubscription() {
  try { return JSON.parse(localStorage.getItem('harekat_push_sub') || 'null'); } catch { return null; }
}

export function initPushUi(root = document) {
  const btn = root.querySelector('#enablePushBtn');
  if (!btn) return;
  const status = root.querySelector('#pushStatus');
  if (getStoredSubscription() && status) status.textContent = 'Push kayıtlı.';
  btn.addEventListener('click', async () => {
    const perm = await requestPushPermission();
    if (!perm.ok) {
      if (status) status.textContent = `İzin: ${perm.permission || perm.reason}`;
      return;
    }
    const sub = await subscribePush();
    if (status) status.textContent = sub.ok ? (sub.mock ? 'Push (mock) aktif — tim daveti.' : 'Push aktif.') : 'Push başarısız.';
  });
}

function urlBase64ToUint8Array(base64String) {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
  const raw = atob(base64);
  const out = new Uint8Array(raw.length);
  for (let i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
  return out;
}

/** Inject a CTA into squad page if missing — called from pages optionally. */
export function pushCtaHtml() {
  return `<div class="card" style="margin-top:1rem">
    <h3>Tim daveti bildirimi</h3>
    <p class="muted">PWA push altyapısı (VAPID). ${t('mock_note')}</p>
    <button type="button" class="btn ghost" id="enablePushBtn">Bildirimleri aç</button>
    <p class="muted" id="pushStatus" role="status"></p>
  </div>`;
}
