// Regression: walking must not add a heavy second outline to the approved art.
// Uses actual native96 pixels, matching export-product's crop/downsample path.
const assert=require('node:assert/strict');
require('./raster-harness.cjs').renderer().then(({rig,renderer,canvas})=>{
  const native=canvas.createCanvas(96,96),ctx=native.getContext('2d');
  function lowerBodyInk(p){
    ctx.clearRect(0,0,96,96);ctx.drawImage(renderer.render(p),32,32,192,192,0,0,96,96);
    const d=ctx.getImageData(0,0,96,96).data;let ink=0;
    for(let y=70;y<96;y++)for(let x=0;x<96;x++){
      const i=(y*96+x)*4;ink+=(255-d[i])*d[i+3]/255;
    }
    return ink;
  }
  const restingInk=lowerBodyInk(rig.pose({}));let min=Infinity,max=0;
  for(let phase=0;phase<32;phase++){
    const ratio=lowerBodyInk(rig.pose({walk:1,distance:phase*10/32}))/restingInk;
    min=Math.min(min,ratio);max=Math.max(max,ratio);
    // A small shape-dependent tolerance permits the authored texture to warp.
    // The bug adds23–28% extra ink compared with the same warped source.
    assert.ok(ratio<=1.10,`walking lower-body outline is heavier than authored standing: phase ${phase}, ${(ratio*100).toFixed(2)}% of rest`);
    assert.ok(ratio>=.85,`walking lower-body ink disappeared: phase ${phase}, ${(ratio*100).toFixed(2)}% of rest`);
  }
  // The adaptive repair must remain continuous through stance/swing handoffs
  // and the phase wrap, not only at the32 stored phase samples.
  function read(p){
    ctx.clearRect(0,0,96,96);ctx.drawImage(renderer.render(p),32,32,192,192,0,0,96,96);
    const data=ctx.getImageData(0,0,96,96).data;
    // Compare the product's premultiplied channels: unpremultiplied RGB at
    // near-transparent fringe pixels can vary without visible color change.
    for(let i=0;i<data.length;i+=4)for(let c=0;c<3;c++)data[i+c]=Math.round(data[i+c]*data[i+3]/255);
    return data;
  }
  for(const walk of [.125,.5,1])for(const distance of [0,1,5,6,10]){
    const a=read(rig.pose({walk,distance:distance-1e-7})),b=read(rig.pose({walk,distance:distance+1e-7}));let difference=0,largest=0;
    for(let i=0;i<a.length;i++){const d=Math.abs(a[i]-b[i]);difference+=d;largest=Math.max(largest,d);}
    assert.ok(largest<=1&&difference/a.length<.001,`native walking ink jumps at amplitude ${walk}, distance ${distance}: max ${largest}, mean ${difference/a.length}`);
  }
  console.log(`PASS: all32 native walking phases keep lower-body ink ${(min*100).toFixed(2)}–${(max*100).toFixed(2)}% of authored standing`);
  console.log('PASS: native ink remains continuous at15 stance/swing and cycle-wrap boundaries');
}).catch(e=>{console.error(e);process.exitCode=1;});
