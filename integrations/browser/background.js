const host = "com.paraxyoz.dynamic_island";
const seen = new Map();
let port, timer, prefix, scanInProgress = false, idle = false, inFlight = 0;

async function ensurePrefix() {
  if (prefix) return;
  const existing = await chrome.storage.local.get("profile");
  const profile = existing.profile || crypto.randomUUID().replaceAll("-", "").slice(0, 12);
  if (!existing.profile) await chrome.storage.local.set({ profile });
  prefix = "web." + profile + ".";
}

function connect() {
  if (port) return;
  const connection = chrome.runtime.connectNative(host);
  port = connection;
  connection.onDisconnect.addListener(() => {
    void chrome.runtime.lastError;
    if (port === connection) {
      port = undefined;
      clearInterval(timer);
      timer = undefined;
      inFlight = 0;
      seen.clear();
    }
  });
  connection.onMessage.addListener(reply => {
    if (port === connection) {
      inFlight = Math.max(0, inFlight - 1);
      if (!reply.ok || (idle && inFlight === 0)) connection.disconnect();
    }
  });
}

function relay(item) {
  const name = (item.filename || "Файл").split(/[\\/]/).pop().slice(0, 120);
  const state = item.state === "complete" ? "100" : item.state === "interrupted" ?
    (item.error === "USER_CANCELED" ? "cancelled" : "failed") :
    item.totalBytes > 0 ? String(Math.max(0, Math.min(99, Math.floor(item.bytesReceived * 100 / item.totalBytes)))) : "pending";
  if (seen.get(item.id) === state) return;
  connect();
  idle = false;
  inFlight++;
  port.postMessage({ Id: prefix + item.id, State: state, Name: name });
  if (item.state === "in_progress") seen.set(item.id, state);
  else seen.delete(item.id);
}

async function scan() {
  if (scanInProgress) return;
  scanInProgress = true;
  try {
    await ensurePrefix();
    const active = await chrome.downloads.search({ state: "in_progress", limit: 24 });
    for (const item of active) relay(item);
    if (active.length === 0) {
      idle = true;
      clearInterval(timer);
      timer = undefined;
      if (inFlight === 0) port?.disconnect();
    } else if (port && !timer) {
      timer = setInterval(() => void scan().catch(() => {}), 1000);
    }
  } finally {
    scanInProgress = false;
  }
}

chrome.downloads.onCreated.addListener(() => void scan().catch(() => {}));
chrome.downloads.onChanged.addListener(change => {
  if (!change.state && !change.filename) return;
  void (async () => {
    await ensurePrefix();
    const [item] = await chrome.downloads.search({ id: change.id });
    if (item) relay(item);
    await scan();
  })().catch(() => {});
});
chrome.runtime.onStartup.addListener(() => void scan().catch(() => {}));
chrome.runtime.onInstalled.addListener(() => void scan().catch(() => {}));
