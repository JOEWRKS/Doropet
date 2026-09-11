const assert=require('node:assert/strict');
const {create}=require('./four-leg-harness.cjs'),hunt=require('./hunt-motion.js');
const compose=require('./gaze-compose.js'),eyes=require('./gaze-eyes.js');
(async()=>{
 const {renderer,rig,canvas}=await create(),make=(w,h)=>canvas.createCanvas(w,h);
 const atlas=make(4096,2560),ac=atlas.getContext('2d');
 rig.body=(x,y,p)=>hunt.map(x,y,p.hunt);
 for(const frame of [0,60,75,99,156]){const p=rig.pose();p.hunt=hunt.sample(frame/60);ac.drawImage(renderer.render(p,{bodyOnly:true}),frame%16*256,Math.floor(frame/16)*256);}
 const head=make(96,96),hc=head.getContext('2d'),hd=hc.createImageData(96,96),part=renderer.parts[4];
 for(let i=0;i<part.length;i+=4){hd.data[i+3]=part[i+3];for(let c=0;c<3;c++)hd.data[i+c]=part[i+3]?part[i+c]*255/part[i+3]:0;}hc.putImageData(hd,0,0);
 const comp=compose.create(atlas,head,eyes.create(head,make),make),gaze={eyeX:0,eyeY:0,roll:0};
 const render=f=>comp.render(f,hunt.sample(f/60).amount,gaze).getContext('2d').getImageData(0,0,256,256).data;
 const data=render(60),alpha=(x,y)=>data[((y*2+32)*256+x*2+32)*4+3];
 // Actual visible contour, not body alpha alone: underlays and the head
 // can extend a white fringe past an otherwise intact hidden vector stroke.
 for(const x of [22,24,26,44,46,48]){
  const col=x*2+32;let found=false;for(let row=181;row<199;row++){
   if(data[(row*256+col)*4+3]>=128&&data[((row+1)*256+col)*4+3]<128){
    let peak=0;for(let y=row-2;y<=row+1;y++){const i=(y*256+col)*4;peak=Math.max(peak,(255-data[i+1])/203*data[i+3]/255);}
    assert.ok(peak>.4,`visible outline fades at ${x},${row/2-16}: ${peak}`);found=true;
   }
  }assert.ok(found,'expected visible forebody edge');
 }
 // The former small foot ended at x16, leaving its medial wrist empty.
 // A broad diagonal foreleg must carry solid body fill through this gap.
 for(const [x,y] of [[18,79],[19,78],[20,77],[21,76]])assert.ok(alpha(x,y)>230,`far wrist pinches away at ${x},${y}`);
 // Both toes remain separate below the belly; a white bridge is not a fix.
 assert.ok(alpha(22,82)<20,'forefeet merged below the belly');
 for(const [x,y] of [[47,76],[48,76],[49,76]])assert.ok(alpha(x,y)>245,'forebody replacement leaves a translucent torso seam');
 // Inspect the real body composite without the occluding hair layer, which
 // can otherwise hide a double-alpha feathering defect at the attachment.
 const empty=make(384,384),bodyOnly=compose.create(atlas,head,{paint:()=>empty},make);
 const isolated=bodyOnly.render(60,1,gaze).getContext('2d').getImageData(0,0,256,256).data;
 assert.ok(isolated[((75*2+32)*256+48*2+32)*4+3]>245,'feather must retain opaque body coverage beneath the hair');
 assert.deepEqual(render(0),render(156),'standing return changes original art');
 assert.deepEqual(render(75),render(99),'new curves jump across the held loop');
 console.log('PASS composed broad foreleg connection, separate toes, exact rest and held-loop endpoints');
})().catch(e=>{console.error(e);process.exitCode=1});
