const assert=require('node:assert/strict'),path=require('node:path');
require('./raster-harness.cjs').renderer().then(async({rig,renderer,canvas})=>{
  const source=canvas.createCanvas(96,96),ctx=source.getContext('2d');ctx.drawImage(await canvas.loadImage(path.resolve(__dirname,'../../src/Dororong.App/Assets/dororong-sleep.png')),0,0);
  const sleep=ctx.getImageData(0,0,96,96).data;
  const frame=renderer.render(rig.pose({sit:1})).getContext('2d').getImageData(0,0,256,256).data;
  // Preserve the source sleeping rump shape (translated down3px to front-foot
  // ground level), not an invented haunch. Permit subpixel stroke sampling.
  let misses=0,total=0;
  for(let y=59;y<=83;y++)for(let x=61;x<=85;x++){
    const src=sleep[(y*96+x)*4+3]>=128;
    const got=frame[(((y+3+16)*2)*256+(x+16)*2)*4+3]>=128;
    if(src!==got)misses++;total++;
  }
  assert.ok(misses/total<.04,`seated rear does not follow sleeping source: ${(misses/total*100).toFixed(1)}% silhouette mismatch`);
  // Authored diagonal folded-knee line must remain, not a new tiny curly paw.
  let dark=0;
  for(let y=75;y<=80;y++)for(let x=60;x<=63;x++){
    const i=(((y+3+16)*2)*256+(x+16)*2)*4;
    if(frame[i+3]>200&&Math.min(...frame.slice(i,i+3))<150)dark++;
  }
  assert.ok(dark>=5,'sleeping hindleg crease was lost');
  console.log('PASS: seated silhouette follows sleeping source and preserves its folded-knee line');
}).catch(e=>{console.error(e);process.exitCode=1;});
