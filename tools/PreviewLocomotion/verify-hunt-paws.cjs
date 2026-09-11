// Catches flattened pointed toes produced by shearing the entire paw to its
// distal end. Measure solid visible toe mass, not just its bounding box.
const assert=require('node:assert/strict'),hunt=require('./hunt-motion.js');
const {create}=require('./four-leg-harness.cjs');
// A reach concentrated in a five-pixel wrist makes an S-shaped hooked ankle,
// even when the distal pad has enough alpha mass. Bound that local shear
// across both forelegs, throughout entry and the held pose.
for(const t of [.4,.55,.7,1,1.25,1.65])for(const x of [20,23,36,40,43])for(let y=65;y<86;y+=.25){
 const a=hunt.map(x,y,hunt.sample(t)),b=hunt.map(x,y+.01,hunt.sample(t));
 assert.ok(Math.abs((b[0]-a[0])/.01)<1.7,'foreleg reach must not bend into a narrow hooked wrist');
}
(async()=>{
 const {renderer,rig}=await create();rig.body=(x,y,p)=>hunt.map(x,y,p.hunt);
 const render=t=>{const p=rig.pose();p.hunt=hunt.sample(t);p.bob=p.hunt.headBob;p.roll=p.hunt.headRoll;return renderer.render(p,{bodyOnly:true}).getContext('2d').getImageData(0,0,256,256).data;};
 const data=render(1);
 const mass=(x,top,bottom)=>{let n=0;for(let y=(top+16)*2;y<(bottom+16)*2;y++)n+=data[(y*256+(x+16)*2)*4+3]/510;return n;};
 assert.ok(mass(10,75,83)>5.3,'far toe must have a broad rounded tip, not a narrow spike');
 // Follow only the bottom connected toe interval, so lifting its rounded
 // upper edge above y80 is counted without including the separate torso.
 let toeMass=0,started=false;for(let y=(89+16)*2;y>=(75+16)*2;y--){const alpha=data[(y*256+(29+16)*2)*4+3];if(alpha>8)started=true;else if(started)break;if(started)toeMass+=alpha/510;}
 assert.ok(toeMass>5,'near toe must stay plump instead of flattening along the floor');
 // Original reach and floor anchors remain unchanged by the rounder shape.
 for(const [source,target] of [[[23,79],[12,79]],[[43,85],[32,85]]]){
  const p=hunt.map(...source,hunt.sample(1));assert.ok(Math.hypot(p[0]-target[0],p[1]-target[1])<.001);
 }
 const a=render(1.25),b=render(1.65);for(let i=0;i<a.length;i++)assert.ok(Math.abs(a[i]-b[i])<=1,'new toe art pops across the held-loop seam');
 console.log('PASS plump toe coverage, preserved reach/floor anchors and loop seam');
})().catch(e=>{console.error(e);process.exitCode=1});
