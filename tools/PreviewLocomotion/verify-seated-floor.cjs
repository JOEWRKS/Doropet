const assert=require('node:assert/strict');
require('./raster-harness.cjs').renderer().then(({rig,renderer})=>{
  const data=renderer.render(rig.pose({sit:1})).getContext('2d').getImageData(0,0,256,256).data;
  const soles=[[17,29],[33,48],[59,76]].map(([l,r])=>{
    let bottom=0;for(let y=150;y<220;y++)for(let x=(l+16)*2;x<(r+16)*2;x++)if(data[(y*256+x)*4+3]>=128)bottom=Math.max(bottom,(y+.5)/2-16);return bottom;
  });
  assert.ok(Math.max(...soles)-Math.min(...soles)<=.5,`seated paws have different floor heights: ${soles}`);
  assert.ok(soles.every(y=>Math.abs(y-83.75)<=.5),'seated paws missed the shared local floor');
  let upperInk=0,foldedInk=0;
  for(let y=75;y<=82;y+=.5)for(let x=60;x<=64;x+=.5){
    const i=(Math.round((y+16)*2)*256+Math.round((x+16)*2))*4;
    if(data[i+3]>240&&Math.min(...data.slice(i,i+3))<160){if(y<77.5)upperInk++;else foldedInk++;}
  }
  assert.equal(upperInk,0,'hindleg crease remains tall instead of folding down');
  assert.ok(foldedInk>0,'folded hindleg crease disappeared');
  console.log(`PASS: front/front/rear soles share a horizontal floor: ${soles}`);
}).catch(e=>{console.error(e);process.exitCode=1;});
