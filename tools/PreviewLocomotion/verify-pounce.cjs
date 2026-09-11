const assert=require('node:assert/strict'),test=require('node:test'),fs=require('node:fs');
const file=__dirname+'/pounce-motion.js';
const motion=fs.existsSync(file)?require(file):null;
test('higher inverted U preserves horizontal easing and flight duration',()=>{
 const s=motion.create();s.step(1.5,true,100);
 let p=s.step(.085,true,100);
 assert.ok(Math.abs(p.x-4.375)<1e-8);assert.ok(Math.abs(p.y+9)<1e-8);
 p=s.step(.085,true,100);
 assert.ok(Math.abs(p.x-14)<1e-8);assert.ok(Math.abs(p.y+12)<1e-8);
 p=s.step(.17,true,100);
 assert.equal(p.phase,'landing');assert.equal(p.x,28);assert.equal(p.y,0);
});
test('continuous nearby dwell resets on early departure',()=>{
 assert.ok(motion,'pounce controller must exist');
 const s=motion.create();let p;
 for(let i=0;i<14;i++)p=s.step(.1,true,-100);
 assert.equal(p.phase,'watch');
 p=s.step(.1,false,-100);assert.equal(p.dwell,0);
 for(let i=0;i<14;i++)p=s.step(.1,true,-100);
 assert.equal(p.phase,'watch');
 p=s.step(.1,true,-100);assert.equal(p.phase,'flight');
});
test('hop keeps takeoff direction and lands without teleport or overshoot',()=>{
 assert.ok(motion,'pounce controller must exist');
 const s=motion.create();let p=s.step(1.5,true,20),previous=p.x;
 assert.equal(p.phase,'flight');
 let raised=false,compressed=false;
 for(let i=0;i<120;i++){
  p=s.step(1/120,true,-100);
  if(p.phase!=='track')assert.equal(p.direction,1);assert.ok(p.x>=previous-1e-9&&p.x<=12);
  assert.ok(p.x-previous<1);assert.ok(p.y<=0);
  raised ||= p.y < -5;compressed ||= p.scaleY<.98;
  previous=p.x;
 }
 assert.ok(raised);assert.ok(compressed);assert.equal(p.phase,'track');
 assert.equal(p.x,12);assert.equal(p.y,0);assert.equal(p.scaleY,1);assert.equal(p.angle,0);
});
test('timer and trajectory are independent of frame subdivisions',()=>{
 assert.ok(motion,'pounce controller must exist');
 const a=motion.create(),b=motion.create();let p,q;
 p=a.step(1.67,true,-100);
 for(let i=0;i<167;i++)q=b.step(.01,true,-100);
 for(const key of ['x','y','age','dwell'])assert.ok(Math.abs(p[key]-q[key])<1e-8,key);
 assert.equal(p.phase,q.phase);
});
test('nonfinite input cannot arm a jump or poison pose',()=>{
 assert.ok(motion,'pounce controller must exist');
 const s=motion.create();
 for(const dt of [-1,NaN,Infinity])assert.throws(()=>s.step(dt,true,10));
 const p=s.step(3,true,NaN);assert.equal(p.count,0);assert.equal(p.dwell,0);
});
test('landing completion starts three seconds of stationary tracking, then fresh dwell',()=>{
 const s=motion.create();s.step(1.5,true,-100);let p=s.step(.62,true,-100);
 assert.equal(p.phase,'track');assert.equal(p.dwell,0);
 p=s.step(2.99,true,100);
 assert.equal(p.phase,'track');assert.equal(p.direction,1);assert.equal(p.x,-28);
 assert.equal(p.y,0);assert.equal(p.angle,0);assert.equal(p.scaleY,1);
 p=s.step(.01,true,100);assert.equal(p.phase,'watch');assert.equal(p.dwell,0);
 p=s.step(1.49,true,100);assert.equal(p.count,1);
 p=s.step(.01,true,100);assert.equal(p.phase,'flight');assert.equal(p.count,2);
});
test('tracking follows distant pointer but does not prepare until nearby',()=>{
 const s=motion.create();s.step(1.5,true,-100);s.step(.62,true,-100);
 let p=s.step(1,false,100);assert.equal(p.phase,'track');assert.equal(p.direction,1);assert.equal(p.x,-28);
 p=s.step(2,false,100);assert.equal(p.phase,'watch');assert.equal(p.dwell,0);
 p=s.step(10,false,100);assert.equal(p.count,1);assert.equal(p.dwell,0);
 p=s.step(1.5,true,100);assert.equal(p.count,2);
});
test('large and subdivided steps cross tracking boundary identically',()=>{
 const a=motion.create(),b=motion.create();let p=a.step(5.3,true,-100),q;
 for(let i=0;i<530;i++)q=b.step(.01,true,-100);
 assert.equal(p.phase,'watch');assert.equal(q.phase,p.phase);
 assert.ok(Math.abs(p.dwell-.18)<1e-8);assert.ok(Math.abs(q.dwell-p.dwell)<1e-8);
});
