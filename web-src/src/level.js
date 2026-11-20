// Level layout. Distances are units along the road (squad runs toward -Z).
// A gate string starting with # is a shootable counter: "#-18" begins at -18 and
// each bullet adds to it. Plain "+N" / "xN" / "-N" / "÷N" are instant on pass.
export const LEVEL = {
  speed: 6.5,          // squad forward speed (units/s)
  bossAt: 400,         // distance where the squad stops for the boss fight
  startCount: 1,       // starts with Rook only
  gates: [
    { d: 20,  left: '+4',   right: '+3' },
    { d: 48,  left: '#-20', right: '#-14' },
    { d: 100, left: 'x2',   right: '#-30' },
    { d: 155, left: '#-34', right: '+12' },
    { d: 210, left: '#-18', right: '#-40' },
    { d: 268, left: 'x2',   right: '#-42' },
    { d: 325, left: '#-32', right: '+16' },
    { d: 372, left: '#-24', right: 'x2' },
  ],
  zombies: [ // just past the gates, so the approach stays a shooting lane
    { d: 56,  x: 0,    n: 4, hp: 3 },
    { d: 118, x: -2.2, n: 5, hp: 4 },
    { d: 175, x: 2,    n: 5, hp: 5 },
    { d: 238, x: -1.6, n: 6, hp: 6 },
    { d: 295, x: 1.8,  n: 6, hp: 7 },
    { d: 348, x: 0,    n: 6, hp: 8 },
  ],
  crates: [
    { d: 90,  x: 3.3,  weapon: 'shotgun', hp: 6 },
    { d: 198, x: -3.3, weapon: 'flamethrower', hp: 10 },
    { d: 292, x: 3.3,  weapon: 'shotgun', hp: 14 },
  ],
  boss: { hp: 2200, speed: 1.1, startGap: 30 },
};

export const WEAPONS = {
  rifle:        { label: 'ASSAULT RIFLE', rate: 2.2, dmg: 1,   range: 24, speed: 42, pellets: 1, spread: 0.035, gate: 1, sfx: 'sfx_rifle',   color: 0xffe08a },
  shotgun:      { label: 'SHOTGUN',       rate: 0.95, dmg: 1.3, range: 14, speed: 34, pellets: 5, spread: 0.28, gate: 2, sfx: 'sfx_shotgun', color: 0xffa040 },
  flamethrower: { label: 'FLAMETHROWER',  dps: 2.2,  range: 9, cone: 0.55, gateRate: 22, sfx: 'sfx_flamethrower_loop' },
};
export const PICKUP_DURATION = 10;
