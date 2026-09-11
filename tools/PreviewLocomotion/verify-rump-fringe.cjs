const assert=require('node:assert/strict');
require('./raster-harness.cjs').renderer(null,{authored:true}).then(({renderer})=>{
  const d=renderer.original.getContext('2d').getImageData(0,0,96,96).data;
  // These disconnected semi-transparent source flecks sit beyond the rump,
  // not on its antialiased boundary. They reappear at the standing handoff.
  for(const [x,y] of [[74,70],[73,71],[74,71]])assert.ok(d[(y*96+x)*4+3]<=2,`visible detached rump fringe at ${x},${y}: ${d[(y*96+x)*4+3]}`);
  assert.ok(d[(70*96+70)*4+3]>200,'connected rump boundary was erased');
  console.log('PASS: detached rump fringe is absent, connected boundary retained');
}).catch(e=>{console.error(e);process.exitCode=1;});
