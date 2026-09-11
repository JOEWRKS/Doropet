const assert=require('node:assert/strict');
const {pose,cycle,scenario,limb,trunk,rest}=require('./rig.js');
// A rotating cut-out moves the cut edges away from the torso. The full width
// of each attachment (not only its center) must share the torso transform.
for(let distance=0;distance<10;distance+=.25){
  const p=pose({distance,walk:1});
  for(let i=0;i<3;i++)for(const dx of [-4,0,4]){
    const x=rest[i].root[0]+dx,y=rest[i].root[1]-2;
    const a=limb(x,y,i,p),b=trunk(x,y,p);
    assert.ok(Math.hypot(a[0]-b[0],a[1]-b[1])<1e-8,`paw ${i} attachment opens at ${distance}`);
  }
}
// Test the actual UI timeline, not just the steady-state gait cycle.
for(const t of [.4,3.2,3.8,4.2,4.8,6.8,7.4,8,16]){
  const a=scenario(t-1e-7),b=scenario(t+1e-7),pa=pose(a),pb=pose(b);
  assert.ok(Math.abs(a.position-b.position)<.0001,'timeline position jumps');
  for(let i=0;i<3;i++)assert.ok(Math.hypot(...pa.legs[i].tip.map((x,k)=>x-pb.legs[i].tip[k]))<.0001,`timeline paw jumps at ${t}`);
}
// A stance foot must not slide in world space as the body travels forward.
for(const phase of [.08,.18,.28]){
  const distance=phase*cycle;
  const a=pose({distance,walk:1,sit:0}),b=pose({distance:distance+.02,walk:1,sit:0});
  assert.ok(Math.abs((a.legs[0].tip[0]-distance)-(b.legs[0].tip[0]-distance-.02))<1e-8,'planted forepaw slides');
  assert.equal(a.legs[0].tip[1],79,'stance paw leaves its authored ground height');
}
// A sit bends the rear, not all three legs or the entire sprite.
const stand=pose({}),sit=pose({sit:1});
for(const point of [[70,48],[64,59],[58,59]])assert.deepEqual(require('./rig.js').trunk(...point,sit),point,'head attachment tears during sitting');
for(let i=0;i<2;i++)assert.deepEqual(sit.legs[i].tip,stand.legs[i].tip,'front paw must stay planted');
assert.ok(sit.legs[2].root[1]-stand.legs[2].root[1]>4,'rump must lower');
assert.ok(sit.legs[2].tip[0]<sit.legs[2].root[0]-5,'rear paw must tuck beneath the rump');
assert.ok(sit.legs[2].tip[1]-sit.legs[2].root[1]<6,'rear leg remains standing');
for(const distance of [0,cycle-1e-7,cycle,cycle+1e-7]){
  const a=pose({distance,walk:1}),b=pose({distance:distance+1e-7,walk:1});
  for(let i=0;i<3;i++)assert.ok(Math.hypot(...a.legs[i].tip.map((x,k)=>x-b.legs[i].tip[k]))<.0001,'gait seam jumps');
}
assert.deepEqual(pose({distance:123,walk:0,sit:0}).legs,stand.legs,'stop retains a walking offset');
console.log('PASS: stance contact, planted forepaws while sitting, folded rear, gait and actual timeline continuity');
