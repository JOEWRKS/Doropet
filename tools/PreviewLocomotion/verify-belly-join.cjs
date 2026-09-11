const assert=require('node:assert/strict');
require('./raster-harness.cjs').renderer().then(({rig,renderer})=>{
  const data=renderer.render(rig.pose({sit:1})).getContext('2d').getImageData(0,0,256,256).data;
  const alpha=(x,y)=>data[(Math.round((y+16)*2)*256+Math.round((x+16)*2))*4+3];
  // The old join pinned the belly to standing y77.7, creating a deep angular
  // notch beside the seated rear. The connection must fill that notch without
  // moving either the front paw's sole or the sleeping rump.
  // Foreleg floor alignment raises this bridge by about2–3px; these are the
  // corresponding hand-checked interiors in the aligned seated pose.
  for(const [x,y] of [[50,78],[51,78.5],[53,79.5]])assert.ok(alpha(x,y)>240,`angular belly notch remains at ${x},${y}`);
  assert.ok(alpha(51,85)<20,'belly bridge should not fill all the way to the paw floor');
  const oldRoot=((76+16)*2*256+(48+16)*2)*4;
  assert.ok(Math.min(...data.slice(oldRoot,oldRoot+3))>230&&data[oldRoot+3]>240,'old vertical root outline remains inside the belly');
  console.log('PASS: shallow belly connection replaces the deep standing-to-sitting notch');
}).catch(e=>{console.error(e);process.exitCode=1;});
