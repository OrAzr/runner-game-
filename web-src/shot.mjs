import puppeteer from 'puppeteer-core';
import http from 'http'; import fs from 'fs'; import path from 'path';
const root = path.resolve('../web');
const srv = http.createServer((q, r) => { let f = path.join(root, decodeURIComponent(q.url.split('?')[0])); if (f.endsWith('/')) f += 'index.html'; fs.readFile(f, (e, d) => { if (e) { r.writeHead(404); r.end(); return; } r.writeHead(200, { 'Content-Type': f.endsWith('.js') ? 'text/javascript' : 'text/html' }); r.end(d); }); }).listen(8765);
const b = await puppeteer.launch({ executablePath: '/usr/bin/google-chrome', headless: 'new', args: ['--no-sandbox', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const p = await b.newPage();
const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error' || m.type() === 'warning') errs.push(m.text()); });
await p.setViewport({ width: 432, height: 768, deviceScaleFactor: 2.5, isMobile: true, hasTouch: true });
const out = process.argv[2] || '../screenshots';
await p.goto('http://localhost:8765/index.html?demo&shot', { waitUntil: 'load' });
await new Promise(r => setTimeout(r, 1500));
await p.screenshot({ path: `${out}/01-title.png` });
await p.evaluate(() => window.__oz.start());
const plan = JSON.parse(process.argv[3] || '[[3.6,"02-gates"],[10,"03-zombies"],[100,"04-flamethrower"]]');
for (const [t, name] of plan) {
  const st = await p.evaluate((t) => { window.__oz.advance(t); const S = window.__oz.S; return { dist: S.dist.toFixed(1), count: S.count, weapon: S.weapon, state: S.state, boss: window.__oz.boss ? Math.round(window.__oz.boss.hp) : null, kills: S.kills }; }, t);
  await new Promise(r => setTimeout(r, 300));
  await p.evaluate(() => document.querySelectorAll('.pop').forEach(e => e.remove()));
  if (name) await p.screenshot({ path: `${out}/${name}.png` });
  console.log(name, JSON.stringify(st));
}
console.log('errors:', errs.slice(0, 10));
await b.close(); srv.close();
