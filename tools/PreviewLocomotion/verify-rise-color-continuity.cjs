const assert=require('node:assert/strict');
require('./raster-harness.cjs').renderer(null,{authored:true}).then(({rig,renderer})=>{
  // Crossing a texel-cell threshold must not suddenly switch contour ink on.
  const t=.7097415383905172,i=(181*256+133)*4;
  const before=Array.from(renderer.render(rig.pose({sit:t-1e-6})).getContext('2d').getImageData(0,0,256,256).data.slice(i,i+4));
  const after=Array.from(renderer.render(rig.pose({sit:t+1e-6})).getContext('2d').getImageData(0,0,256,256).data.slice(i,i+4));
  assert.ok(Math.max(...before.map((v,k)=>Math.abs(v-after[k])))<=2,`interior ink jumps: ${before} -> ${after}`);
  console.log('PASS: mapped interior ink is continuous across texel-cell boundaries');
}).catch(e=>{console.error(e);process.exitCode=1;});
