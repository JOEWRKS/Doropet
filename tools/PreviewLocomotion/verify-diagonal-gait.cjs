const assert=require('node:assert/strict');
const rig=require('./rig.js');
const eps=1e-8;
const displacement=(pose,i)=>pose.legs[i].tip.map((v,k)=>v-rig.rest[i].tip[k]);
const same=(a,b)=>a.every((v,k)=>Math.abs(v-b[k])<eps);
// On this left-facing three-quarter art: far fore, near fore, near hind,
// far hind. A same-side phase assignment must fail the diagonal pair tests.
assert.equal(rig.pose({walk:1}).legs.length,4,'walking must articulate the occluded fourth leg');
for(const amplitude of [.125,.5,1])for(let d=0;d<10;d+=.125){
  const p=rig.pose({walk:amplitude,distance:d});
  assert.ok(same(displacement(p,0),displacement(p,2)),`far fore / near hind unpaired at ${d}`);
  assert.ok(same(displacement(p,1),displacement(p,3)),`near fore / far hind unpaired at ${d}`);
  const opposite=rig.pose({walk:amplitude,distance:d+5});
  assert.ok(same(displacement(p,0),displacement(opposite,1)),`pairs are not half a cycle apart at ${d}`);
}
const swing=rig.pose({walk:1,distance:8});
assert.ok(displacement(swing,0)[1]<-2&&displacement(swing,2)[1]<-2,'first diagonal pair must lift together');
assert.equal(displacement(swing,1)[1],0,'opposite forepaw must remain planted');
assert.equal(displacement(swing,3)[1],0,'opposite hindpaw must remain planted');
for(const boundary of [0,1,5,6,10]){
  const a=rig.pose({walk:1,distance:boundary-1e-7}),b=rig.pose({walk:1,distance:boundary+1e-7});
  for(let i=0;i<4;i++)assert.ok(Math.hypot(...a.legs[i].tip.map((v,k)=>v-b.legs[i].tip[k]))<.0001,'diagonal cycle has a discontinuity');
}
for(const d of [0,3,8])for(let i=0;i<4;i++)assert.deepEqual(rig.pose({distance:d,walk:0}).legs[i].tip,rig.rest[i].tip,'stopped gait retains a foot offset');
for(let i=0;i<4;i++){
  const distance=1.5+(i%2?5:0),a=rig.pose({walk:1,distance}),b=rig.pose({walk:1,distance:distance+.02});
  assert.ok(Math.abs((a.legs[i].tip[0]-distance)-(b.legs[i].tip[0]-distance-.02))<eps,`stance foot ${i} slides in world space`);
  assert.equal(a.legs[i].tip[1],rig.rest[i].tip[1],`stance foot ${i} leaves its ground plane`);
  for(let d=0;d<10;d+=.25)for(const dx of [-4,0,4]){
    const p=rig.pose({walk:1,distance:d}),x=rig.rest[i].root[0]+dx,y=rig.rest[i].root[1]-2;
    assert.ok(same(rig.limb(x,y,i,p),rig.trunk(x,y,p)),`attachment ${i} separates from trunk at ${d}`);
  }
}
console.log('PASS: four articulated legs, diagonal pairs, opposite support, cycle continuity, stopped offsets and all-four attachment/stance contact');
