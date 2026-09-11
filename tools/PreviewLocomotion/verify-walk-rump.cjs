// A seam donor must not create a new exterior silhouette above the rump.
const assert=require('node:assert/strict');
require('./raster-harness.cjs').renderer().then(({renderer})=>{
  const source=renderer.original.getContext('2d').getImageData(0,0,96,96).data;
  const body=renderer.parts[3];
  for(const [x,y] of [[71,47],[72,47],[72,48],[73,48]]){
    const i=(y*96+x)*4;
    assert.equal(source[i+3],0,'fixture point must remain outside authored art');
    assert.equal(body[i+3],0,`body donor creates exterior rump spur at ${x},${y}`);
  }
  assert.equal(body[(47*96+70)*4+3],0,'partially transparent ribbon edge must not receive an opaque body donor');
  assert.ok(body[(48*96+70)*4+3]>=250,'solid ribbon/body attachment donor was removed');
  for(let y=38;y<50;y++)for(let x=69;x<80;x++){
    const i=(y*96+x)*4;
    assert.ok(body[i+3]<=source[i+3],`exterior donor coverage exceeds authored coverage at ${x},${y}`);
    for(let c=0;c<3;c++)assert.ok(body[i+c]<=body[i+3]+.001,'donor is not premultiplied');
  }
  console.log('PASS: upper-rump donors remain under solid foreground; no exterior spur and solid attachment retained');
}).catch(e=>{console.error(e);process.exitCode=1;});
