import puppeteer from 'puppeteer-core';
const b = await puppeteer.launch({ executablePath: '/usr/bin/google-chrome', headless: 'new', args: ['--no-sandbox','--autoplay-policy=no-user-gesture-required'] });
const p = await b.newPage(); await p.goto('file:///workspace/outpost-zero/web/index.html'); await new Promise(r=>setTimeout(r,500));
await p.click('#playBtn'); await new Promise(r=>setTimeout(r,2500));
console.log(await p.evaluate(()=>{const a=window.__oz.audio;return {ready:a.ready,state:a.ctx&&a.ctx.state,buffers:Object.fromEntries(Object.entries(a.buffers).map(([k,v])=>[k,+v.duration.toFixed(3)])),loops:Object.keys(a.loops)}}));
await b.close();
