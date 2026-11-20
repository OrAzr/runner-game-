import puppeteer from 'puppeteer-core';
const b = await puppeteer.launch({ executablePath: '/usr/bin/google-chrome', headless: 'new', args: ['--no-sandbox','--use-angle=swiftshader','--enable-unsafe-swiftshader'] });
const p = await b.newPage(); const errs=[]; p.on('pageerror', e => errs.push(e.stack||e.message)); p.on('console', m=>errs.push(m.type()+': '+m.text()));
await p.setViewport({ width: 432, height: 768 });
await p.goto('file:///workspace/outpost-zero/web/index.html?demo'); await new Promise(r=>setTimeout(r,1000));
await p.evaluate(()=>window.__oz.start());
for (let i=0;i<10;i++){ const st=await p.evaluate(()=>{window.__oz.advance(10);const S=window.__oz.S,B=window.__oz.boss;return [S.dist|0,S.count,S.state,B&&Math.round(B.hp),B&&B.alive,S.kills]}); console.log(JSON.stringify(st)); await new Promise(r=>setTimeout(r,1200)); }
console.log(await p.evaluate(()=>document.getElementById('end').className+' '+document.getElementById('endStats').textContent));
console.log(errs); await b.close();
