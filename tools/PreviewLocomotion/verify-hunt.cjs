// Catches absent crouch, moving feet, mesh folds, abrupt transitions and
// loop seams and departure transitions. The finite source clip remains a
// one-shot; the session now loops its settled wiggle while prey stays near.
const assert=require('node:assert/strict'),fs=require('node:fs');
const rig=require('./rig.js');
const hunt=fs.existsSync(__dirname+'/hunt-motion.js')?require('./hunt-motion.js'):{
  sample:()=>({amount:0,sway:0}),map:(x,y)=>rig.body(x,y,rig.pose())
};
assert.ok(hunt.sample(1).amount>.8,'nearby prey must lower the forebody');
for(const t of [0,2.6,8])assert.equal(hunt.sample(t).amount,0,'return fully to original art');
for(let t=0;t<=2.6;t+=1/120){
  const p=hunt.sample(t),next=hunt.sample(t+1/120);
  for(const [x,y] of [[66,82],[54.5,78.5]]){
    const moved=hunt.map(x,y,p);assert.ok(Math.hypot(moved[0]-x,moved[1]-y)<.001,'planted paw must not slide');
  }
  for(const [x,y] of [[23,79],[43,85]]){
    const moved=hunt.map(x,y,p);assert.ok(Math.abs(moved[1]-y)<.001,'reaching forepaw must stay on the floor');
    assert.ok(moved[0]<=x+.001,'forepaws must reach forward, never backward');
    if(p.amount>.99)assert.ok(moved[0]<x-8,'forepaws must visibly extend in front of the chest');
  }
  for(let y=45;y<86;y+=2)for(let x=15;x<80;x+=2){
    const a=hunt.map(x,y,p),b=hunt.map(x+.1,y,p),c=hunt.map(x,y+.1,p),d=hunt.map(x,y,next);
    assert.ok((b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])>0,'no inverted body triangles');
    assert.ok(Math.hypot(d[0]-a[0],d[1]-a[1])<.6,'no frame-boundary jump');
  }
}
const crouch=hunt.sample(.8);
assert.ok(hunt.map(30,62,crouch)[1]>68,'front must lower farther into the stalking pose');
for(const [root,tip] of [[[22,71],[23,79]],[[39,77],[43,85]]]){
 const a=hunt.map(...root,crouch),b=hunt.map(...tip,crouch);
 assert.ok(b[0]<a[0]-5,'forelegs must extend diagonally forward rather than tuck under the chest');
}
assert.ok(hunt.map(70,62,crouch)[1]<62,'rump stays raised');
assert.ok(hunt.head(43,48,crouch)[1]>54,'head must follow the deeper shoulder lowering');
for(const tip of [[23,79],[43,85]]){
 const anchor=hunt.map(...tip,hunt.sample(1.25));
 for(let t=1.25;t<=1.65;t+=1/120){const p=hunt.map(...tip,hunt.sample(t));assert.ok(Math.hypot(p[0]-anchor[0],p[1]-anchor[1])<.001,'extended forepaws slide during held wiggle');}
}
assert.ok(hunt.sample(1).sway*hunt.sample(1.2).sway<0,'rump sways both ways');
const session=hunt.createSession();
assert.equal(session.update(0,true),0);
assert.equal(session.update(1,true),1);
for(const now of [4,10,30,120])assert.equal(hunt.sample(session.update(now,true)).amount,1,'nearby prey must keep the hunting pose indefinitely');
const swings=[];for(let t=120;t<120.4;t+=1/60)swings.push(hunt.sample(session.update(t,true)).sway);
assert.ok(Math.max(...swings)>1.35&&Math.min(...swings)<-1.35,'sway must remain bidirectional at 1.5x amplitude, not freeze after entry');
const departure=120.417;let previous=hunt.sample(session.update(departure,true));
for(let now=departure;now<departure+2;now+=1/120){
 const current=hunt.sample(session.update(now,false));
 assert.ok(Math.abs(current.sway-previous.sway)<.25,'departure or loop boundary snaps pelvis');
 assert.ok(Math.abs(current.amount-previous.amount)<.04,'departure snaps the crouched body');previous=current;
}
assert.equal(previous.amount,0,'departing prey must release the pose');assert.equal(Math.abs(previous.sway),0);
assert.equal(session.update(124,true),0,'returning prey rearms after settling');
const looping=hunt.createSession();let prior=hunt.sample(looping.update(0,true));
for(let t=1/120;t<20;t+=1/120){const current=hunt.sample(looping.update(t,true));assert.ok(Math.abs(current.sway-prior.sway)<.25,'held-loop seam snaps');prior=current;}
for(const leave of [.2,.8,3.001,3.12,3.27,3.39]){
 const s=hunt.createSession();s.update(0,true);let last=hunt.sample(s.update(leave,true));
 for(let t=leave;t<leave+3;t+=1/120){const p=hunt.sample(s.update(t,false));assert.ok(Math.abs(p.sway-last.sway)<.25,'phase-dependent departure snaps');last=p;}
 assert.equal(last.amount,0,'departure failed to settle');
}
console.log('PASS hunt: crouch, planted paws, mesh, continuity, stronger sustained wiggle and smooth departure');
