import puppeteer from 'puppeteer-core';
const b = await puppeteer.launch({ executablePath: '/usr/bin/google-chrome', headless: 'new', args: ['--no-sandbox','--use-angle=swiftshader','--enable-unsafe-swiftshader'] });
const p = await b.newPage(); await p.setViewport({ width: 432, height: 768, hasTouch: true, isMobile: true });
await p.goto('file:///workspace/outpost-zero/web/index.html'); await new Promise(r=>setTimeout(r,800));
await p.tap('#playBtn'); await new Promise(r=>setTimeout(r,500));
console.log('state', await p.evaluate(()=>window.__oz.S.state));
// touch drag right
const cdp = await p.target().createCDPSession();
const tp = (type, x) => cdp.send('Input.dispatchTouchEvent', { type, touchPoints: type==='touchEnd'?[]:[{ x, y: 600 }] });
await tp('touchStart', 200); for (let x=200;x<=330;x+=10){ await tp('touchMove', x); } await tp('touchEnd');
await new Promise(r=>setTimeout(r,500));
console.log('after touch drag right targetX', await p.evaluate(()=>[window.__oz.S.targetX.toFixed(2), window.__oz.S.x.toFixed(2)]));
await p.mouse.move(300,600); await p.mouse.down(); await p.mouse.move(120,600,{steps:10}); await p.mouse.up();
await new Promise(r=>setTimeout(r,500));
console.log('after mouse drag left', await p.evaluate(()=>[window.__oz.S.targetX.toFixed(2), window.__oz.S.x.toFixed(2)]));
await p.tap('#shieldBtn'); console.log('shield', await p.evaluate(()=>window.__oz.S.shieldT.toFixed(1)));
await b.close();
