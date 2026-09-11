// Catches reusing stationary edge donors, and a head/body split without a
// moving back connection. Points were inspected on the source and -20deg proof.
const assert=require('node:assert/strict'),{create}=require('./four-leg-harness.cjs');
const compose=require('./gaze-compose.js'),eyes=require('./gaze-eyes.js');
const hunt=require('./hunt-motion.js');
(async()=>{
 const {renderer,rig,canvas}=await create(),make=(w,h)=>canvas.createCanvas(w,h);
 const body=make(256,256),bc=body.getContext('2d');bc.drawImage(renderer.render(rig.pose(),{bodyOnly:true}),0,0);
 const pixels=bc.getImageData(0,0,256,256).data;
 assert.equal(pixels[(127*256+175)*4+3],0,'old exterior donor at upper rump must not survive in isolated body');
 const head=make(96,96),hc=head.getContext('2d'),hd=hc.createImageData(96,96),part=renderer.parts[4];
 for(let i=0;i<part.length;i+=4){hd.data[i+3]=part[i+3];for(let c=0;c<3;c++)hd.data[i+c]=part[i+3]?part[i+c]*255/part[i+3]:0}hc.putImageData(hd,0,0);
 const comp=compose.create(body,head,eyes.create(head,make),make);
 const neutral=comp.render(0,0,{eyeX:0,eyeY:0,roll:0}).getContext('2d').getImageData(0,0,256,256).data;
 const rearEdge=y=>{let edge=0;const row=Math.round((y+16)*2);for(let x=164;x<198;x++)if(neutral[(row*256+x)*4+3]>=128)edge=x;return edge/2-16;};
 // A rounded haunch has a visible outward apex between its shoulders, not
 // the former long vertical side with a separately curved cap on top.
 assert.ok(rearEdge(60)-(rearEdge(54)+rearEdge(66))/2>=1.25,'rump remains flat-sided instead of a continuous rounded haunch');
 assert.ok(rearEdge(62)>rearEdge(58),'haunch apex is too high and pointed instead of broad and rounded');
 const d=comp.render(0,0,{eyeX:0,eyeY:0,roll:-Math.PI/9}).getContext('2d').getImageData(0,0,256,256).data;
 assert.ok(d[(128*256+166)*4+3]>245,'back must remain filled between rotated ribbon and rump, native(67,48)');
 // Each horizontal row across the exposed back must contain connected ink,
 // not a blank white fill or a floating cap at the old attachment.
 let previous=null;
 for(let y=121;y<=143;y++){
  let edge=-1;for(let x=157;x<189;x++){const i=(y*256+x)*4;if(d[i+3]>110)edge=x;}
  assert.ok(edge>=0,`missing back silhouette row ${y}`);
  let ink=0;for(let x=edge-3;x<=edge;x++){const i=(y*256+x)*4;ink+=(255-Math.min(d[i],d[i+1],d[i+2]))*d[i+3]/255;}
  assert.ok(ink>90,`uninked back row ${y}`);
  if(previous!==null)assert.ok(Math.abs(edge-previous)<=2,'detached spur or abrupt back contour');previous=edge;
 }
 console.log('PASS isolated rump has no old spur; rotated back has continuous fill and ink');
 // The exposed back must bow outward, not read as a diagonal chord joining
 // ribbon and pelvis. This point lies outside the old near-straight join.
 assert.ok(d[(126*256+175)*4+3]>200,'upper back needs a rounded shoulder, native(71.5,47)');
 const up=comp.render(0,0,{eyeX:0,eyeY:0,roll:Math.PI/9}).getContext('2d').getImageData(0,0,256,256).data;
 for(let y=156;y<166;y++)for(let x=183;x<190;x++)for(let c=0;c<4;c++){
  const i=(y*256+x)*4+c;assert.ok(Math.abs(up[i]-neutral[i])<=1,'raised ribbon pinches the rounded haunch into a point');
 }
 for(const [x,y] of [[20,64],[20,66],[22,67],[28,67]]){
  assert.ok(up[((y*2+32)*256+x*2+32)*4+3]>245,`upward gaze opens the left neck at ${x},${y}`);
 }
 console.log('PASS rounded back and filled left neck at upward extreme');
 // Exercise the actual lowered/swaying body field as well as standing.
 const atlas=make(4096,2560),ac=atlas.getContext('2d'),frames=[0,30,60,72,105,145,156];
 rig.body=(x,y,p)=>hunt.map(x,y,p.hunt);
 const loopRaster=t=>{const p=rig.pose();p.hunt=hunt.sample(t);p.bob=p.hunt.headBob;p.roll=p.hunt.headRoll;return renderer.render(p,{bodyOnly:true}).getContext('2d').getImageData(0,0,256,256).data;};
 const loopStart=loopRaster(1.25),loopEnd=loopRaster(1.65);
 for(let i=0;i<loopStart.length;i++)assert.ok(Math.abs(loopStart[i]-loopEnd[i])<=1,'settled loop endpoints differ in actual body raster');
 for(const frame of frames){const p=rig.pose();p.hunt=hunt.sample(frame/60);p.bob=p.hunt.headBob;p.roll=p.hunt.headRoll;ac.drawImage(renderer.render(p,{bodyOnly:true}),frame%16*256,Math.floor(frame/16)*256);}
 const moving=compose.create(atlas,head,eyes.create(head,make),make);
 for(const frame of frames)for(const roll of [.1,.2,Math.PI/9]){
  const pose=hunt.sample(frame/60),data=moving.render(frame,pose.amount,{eyeX:0,eyeY:0,roll}).getContext('2d').getImageData(0,0,256,256).data;
  for(const point of [[20,64],[20,66],[22,67],[28,67]]){
   const [x,y]=hunt.map(...point,pose).map(v=>Math.round((v+16)*2));
   assert.ok(data[(y*256+x)*4+3]>245,`neck opens while crouching: frame${frame} roll${roll} at${point}`);
  }
 }
 console.log('PASS left-neck coverage through standing, crouching, sway and recovery');
})().catch(e=>{console.error(e);process.exitCode=1});
