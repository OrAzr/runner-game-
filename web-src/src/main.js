// Outpost Zero - browser prototype (Three.js). Mirrors the Unity draft's mechanics.
import * as THREE from 'three';
import { AudioManager } from './audio.js';
import { LEVEL, WEAPONS, PICKUP_DURATION } from './level.js';

const params = new URLSearchParams(location.search);
const DEMO = params.has('demo');           // auto-steer (used for screenshots / attract mode)
const ROAD_HALF = 5;
const MAX_VISIBLE = 80;
const COL = { teal: 0x1f8a8a, tealDark: 0x125e60, gun: 0x2b3038, orange: 0xe8792b, skin: 0x7d9a4a, zshirt: 0x6b4a3a, road: 0x3a3d42, ground: 0x5a5e4c };

// ---------- renderer / scene ----------
const wrap = document.getElementById('game');
const renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: params.has('shot') });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.shadowMap.enabled = true; renderer.shadowMap.type = THREE.PCFSoftShadowMap;
wrap.appendChild(renderer.domElement);
const scene = new THREE.Scene();
scene.background = new THREE.Color(0x8e9690);
scene.fog = new THREE.Fog(0x8e9690, 45, 110);
const camera = new THREE.PerspectiveCamera(52, 9 / 16, 0.1, 200);
scene.add(new THREE.HemisphereLight(0xdfe8e6, 0x4a4a3c, 1.1));
const sun = new THREE.DirectionalLight(0xfff1dc, 1.6);
sun.position.set(-8, 20, 6); sun.castShadow = true;
sun.shadow.mapSize.set(1024, 1024);
Object.assign(sun.shadow.camera, { left: -16, right: 16, top: 16, bottom: -16, near: 1, far: 60 });
scene.add(sun, sun.target);

function resize() {
  const W = window.innerWidth, H = window.innerHeight;
  let w = W, h = H; if (w / h > 9 / 16) w = Math.round(h * 9 / 16); // keep portrait on desktop
  renderer.setSize(w, h); wrap.style.width = w + 'px'; wrap.style.height = h + 'px';
  camera.aspect = w / h; camera.updateProjectionMatrix();
}
window.addEventListener('resize', resize); resize();

const mat = (c, o = {}) => new THREE.MeshStandardMaterial({ color: c, roughness: 0.75, metalness: 0.1, ...o });

// ---------- world (sparse, muted) ----------
const world = new THREE.Group(); scene.add(world);
const ROAD_LEN = LEVEL.bossAt + 80;
{
  const road = new THREE.Mesh(new THREE.PlaneGeometry(ROAD_HALF * 2 + 1, ROAD_LEN + 60), mat(COL.road, { roughness: 0.95 }));
  road.rotation.x = -Math.PI / 2; road.position.z = -ROAD_LEN / 2 + 10; road.receiveShadow = true; world.add(road);
  const ground = new THREE.Mesh(new THREE.PlaneGeometry(200, ROAD_LEN + 120), mat(COL.ground, { roughness: 1 }));
  ground.rotation.x = -Math.PI / 2; ground.position.set(0, -0.02, -ROAD_LEN / 2); ground.receiveShadow = true; world.add(ground);
  // edge lines + center dashes (instanced)
  const lineMat = new THREE.MeshBasicMaterial({ color: 0xd8d8d0 });
  for (const x of [-ROAD_HALF, ROAD_HALF]) {
    const l = new THREE.Mesh(new THREE.PlaneGeometry(0.15, ROAD_LEN + 60), lineMat);
    l.rotation.x = -Math.PI / 2; l.position.set(x, 0.01, -ROAD_LEN / 2 + 10); world.add(l);
  }
  const dashN = Math.floor(ROAD_LEN / 6);
  const dashes = new THREE.InstancedMesh(new THREE.PlaneGeometry(0.18, 2.2), new THREE.MeshBasicMaterial({ color: 0xd9a531 }), dashN);
  const m = new THREE.Matrix4(), q = new THREE.Quaternion().setFromEuler(new THREE.Euler(-Math.PI / 2, 0, 0));
  for (let i = 0; i < dashN; i++) { m.compose(new THREE.Vector3(0, 0.012, 10 - i * 6), q, new THREE.Vector3(1, 1, 1)); dashes.setMatrixAt(i, m); }
  world.add(dashes);
  // low concrete barriers along the edges (spaced out)
  const barN = Math.floor(ROAD_LEN / 9) * 2;
  const bars = new THREE.InstancedMesh(new THREE.BoxGeometry(0.5, 0.6, 3.2), mat(0x8c8a82), barN);
  let k = 0; for (let i = 0; i < barN / 2; i++) for (const s of [-1, 1]) { m.makeTranslation(s * (ROAD_HALF + 0.9), 0.3, 8 - i * 9); bars.setMatrixAt(k++, m); }
  bars.castShadow = true; bars.receiveShadow = true; world.add(bars);
  // a few distant ruins (few, muted)
  const ruinMat = mat(0x7b7b72, { roughness: 1 });
  for (let i = 0; i < 22; i++) {
    const side = i % 2 ? 1 : -1, w = 3 + (i * 7 % 5), h = 2 + (i * 13 % 7);
    const b = new THREE.Mesh(new THREE.BoxGeometry(w, h, 3 + (i * 3 % 4)), ruinMat);
    b.position.set(side * (16 + (i * 11 % 14)), h / 2, -i * 22 - 10); b.castShadow = true; world.add(b);
  }
}

// ---------- text sprites ----------
function textTexture(text, { bg = null, fg = '#fff', w = 256, h = 128, font = 'bold 72px system-ui, sans-serif', glow = null } = {}) {
  const c = document.createElement('canvas'); c.width = w; c.height = h; const g = c.getContext('2d');
  if (bg) { g.fillStyle = bg; g.fillRect(0, 0, w, h); }
  g.font = font; g.textAlign = 'center'; g.textBaseline = 'middle';
  if (glow) { g.shadowColor = glow; g.shadowBlur = 18; }
  g.fillStyle = fg; g.fillText(text, w / 2, h / 2 + 4);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}
function makeLabel(text, opts, scale = [2, 1]) {
  const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: textTexture(text, opts), depthTest: false, transparent: true }));
  s.scale.set(scale[0], scale[1], 1); s.renderOrder = 10; return s;
}
function setLabel(s, text, opts) { s.material.map.dispose(); s.material.map = textTexture(text, opts); s.material.needsUpdate = true; }

// ---------- squad (instanced soldiers + Rook) ----------
const soldierGeo = {
  body: new THREE.BoxGeometry(0.42, 0.5, 0.3).translate(0, 0.55, 0),
  legs: new THREE.BoxGeometry(0.36, 0.32, 0.24).translate(0, 0.16, 0),
  head: new THREE.SphereGeometry(0.19, 10, 8).translate(0, 0.98, 0),
  gun: new THREE.BoxGeometry(0.09, 0.09, 0.55).translate(0.16, 0.62, -0.3),
};
const sMats = { body: mat(COL.teal), legs: mat(COL.gun), head: mat(COL.tealDark, { metalness: 0.3, roughness: 0.4 }), gun: mat(0x1d2126) };
const soldierParts = Object.keys(soldierGeo).map(k => {
  const im = new THREE.InstancedMesh(soldierGeo[k], sMats[k], MAX_VISIBLE); im.castShadow = true; im.count = 0; im.frustumCulled = false; scene.add(im); return im;
});

function buildRook() {
  const g = new THREE.Group();
  const s = 1.35;
  const add = (geo, m, x, y, z) => { const me = new THREE.Mesh(geo, m); me.position.set(x, y, z); me.castShadow = true; g.add(me); return me; };
  add(new THREE.BoxGeometry(0.5, 0.55, 0.34), mat(COL.teal), 0, 0.6, 0);
  add(new THREE.BoxGeometry(0.42, 0.34, 0.28), mat(COL.gun), 0, 0.17, 0);
  add(new THREE.SphereGeometry(0.21, 12, 10), mat(COL.tealDark, { metalness: 0.4, roughness: 0.35 }), 0, 1.06, 0);
  const pad = mat(COL.orange);
  add(new THREE.BoxGeometry(0.2, 0.14, 0.34), pad, -0.33, 0.85, 0); add(new THREE.BoxGeometry(0.2, 0.14, 0.34), pad, 0.33, 0.85, 0);
  add(new THREE.BoxGeometry(0.1, 0.1, 0.7), mat(0x1d2126), 0.2, 0.66, -0.36);
  add(new THREE.BoxGeometry(0.5, 0.06, 0.36), pad, 0, 0.36, 0); // belt
  g.scale.setScalar(s);
  // shield wall (special)
  const shield = new THREE.Mesh(new THREE.BoxGeometry(1, 1.6, 0.12), new THREE.MeshStandardMaterial({ color: 0x38e0e0, emissive: 0x1aa0a0, transparent: true, opacity: 0.45 }));
  shield.visible = false; scene.add(shield);
  return { g, shield };
}
const rook = buildRook(); scene.add(rook.g);
const countLabel = makeLabel('1', { bg: null, fg: '#fff', font: 'bold 80px system-ui, sans-serif', glow: '#000' }, [1.8, 0.9]);
scene.add(countLabel);

// ---------- pools ----------
const bulletGeo = new THREE.BoxGeometry(0.07, 0.07, 0.7);
const bulletMat = new THREE.MeshBasicMaterial({ color: 0xffd36b });
const MAX_BULLETS = 400;
const bulletMesh = new THREE.InstancedMesh(bulletGeo, bulletMat, MAX_BULLETS); bulletMesh.frustumCulled = false; scene.add(bulletMesh);
bulletMesh.instanceColor = new THREE.InstancedBufferAttribute(new Float32Array(MAX_BULLETS * 3), 3);
const bullets = [];

const PMAX = 300;
const partMesh = new THREE.InstancedMesh(new THREE.SphereGeometry(0.12, 6, 4), new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.9 }), PMAX);
partMesh.instanceColor = new THREE.InstancedBufferAttribute(new Float32Array(PMAX * 3), 3);
partMesh.frustumCulled = false; scene.add(partMesh);
const parts = [];
function spawnPart(x, y, z, vx, vy, vz, life, color, size = 1) { if (parts.length < PMAX) parts.push({ x, y, z, vx, vy, vz, life, max: life, color: new THREE.Color(color), size }); }
function burst(x, y, z, n, color, sp = 3) { for (let i = 0; i < n; i++) spawnPart(x, y, z, (Math.random() - .5) * sp, Math.random() * sp, (Math.random() - .5) * sp, 0.4 + Math.random() * 0.3, color); }

// ---------- zombies ----------
const zGeo = {
  body: new THREE.BoxGeometry(0.6, 0.7, 0.4), legs: new THREE.BoxGeometry(0.5, 0.5, 0.3), head: new THREE.BoxGeometry(0.42, 0.42, 0.42),
  arm: new THREE.BoxGeometry(0.16, 0.16, 0.6), eye: new THREE.BoxGeometry(0.08, 0.06, 0.02),
};
const eyeMat = new THREE.MeshBasicMaterial({ color: 0xff3a2a });
function buildZombie(scale = 1) {
  const g = new THREE.Group(); const skin = mat(COL.skin), shirt = mat(COL.zshirt);
  const add = (geo, m, x, y, z) => { const me = new THREE.Mesh(geo, m); me.position.set(x, y, z); me.castShadow = true; g.add(me); return me; };
  add(zGeo.legs, mat(0x3e3a33), 0, 0.25, 0);
  const body = add(zGeo.body, shirt, 0, 0.85, 0);
  const head = add(zGeo.head, skin, 0, 1.42, 0.02);
  add(zGeo.eye, eyeMat, -0.1, 1.46, 0.22); add(zGeo.eye, eyeMat, 0.1, 1.46, 0.22);
  const armL = add(zGeo.arm, skin, -0.38, 1.05, 0.28), armR = add(zGeo.arm, skin, 0.38, 1.05, 0.28);
  g.scale.setScalar(scale); g.rotation.y = 0; // faces +Z (toward squad)
  return { g, mats: [skin, shirt], armL, armR, head, body };
}
const zombies = []; let boss = null;

// ---------- gates & crates ----------
const gates = [], crates = [];
function parseOp(s) { const n = parseFloat(s.replace(/[^\d.]/g, '')); const op = s[0]; return { op, n, good: op === '+' || op === 'x' }; }
function applyOp(count, o) { switch (o.op) { case '+': return count + o.n; case '-': return count - o.n; case 'x': return Math.round(count * o.n); case '÷': return Math.floor(count / o.n); } return count; }
function buildGate(def) {
  const z = -def.d; const grp = new THREE.Group(); world.add(grp);
  const sides = [];
  for (const [side, txt] of [[-1, def.left], [1, def.right]]) {
    const o = parseOp(txt);
    const color = o.good ? 0x2e8bff : 0xff3b3b;
    const panel = new THREE.Mesh(new THREE.PlaneGeometry(ROAD_HALF - 0.3, 2.2), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.35, side: THREE.DoubleSide, depthWrite: false }));
    panel.position.set(side * ROAD_HALF / 2, 1.4, z); grp.add(panel);
    const lbl = makeLabel(txt, { fg: '#fff', font: 'bold 96px system-ui, sans-serif', glow: o.good ? '#4aa8ff' : '#ff4040' }, [2.6, 1.3]);
    lbl.position.set(side * ROAD_HALF / 2, 1.5, z + 0.05); grp.add(lbl);
    const postMat = new THREE.MeshBasicMaterial({ color: o.good ? 0x7fc4ff : 0xff8a8a });
    for (const px of [side * 0.15, side * (ROAD_HALF - 0.15)]) { const p = new THREE.Mesh(new THREE.BoxGeometry(0.22, 2.8, 0.22), postMat); p.position.set(px, 1.4, z); grp.add(p); }
    const top = new THREE.Mesh(new THREE.BoxGeometry(ROAD_HALF - 0.1, 0.18, 0.22), postMat); top.position.set(side * ROAD_HALF / 2, 2.75, z); grp.add(top);
    sides.push({ side, op: o, txt, panel });
  }
  gates.push({ z, grp, sides, used: false });
}
const crateTex = { shotgun: textTexture('SHOTGUN', { bg: '#e8792b', fg: '#1b1b1b', font: 'bold 44px system-ui', w: 256, h: 64 }), flamethrower: textTexture('FLAME', { bg: '#e8792b', fg: '#1b1b1b', font: 'bold 50px system-ui', w: 256, h: 64 }) };
function buildCrate(def) {
  const g = new THREE.Group(); g.position.set(def.x, 0, -def.d);
  const box = new THREE.Mesh(new THREE.BoxGeometry(1.2, 1.2, 1.2), mat(0x4d5560, { emissive: 0x2a1406 }));
  box.position.y = 0.6; box.castShadow = true; g.add(box);
  const band = new THREE.Mesh(new THREE.BoxGeometry(1.24, 0.3, 1.24), mat(COL.orange, { emissive: 0x6a2a00 })); band.position.y = 0.6; g.add(band);
  const tag = new THREE.Sprite(new THREE.SpriteMaterial({ map: crateTex[def.weapon], depthTest: false })); tag.scale.set(1.8, 0.45, 1); tag.position.y = 1.7; tag.renderOrder = 9; g.add(tag);
  const hpLbl = makeLabel(String(def.hp), { fg: '#fff', font: 'bold 70px system-ui', glow: '#000' }, [0.9, 0.45]); hpLbl.position.y = 2.2; g.add(hpLbl);
  world.add(g);
  crates.push({ ...def, g, box, hpLbl, alive: true, maxHp: def.hp });
}

// ---------- game state ----------
const audio = new AudioManager();
const S = {};
function resetGame() {
  for (const z of zombies) world.remove(z.g); zombies.length = 0;
  for (const g of gates) world.remove(g.grp); gates.length = 0;
  for (const c of crates) world.remove(c.g); crates.length = 0;
  if (boss) { world.remove(boss.g); boss = null; }
  bullets.length = 0; parts.length = 0;
  Object.assign(S, {
    state: 'menu', dist: 0, x: 0, targetX: 0, count: LEVEL.startCount, shotAcc: 0, weapon: 'rifle', weaponT: 0,
    shieldT: 0, shieldCd: 0, time: 0, groanT: 3, formation: [], bossStarted: false, flameOn: false, shake: 0, kills: 0,
  });
  LEVEL.gates.forEach(buildGate);
  LEVEL.crates.forEach(buildCrate);
  for (const zg of LEVEL.zombies) {
    for (let i = 0; i < zg.n; i++) {
      const z = buildZombie(1);
      const a = i * 2.4, r = 0.7 * Math.sqrt(i);
      z.g.position.set(THREE.MathUtils.clamp(zg.x + Math.cos(a) * r, -ROAD_HALF + 0.5, ROAD_HALF - 0.5), 0, -zg.d + Math.sin(a) * r);
      world.add(z.g); zombies.push({ ...z, hp: zg.hp, maxHp: zg.hp, alive: true, phase: Math.random() * 6, hitT: 0 });
    }
  }
  ui.update();
}

// ---------- UI (DOM) ----------
const $ = id => document.getElementById(id);
const ui = {
  update() {
    $('progressFill').style.width = Math.min(100, S.dist / LEVEL.bossAt * 100) + '%';
    const w = $('weapon');
    if (S.weapon !== 'rifle') { w.style.display = 'flex'; $('weaponName').textContent = WEAPONS[S.weapon].label; $('weaponTime').style.width = (S.weaponT / PICKUP_DURATION * 100) + '%'; }
    else w.style.display = 'none';
    const sb = $('shieldBtn');
    sb.classList.toggle('cooling', S.shieldCd > 0);
    $('shieldCd').textContent = S.shieldT > 0 ? 'ACTIVE' : S.shieldCd > 0 ? Math.ceil(S.shieldCd) + 's' : 'READY';
    if (boss) { $('bossBar').style.display = 'block'; $('bossFill').style.width = Math.max(0, boss.hp / boss.maxHp * 100) + '%'; $('bossHp').textContent = Math.max(0, Math.ceil(boss.hp)); }
    else $('bossBar').style.display = 'none';
  },
  pop(text, good) {
    const e = document.createElement('div'); e.className = 'pop ' + (good ? 'good' : 'bad'); e.textContent = text;
    $('hud').appendChild(e); setTimeout(() => e.remove(), 900);
  },
};

function startGame() {
  audio.play('sfx_ui_click');
  $('menu').classList.add('hidden'); $('end').classList.add('hidden');
  if (S.state !== 'menu') resetGame();
  S.state = 'run';
  audio.startLoop('music_battle_loop', 'music', 1);
}
async function onStartTap(e) { e.preventDefault(); await audio.unlock(); startGame(); }
$('playBtn').addEventListener('click', onStartTap);
$('againBtn').addEventListener('click', onStartTap);
$('muteBtn').addEventListener('click', async e => { e.stopPropagation(); await audio.unlock(); audio.setMuted(!audio.muted); $('muteBtn').textContent = audio.muted ? '🔇' : '🔊'; });
$('shieldBtn').addEventListener('pointerdown', e => { e.stopPropagation(); activateShield(); });

function activateShield() {
  if (S.state !== 'run' || S.shieldCd > 0) return;
  S.shieldT = 4; S.shieldCd = 18; audio.play('sfx_gate_good', { rate: 0.7 }); audio.play('sfx_ui_click');
}

// ---------- input: drag to steer (touch + mouse) ----------
let dragging = false, dragStartX = 0, dragStartTarget = 0;
const canvas = renderer.domElement;
canvas.style.touchAction = 'none';
canvas.addEventListener('pointerdown', e => { dragging = true; dragStartX = e.clientX; dragStartTarget = S.targetX; canvas.setPointerCapture(e.pointerId); });
canvas.addEventListener('pointermove', e => {
  if (!dragging) return;
  const w = canvas.clientWidth; const dx = (e.clientX - dragStartX) / w;
  S.targetX = THREE.MathUtils.clamp(dragStartTarget + dx * ROAD_HALF * 2.4, -ROAD_HALF + 0.6, ROAD_HALF - 0.6);
});
const endDrag = () => { dragging = false; };
canvas.addEventListener('pointerup', endDrag); canvas.addEventListener('pointercancel', endDrag);
window.addEventListener('keydown', e => { if (e.key === 'ArrowLeft') S.targetX = Math.max(-ROAD_HALF + 0.6, S.targetX - 1.2); if (e.key === 'ArrowRight') S.targetX = Math.min(ROAD_HALF - 0.6, S.targetX + 1.2); if (e.key === ' ') activateShield(); });

// ---------- squad helpers ----------
function squadRadius() { return 0.42 * Math.sqrt(Math.min(S.count, MAX_VISIBLE)) + 0.45; }
function formationOffset(i) { // Rook is index 0 at front; soldiers fill a sunflower behind him
  if (i === 0) return [0, -0.2];
  const a = i * 2.39996, r = 0.42 * Math.sqrt(i);
  return [Math.cos(a) * r, Math.abs(Math.sin(a) * r) * 0.9 + 0.35 + (Math.sin(a) * r) * 0.2];
}
function loseSoldiers(n) {
  const before = S.count;
  S.count = Math.max(0, S.count - n);
  if (S.count < before) { audio.play('sfx_soldier_lost', { throttle: 0.06, detune: 0.2 }); const sq = squadPos(); burst(sq.x, 0.8, sq.z + 0.5, 6, 0x2fb3b3); }
  if (S.count <= 0) endGame(false);
}
function squadPos() { return { x: S.x, z: -S.dist }; }

// ---------- shooting ----------
function fire(dt) {
  const w = WEAPONS[S.weapon];
  const shooters = Math.min(S.count, MAX_VISIBLE);
  const sq = squadPos();
  if (S.weapon === 'flamethrower') {
    if (!S.flameOn) { audio.startLoop('sfx_flamethrower_loop', 'sfx', 0.55); S.flameOn = true; }
    const R = w.range + squadRadius() * 0.5;
    const dps = w.dps * (4 + shooters);
    const hitList = [...zombies.filter(z => z.alive), ...crates.filter(c => c.alive), ...(boss && boss.alive ? [boss] : [])];
    for (const t of hitList) {
      const p = t.g.position; const dz = sq.z - p.z, dx = p.x - sq.x;
      if (dz > -0.5 && dz < R && Math.abs(dx) < squadRadius() + 0.8 + dz * w.cone) damage(t, dps * dt);
    }
    for (let i = 0; i < 6; i++) {
      const ox = (Math.random() - .5) * squadRadius() * 1.6;
      spawnPart(sq.x + ox, 0.7, sq.z - squadRadius() * 0.3, ox * 0.6 + (Math.random() - .5) * 2, Math.random() * 1.2, -10 - Math.random() * 6, 0.55, Math.random() < .5 ? 0xff7a1a : 0xffc23a, 1.6);
    }
    return;
  }
  if (S.flameOn) { audio.stopLoop('sfx_flamethrower_loop'); S.flameOn = false; }
  S.shotAcc += shooters * w.rate * dt;
  let shots = Math.floor(S.shotAcc); S.shotAcc -= shots;
  if (shots > 0) audio.play(w.sfx, { throttle: S.weapon === 'rifle' ? 0.075 : 0.18, vol: S.weapon === 'rifle' ? 0.5 : 0.7, detune: 0.12 });
  while (shots-- > 0) {
    const i = Math.floor(Math.random() * Math.min(S.count, MAX_VISIBLE));
    const f = S.formation[i] || [sq.x, sq.z];
    for (let p = 0; p < w.pellets; p++) {
      if (bullets.length >= MAX_BULLETS) break;
      const ang = (Math.random() - 0.5) * 2 * w.spread;
      bullets.push({ x: f[0] + 0.2, z: f[1] - 0.5, vx: Math.sin(ang) * w.speed, vz: -Math.cos(ang) * w.speed, life: w.range / w.speed, dmg: w.dmg, color: w.color });
    }
  }
}
function damage(t, amount) {
  if (!t.alive) return;
  t.hp -= amount; t.hitT = 0.08;
  if (t.isCrate) { setLabel(t.hpLbl, String(Math.max(0, Math.ceil(t.hp))), { fg: '#fff', font: 'bold 70px system-ui', glow: '#000' }); }
  if (t.hp <= 0) kill(t);
}
function kill(t) {
  t.alive = false;
  const p = t.g.position;
  if (t === boss) { burst(p.x, 2, p.z, 60, 0x9fd04a, 8); burst(p.x, 2, p.z, 30, 0xff6030, 6); audio.play('sfx_boss_roar', { rate: 1.3, vol: 0.8 }); world.remove(t.g); S.winAt = S.time + 0.9; return; }
  if (t.weapon) { // crate
    audio.play('sfx_crate_open'); burst(p.x, 0.8, p.z, 24, 0xe8792b, 5); world.remove(t.g);
    S.weapon = t.weapon; S.weaponT = PICKUP_DURATION; ui.pop(WEAPONS[t.weapon].label + '!', true); return;
  }
  S.kills++; audio.play('sfx_zombie_hit', { throttle: 0.05, detune: 0.3, vol: 0.8 });
  burst(p.x, 1, p.z, 10, 0x8fb04a, 4); world.remove(t.g);
}

// ---------- boss ----------
function spawnBoss() {
  const z = buildZombie(3.2);
  z.g.position.set(0, 0, -LEVEL.bossAt - LEVEL.boss.startGap);
  z.mats[0].color.set(0x6f8f3a); z.mats[1].color.set(0x4a2e2a);
  // spikes / armor for silhouette
  for (let i = 0; i < 3; i++) { const sp = new THREE.Mesh(new THREE.ConeGeometry(0.1, 0.35, 5), mat(0xd8d0b8)); sp.position.set(-0.2 + i * 0.2, 1.75, -0.05); z.g.add(sp); }
  world.add(z.g);
  boss = { ...z, hp: LEVEL.boss.hp, maxHp: LEVEL.boss.hp, alive: true, phase: 0, hitT: 0, biteT: 0, roarT: 6 };
  audio.play('sfx_boss_roar'); S.shake = 0.6; ui.pop('BOSS!', false);
}

// ---------- end ----------
function endGame(win) {
  if (S.state !== 'run') return;
  S.state = win ? 'win' : 'lose';
  audio.stopLoop('music_battle_loop', 0.4); audio.stopLoop('sfx_flamethrower_loop'); S.flameOn = false;
  audio.play(win ? 'sting_victory' : 'sting_defeat');
  $('endTitle').textContent = win ? 'VICTORY' : 'DEFEAT';
  $('endTitle').className = win ? 'win' : 'lose';
  $('endStats').textContent = win ? `Outpost secured · ${S.count} survivors · ${S.kills} zombies down` : `Squad lost at ${Math.round(S.dist)}m · ${S.kills} zombies down`;
  S.overlayAt = S.time + 0.7;
}

// ---------- update ----------
const tmpM = new THREE.Matrix4(), tmpQ = new THREE.Quaternion(), tmpS = new THREE.Vector3(1, 1, 1), tmpP = new THREE.Vector3();
function demoSteer() {
  // look ahead: pick the better gate, else dodge zombies / aim at crates
  const g = gates.find(g => !g.used && g.z < -S.dist && g.z > -S.dist - 40);
  if (g) { const [L, R] = g.sides; S.targetX = applyOp(S.count, L.op) >= applyOp(S.count, R.op) ? -2.5 : 2.5; return; }
  const c = crates.find(c => c.alive && c.g.position.z < -S.dist && c.g.position.z > -S.dist - 25);
  S.targetX = c ? c.x * 0.8 : 0;
}

function update(dt) {
  S.time += dt;
  if (S.winAt && S.time >= S.winAt) { S.winAt = 0; endGame(true); }
  if (S.overlayAt && S.time >= S.overlayAt) { S.overlayAt = 0; $('end').classList.remove('hidden'); }
  if (S.state === 'run') {
    if (DEMO) demoSteer();
    // forward motion (stop at boss)
    if (S.dist < LEVEL.bossAt) S.dist = Math.min(LEVEL.bossAt, S.dist + LEVEL.speed * dt);
    else if (!S.bossStarted) { S.bossStarted = true; spawnBoss(); }
    S.x += (S.targetX - S.x) * Math.min(1, dt * 10);
    const sq = squadPos(), R = squadRadius();
    // gates
    for (const g of gates) if (!g.used && sq.z <= g.z) {
      g.used = true; const side = sq.x < 0 ? g.sides[0] : g.sides[1];
      const before = S.count; S.count = Math.max(0, applyOp(S.count, side.op));
      side.panel.material.opacity = 0.8; g.grp.children.forEach(c => { if (c.material && c !== side.panel) c.material.opacity = 0.15; });
      audio.play(side.op.good ? 'sfx_gate_good' : 'sfx_gate_bad');
      ui.pop(side.txt + '  →  ' + S.count, S.count >= before);
      if (S.count <= 0) endGame(false);
    }
    // weapon timer
    if (S.weapon !== 'rifle') { S.weaponT -= dt; if (S.weaponT <= 0) { S.weapon = 'rifle'; } }
    if (S.shieldT > 0) S.shieldT -= dt; if (S.shieldCd > 0) S.shieldCd -= dt;
    fire(dt);
    // zombies
    S.groanT -= dt;
    for (const z of zombies) {
      if (!z.alive) continue;
      const p = z.g.position; const dz = sq.z - p.z;
      z.phase += dt * 6;
      if (dz < 30 && dz > -3) { // shamble toward squad
        const dx = sq.x - p.x; const l = Math.hypot(dx, dz) || 1;
        p.x += dx / l * 1.6 * dt; p.z += dz / l * 1.6 * dt;
        z.g.rotation.y = Math.atan2(dx, dz);
        if (S.groanT <= 0) { audio.play('sfx_zombie_groan', { vol: 0.5, detune: 0.3 }); S.groanT = 2.5 + Math.random() * 2; }
      }
      z.armL.rotation.x = Math.sin(z.phase) * 0.25; z.armR.rotation.x = -Math.sin(z.phase) * 0.25;
      z.g.position.y = Math.abs(Math.sin(z.phase)) * 0.06;
      if (z.hitT > 0) { z.hitT -= dt; z.mats.forEach(m => m.emissive.setHex(0x883322)); } else z.mats.forEach(m => m.emissive.setHex(0));
      // contact
      if (Math.hypot(p.x - sq.x, p.z - (sq.z + 0.6)) < R + 0.3) {
        z.alive = false; world.remove(z.g); burst(p.x, 1, p.z, 10, 0x8fb04a, 4);
        if (S.shieldT > 0) { audio.play('sfx_zombie_hit'); S.kills++; } else { loseSoldiers(1); S.shake = 0.15; }
      }
      if (p.z > sq.z + 6) { z.alive = false; world.remove(z.g); }
    }
    // boss
    if (boss && boss.alive) {
      const p = boss.g.position; const gap = sq.z - p.z;
      boss.phase += dt * 3;
      boss.armL.rotation.x = Math.sin(boss.phase) * 0.35; boss.armR.rotation.x = -Math.sin(boss.phase) * 0.35;
      boss.g.rotation.y = Math.atan2(sq.x - p.x, gap) * 0.5;
      if (gap > R + 1.6) { p.z += LEVEL.boss.speed * dt; p.x += (sq.x - p.x) * 0.3 * dt; }
      else { boss.biteT -= dt; if (boss.biteT <= 0) { boss.biteT = 0.4; if (S.shieldT > 0) { burst(p.x, 1, p.z + 1.5, 8, 0x38e0e0); } else { loseSoldiers(Math.max(1, Math.ceil(S.count * 0.04))); S.shake = 0.25; } } }
      boss.roarT -= dt; if (boss.roarT <= 0) { boss.roarT = 7; audio.play('sfx_boss_roar', { vol: 0.7 }); S.shake = 0.35; }
      if (boss.hitT > 0) { boss.hitT -= dt; boss.mats.forEach(m => m.emissive.setHex(0x662211)); } else boss.mats.forEach(m => m.emissive.setHex(0));
    }
  }
  // bullets
  const targets = [...zombies.filter(z => z.alive), ...crates.filter(c => c.alive), ...(boss && boss.alive ? [boss] : [])];
  for (let i = bullets.length - 1; i >= 0; i--) {
    const b = bullets[i]; b.x += b.vx * dt; b.z += b.vz * dt; b.life -= dt;
    let hit = false;
    for (const t of targets) {
      if (!t.alive) continue; const p = t.g.position;
      const r = t === boss ? 1.6 : t.weapon ? 0.75 : 0.45;
      if (Math.abs(p.x - b.x) < r && Math.abs(p.z - b.z) < r + 0.3) { damage(t, b.dmg); spawnPart(b.x, 1, b.z, 0, 1, 1, 0.15, 0xfff0a0); hit = true; break; }
    }
    if (hit || b.life <= 0) bullets.splice(i, 1);
  }
}

// ---------- render sync ----------
const clock = new THREE.Clock();
function sync(dt) {
  const sq = squadPos();
  // formation
  const vis = Math.min(S.count, MAX_VISIBLE);
  while (S.formation.length < vis) { const o = formationOffset(S.formation.length); S.formation.push([sq.x + o[0], sq.z + o[1] + 2]); }
  S.formation.length = vis;
  const running = S.state === 'run' && S.dist < LEVEL.bossAt;
  for (let i = 0; i < vis; i++) {
    const o = formationOffset(i), f = S.formation[i];
    const tx = THREE.MathUtils.clamp(sq.x + o[0], -ROAD_HALF + 0.3, ROAD_HALF - 0.3), tz = sq.z + o[1];
    f[0] += (tx - f[0]) * Math.min(1, dt * 8); f[1] += (tz - f[1]) * Math.min(1, dt * 8);
    if (i === 0) continue; // Rook rendered separately
    const bob = (running || S.state === 'run') ? Math.abs(Math.sin(S.time * 12 + i)) * 0.07 : 0;
    tmpP.set(f[0], bob, f[1]); tmpQ.identity();
    tmpM.compose(tmpP, tmpQ, tmpS);
    for (const im of soldierParts) im.setMatrixAt(i - 1, tmpM);
  }
  for (const im of soldierParts) { im.count = Math.max(0, vis - 1); im.instanceMatrix.needsUpdate = true; }
  const rf = S.formation[0] || [sq.x, sq.z];
  rook.g.visible = S.count > 0;
  rook.g.position.set(rf[0], Math.abs(Math.sin(S.time * 11)) * 0.06, rf[1]);
  rook.shield.visible = S.shieldT > 0;
  if (rook.shield.visible) { const w = squadRadius() * 2 + 0.6; rook.shield.scale.set(w, 1, 1); rook.shield.position.set(sq.x, 0.8, sq.z - 0.9 - 0.2); rook.shield.material.opacity = 0.35 + Math.sin(S.time * 20) * 0.1; }
  countLabel.position.set(sq.x, 2.6 + squadRadius() * 0.3, sq.z + 0.3);
  if (countLabel.userData.n !== S.count) { countLabel.userData.n = S.count; setLabel(countLabel, String(S.count), { fg: '#ffffff', font: 'bold 80px system-ui, sans-serif', glow: '#0a2a2a' }); }
  // crates bob
  for (const c of crates) if (c.alive) { c.box.rotation.y += dt * 0.8; if (c.hitT > 0) { c.hitT -= dt; c.box.material.emissive.setHex(0xaa5500); } else c.box.material.emissive.setHex(0x2a1406); }
  // bullets
  for (let i = 0; i < bullets.length; i++) {
    const b = bullets[i]; tmpP.set(b.x, 0.75, b.z); tmpQ.setFromAxisAngle(THREE.Object3D.DEFAULT_UP, Math.atan2(b.vx, b.vz));
    tmpM.compose(tmpP, tmpQ, tmpS); bulletMesh.setMatrixAt(i, tmpM); bulletMesh.instanceColor.setXYZ(i, ...new THREE.Color(b.color).toArray());
  }
  bulletMesh.count = bullets.length; bulletMesh.instanceMatrix.needsUpdate = true; bulletMesh.instanceColor.needsUpdate = true;
  // particles
  for (let i = parts.length - 1; i >= 0; i--) { const p = parts[i]; p.life -= dt; if (p.life <= 0) { parts.splice(i, 1); continue; } p.x += p.vx * dt; p.y += p.vy * dt; p.z += p.vz * dt; p.vy -= 6 * dt; if (p.y < 0.05) { p.y = 0.05; p.vy *= -0.3; } }
  for (let i = 0; i < parts.length; i++) {
    const p = parts[i]; const s = p.size * (0.4 + p.life / p.max);
    tmpP.set(p.x, p.y, p.z); tmpM.compose(tmpP, tmpQ.identity(), new THREE.Vector3(s, s, s)); partMesh.setMatrixAt(i, tmpM);
    partMesh.instanceColor.setXYZ(i, p.color.r, p.color.g, p.color.b);
  }
  partMesh.count = parts.length; partMesh.instanceMatrix.needsUpdate = true; partMesh.instanceColor.needsUpdate = true;
  // camera
  let shakeX = 0, shakeY = 0; if (S.shake > 0) { S.shake -= dt; shakeX = (Math.random() - .5) * S.shake; shakeY = (Math.random() - .5) * S.shake; }
  camera.position.set(sq.x * 0.35 + shakeX, 15 + shakeY, sq.z + 15.5);
  camera.lookAt(sq.x * 0.3, 0, sq.z - 5);
  sun.position.set(sq.x - 8, 20, sq.z + 6); sun.target.position.set(sq.x, 0, sq.z - 4);
  ui.update();
}

function loop() {
  const dt = Math.min(clock.getDelta(), 1 / 20);
  step(dt);
  renderer.render(scene, camera);
  requestAnimationFrame(loop);
}
function step(dt) { update(dt); sync(dt); }

// crates participate in damage(): mark them
const _push = crates.push.bind(crates);
crates.push = (...items) => { items.forEach(c => { c.isCrate = true; c.hitT = 0; }); return _push(...items); };

resetGame();
if (params.has('autostart')) { $('menu').classList.add('hidden'); S.state = 'run'; }
requestAnimationFrame(loop);

// test / screenshot hook: advance simulation deterministically
window.__oz = {
  S, LEVEL, audio, get boss() { return boss; },
  advance(seconds, fps = 30) { const dt = 1 / fps; for (let t = 0; t < seconds; t += dt) step(dt); renderer.render(scene, camera); },
  start() { $('menu').classList.add('hidden'); S.state = 'run'; },
  shield: activateShield,
};
