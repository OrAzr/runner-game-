// Level layout. Distances are units along the road (squad runs toward -Z).
export const LEVEL = {
  speed: 6.5,          // squad forward speed (units/s)
  bossAt: 400,         // distance where the squad stops for the boss fight
  startCount: 1,       // starts with Rook only
  gates: [
    { d: 28,  left: '+5',  right: '+3' },
    { d: 75,  left: 'x2',  right: '-4' },
    { d: 130, left: '+8',  right: 'x2' },
    { d: 190, left: '-10', right: '+15' },
    { d: 255, left: '+10', right: '-5' },
    { d: 320, left: '÷2',  right: '+20' },
    { d: 365, left: 'x2',  right: '+12' },
  ],
  zombies: [ // small, spaced-out groups
    { d: 55,  x: 0,    n: 3, hp: 3 },
    { d: 105, x: -2,   n: 5, hp: 4 },
    { d: 160, x: 1.5,  n: 6, hp: 5 },
    { d: 225, x: 0,    n: 8, hp: 6 },
    { d: 290, x: -1.5, n: 9, hp: 8 },
    { d: 345, x: 1,    n: 10, hp: 10 },
  ],
  crates: [
    { d: 95,  x: 2.6,  weapon: 'shotgun', hp: 6 },
    { d: 210, x: -2.6, weapon: 'flamethrower', hp: 10 },
    { d: 300, x: 2.6,  weapon: 'shotgun', hp: 14 },
  ],
  boss: { hp: 2200, speed: 1.1, startGap: 30 },
};

export const WEAPONS = {
  rifle:        { label: 'ASSAULT RIFLE', rate: 2.2, dmg: 1,   range: 24, speed: 38, pellets: 1, spread: 0.02, sfx: 'sfx_rifle',   color: 0xffd36b },
  shotgun:      { label: 'SHOTGUN',       rate: 0.9, dmg: 1.3, range: 14, speed: 32, pellets: 5, spread: 0.32, sfx: 'sfx_shotgun', color: 0xffa040 },
  flamethrower: { label: 'FLAMETHROWER',  dps: 2.2,  range: 8, cone: 0.55, sfx: 'sfx_flamethrower_loop' },
};
export const PICKUP_DURATION = 10;
