const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {create}=require('./four-leg-harness.cjs'),hunt=require('./hunt-motion.js'),eyes=require('./gaze-eyes.js'),compose=require('./gaze-compose.js');
(async()=>{
 const {canvas}=await create(),make=(w,h)=>canvas.createCanvas(w,h),load=f=>canvas.loadImage(path.resolve(__dirname,'../../artifacts/repro/hunt-preview-20260910',f));
 const head=await load('head.png'),body=await load('body-atlas.png'),c=compose.create(body,head,eyes.create(head,make),make);
 const states=[{eyeX:0,eyeY:0,headX:0,headY:0,roll:0},{eyeX:-.85,eyeY:-.65,headX:0,headY:0,roll:-Math.PI/9},{eyeX:.85,eyeY:.65,headX:0,headY:0,roll:Math.PI/9},{eyeX:.85,eyeY:-.65,headX:0,headY:0,roll:0}];
 const sheet=make(1024,1024),ctx=sheet.getContext('2d');let overlappingChannels=0;
 for(let row=0;row<4;row++){
  const frame=row%2?60:156,amount=hunt.sample(frame/60).amount;
  const neutral=c.render(frame,amount,states[0]).getContext('2d').getImageData(0,0,256,256).data;
  for(let col=0;col<4;col++){
   const surface=c.render(frame,amount,states[col]),data=surface.getContext('2d').getImageData(0,0,256,256).data;
   // Actual composite, not the transform helper: gaze cannot move the paws.
   for(let y=191;y<224;y++)for(let x=32;x<220;x++){
    const offset=(y*256+x)*4;
    // Test actual body coverage, not empty space in the same rectangle where
    // a rotating hair tip can legitimately appear beside the paw.
    if(neutral[offset+3]===0)continue;
    for(let ch=0;ch<4;ch++)if(data[offset+ch]!==neutral[offset+ch]){
     overlappingChannels++;
     if(!process.argv.includes('--study-overlap'))assert.equal(data[offset+ch],neutral[offset+ch],`gaze alters visible foot: row${row} col${col} x${x} y${y} ch${ch}`);
    }
   }
   ctx.fillStyle=row<2?'#fcfaf8':'#101014';ctx.fillRect(col*256,row*256,256,256);ctx.drawImage(surface,col*256,row*256);
   ctx.fillStyle=row<2?'#333':'#eee';ctx.font='14px sans-serif';ctx.fillText(['NEUTRAL','UP LEFT','DOWN RIGHT','EYES ONLY'][col],col*256+12,row*256+24);
  }
 }
 const out=path.resolve(__dirname,'../../artifacts/repro/hunt-gaze-20260910');fs.mkdirSync(out,{recursive:true});fs.writeFileSync(path.join(out,'gaze-contact-sheet.png'),sheet.toBuffer('image/png'));
 console.log(overlappingChannels?'STUDY ONLY: rotated head overlaps lower-body region; changed channels='+overlappingChannels:'PASS composed gaze: all lower body pixels fixed in standing/crouched extremes');
})();
