// MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.3.0 (roadmap W-1, owner decision D-1: a read-only browser view, no write routes in the browser). /web serves one
/// static page that signs in with an existing MystTiq account (auth/browser-login) and shows each server's status,
/// players and backups through the ordinary read routes, so the roles apply exactly as for the desktop. A browser session
/// is marked as such: the API refuses every change made with it (only reading and signing out pass). The page itself holds
/// no data, so it is served without a token; everything it shows comes from authenticated calls. Remote access needs
/// authentication and TLS like every non-loopback use of the API.
/// </summary>
public static class BrowserView
{
    public const string ContentSecurityPolicy = "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self'; form-action 'none'; frame-ancestors 'none'; base-uri 'none'";

    public static bool IsPagePath(string? path) => path is "/web" or "/web/" or "/web/app.js" or "/web/app.css";

    // A browser session may read (GET, HEAD) and sign out; every other request is refused.
    public static bool BrowserSessionAllows(string method, string? path) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || string.Equals(path, "/api/v1/auth/logout", StringComparison.OrdinalIgnoreCase);

    public const string Html = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="referrer" content="no-referrer">
<title>MystTiq — read-only view</title>
<link rel="stylesheet" href="/web/app.css">
</head>
<body>
<header><h1>MystTiq</h1><span id="version" class="muted"></span><span class="readonly">Read-only view</span><button id="signout" type="button" hidden>Sign out</button></header>
<main>
<section id="signin" class="card">
<h2>Sign in</h2>
<p class="muted">Sign in with your MystTiq account. This view shows each server's status, players and backups; it cannot change anything.</p>
<form id="signin-form">
<label>Username <input id="username" autocomplete="username" required></label>
<label>Password <input id="password" type="password" autocomplete="current-password" required></label>
<button type="submit">Sign in</button>
</form>
<p id="signin-message" class="message" role="alert"></p>
</section>
<section id="view" hidden>
<p id="updated" class="muted"></p>
<div id="servers"></div>
</section>
</main>
<script src="/web/app.js"></script>
</body>
</html>
""";

    public const string Script = """
'use strict';
// MystTiq read-only browser view (v1.0.3.0, roadmap W-1). Reads status, players and backups; never writes. Every value is
// written with textContent, so nothing a server or player name contains can run as page code.
const $ = id => document.getElementById(id);
let token = sessionStorage.getItem('mysttiq-browser-token');

function el(tag, text, cls) {
  const e = document.createElement(tag);
  if (text !== undefined && text !== null) e.textContent = String(text);
  if (cls) e.className = cls;
  return e;
}

function show(signedIn) {
  $('signin').hidden = signedIn;
  $('view').hidden = !signedIn;
  $('signout').hidden = !signedIn;
}

function signOutLocal(message) {
  token = null;
  sessionStorage.removeItem('mysttiq-browser-token');
  show(false);
  $('signin-message').textContent = message || '';
}

async function api(path, options = {}) {
  const headers = Object.assign({ Accept: 'application/json' }, options.headers || {});
  if (token) headers.Authorization = 'Bearer ' + token;
  const response = await fetch(path, Object.assign({}, options, { headers, cache: 'no-store' }));
  if (response.status === 401) { signOutLocal('Your session has ended. Sign in again.'); throw new Error('signed out'); }
  if (!response.ok) throw new Error('HTTP ' + response.status);
  return response.json();
}

$('signin-form').addEventListener('submit', async event => {
  event.preventDefault();
  $('signin-message').textContent = '';
  try {
    const response = await fetch('/api/v1/auth/browser-login', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: $('username').value, password: $('password').value })
    });
    const body = await response.json().catch(() => ({}));
    if (!response.ok || !body.success) { $('signin-message').textContent = body.message || ('Sign-in failed (HTTP ' + response.status + ').'); return; }
    token = body.token;
    sessionStorage.setItem('mysttiq-browser-token', token);
    $('password').value = '';
    show(true);
    load();
  } catch (error) {
    $('signin-message').textContent = 'MystTiq could not be reached.';
  }
});

$('signout').addEventListener('click', async () => {
  try { await api('/api/v1/auth/logout', { method: 'POST' }); } catch (error) { /* signed out locally either way */ }
  signOutLocal('Signed out.');
});

async function load() {
  try {
    const health = await (await fetch('/healthz', { cache: 'no-store' })).json();
    $('version').textContent = 'v' + health.version;
    const sections = [];
    for (const id of health.serverProfileIds || []) sections.push(await server(id));
    $('servers').replaceChildren(...sections);
    $('updated').textContent = 'Updated ' + new Date().toLocaleTimeString() + ' · refreshes every 30 seconds';
  } catch (error) {
    if (token) $('updated').textContent = 'Could not refresh: ' + error.message;
  }
}

async function server(id) {
  const base = '/api/v1/servers/' + encodeURIComponent(id);
  const section = el('section', null, 'card server');
  section.append(el('h2', id));
  try {
    const s = await api(base + '/status');
    const running = (s.processes || []).length > 0;
    const state = s.ready ? 'Running' : running ? 'Starting' : 'Stopped';
    const line = el('p');
    line.append(el('span', state, 'tag ' + (s.ready ? 'ok' : running ? 'info' : 'off')), el('span', ' ' + (s.detail || ''), 'muted'));
    section.append(line);
  } catch (error) { section.append(el('p', 'Status unavailable: ' + error.message, 'muted')); }
  try {
    const p = await api(base + '/players');
    section.append(el('h3', 'Players online: ' + (p.onlineCount || 0)));
    const list = el('ul');
    for (const x of p.players || []) list.append(el('li', x.name + (x.level ? ' · level ' + x.level : '') + (x.platform ? ' · ' + x.platform : '')));
    if (!(p.players || []).length) list.append(el('li', p.available ? 'Nobody is online.' : (p.detail || 'Players are not available.'), 'muted'));
    section.append(list);
  } catch (error) { section.append(el('p', 'Players unavailable: ' + error.message, 'muted')); }
  try {
    const b = await api(base + '/backups');
    const items = (b.items || []).slice().sort((x, y) => String(y.createdAt).localeCompare(String(x.createdAt)));
    section.append(el('h3', 'Backups: ' + items.length));
    const table = el('table');
    const head = el('tr');
    for (const h of ['Created', 'File', 'Size', 'Checked', 'World day']) head.append(el('th', h));
    table.append(head);
    for (const x of items.slice(0, 10)) {
      const row = el('tr');
      row.append(el('td', new Date(x.createdAt).toLocaleString()), el('td', x.fileName), el('td', (x.sizeBytes / 1048576).toFixed(1) + ' MB'),
        el('td', x.verified ? 'Verified' : 'Unreadable'), el('td', x.worldDayNumber ? 'Day ' + x.worldDayNumber + (x.worldTimeText ? ' • ' + x.worldTimeText : '') : '—'));
      table.append(row);
    }
    section.append(table);
  } catch (error) { section.append(el('p', 'Backups unavailable: ' + error.message, 'muted')); }
  return section;
}

show(!!token);
if (token) load();
setInterval(() => { if (token) load(); }, 30000);
""";

    public const string Style = """
:root { color-scheme: dark light; --bg: #0d1b2a; --card: #14263a; --text: #e6edf5; --muted: #93a7bd; --ok: #2e8b57; --info: #2f6fb3; --off: #4b5d70; --accent: #5aa9e6; }
@media (prefers-color-scheme: light) { :root { --bg: #f2f5f9; --card: #ffffff; --text: #14212e; --muted: #5b6b7c; --off: #9aa8b6; } }
* { box-sizing: border-box; }
body { margin: 0; background: var(--bg); color: var(--text); font: 15px/1.45 system-ui, "Segoe UI", sans-serif; }
header { display: flex; gap: 12px; align-items: center; padding: 12px 16px; border-bottom: 1px solid var(--off); }
header h1 { margin: 0; font-size: 20px; }
header button { margin-left: auto; }
.readonly { border: 1px solid var(--accent); border-radius: 4px; padding: 1px 6px; font-size: 12px; }
main { max-width: 980px; margin: 0 auto; padding: 16px; }
.card { background: var(--card); border-radius: 8px; padding: 14px 16px; margin-bottom: 14px; }
.muted { color: var(--muted); }
.message { color: #e07a5f; min-height: 1.2em; }
label { display: block; margin: 8px 0; }
input { display: block; width: 100%; max-width: 320px; padding: 6px 8px; margin-top: 4px; }
button { padding: 6px 14px; cursor: pointer; }
.tag { display: inline-block; border-radius: 4px; padding: 1px 8px; font-size: 12px; font-weight: 700; color: #fff; }
.tag.ok { background: var(--ok); } .tag.info { background: var(--info); } .tag.off { background: var(--off); }
table { width: 100%; border-collapse: collapse; font-size: 13px; }
th, td { text-align: left; padding: 4px 6px; border-bottom: 1px solid var(--off); overflow-wrap: anywhere; }
""";

    public static IResult Page(HttpContext context, string body, string contentType)
    {
        context.Response.Headers["Content-Security-Policy"] = ContentSecurityPolicy;
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Cache-Control"] = "no-store";
        return Results.Text(body, contentType, Encoding.UTF8);
    }
}

/// <summary>v1.0.3.0 (roadmap W-1): which sign-in tokens belong to the browser view (kept as SHA-256, never the token).</summary>
public sealed class BrowserSessionRegistry
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> sessions = new(StringComparer.Ordinal);

    public void Add(string token, DateTimeOffset? expiresUtc) => sessions[Hash(token)] = expiresUtc ?? DateTimeOffset.UtcNow.AddHours(12);

    public void Remove(string token) => sessions.TryRemove(Hash(token), out _);

    public bool IsBrowserSession(string token)
    {
        var key = Hash(token);
        if (!sessions.TryGetValue(key, out var expires)) return false;
        if (expires > DateTimeOffset.UtcNow) return true;
        sessions.TryRemove(key, out _);
        return false;
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
