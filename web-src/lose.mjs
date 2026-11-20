import puppeteer from 'puppeteer-core';
const b = await puppeteer.launch({ executablePath: '/usr/bin/google-chrome', headless: 'new', args: ['--no-sandbox','--use-angle=swiftshader','--enable-unsafe-swiftshader'] });
const p = await b.newPage(); await p.setViewport({ width: 432, height: 768 }); const errs=[]; p.on('pageerror',e=>errs.push(e.message)); p.on('console',m=>{if(m.type()==='error')errs.push(m.text())});
await p.goto('file:///workspace/outpost-zero/web/index.html'); await new Promise(r=>setTimeout(r,800));
await p.evaluate(()=>window.__oz.start());
console.log(await p.evaluate(()=>{window.__oz.advance(15);return [window.__oz.S.state,window.__oz.S.count, document.getElementById('end').className, document.getElementById('endTitle').textContent]}));
console.log(errs); await b.close();
