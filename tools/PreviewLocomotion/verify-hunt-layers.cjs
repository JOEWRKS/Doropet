const assert=require('node:assert/strict');
const {create}=require('./four-leg-harness.cjs');
(async()=>{const {renderer,rig}=await create();const body=renderer.render(rig.pose(),{bodyOnly:true}).getContext('2d').getImageData(0,0,256,256).data;
let head=0,lower=0;
for(let y=78;y<122;y++)for(let x=72;x<148;x++)head+=body[(y*256+x)*4+3];
for(let y=180;y<204;y++)for(let x=114;x<174;x++)lower+=body[(y*256+x)*4+3];
assert.equal(head,0,'body-only layer must not contain the old head underneath the moving head');
assert.ok(lower>30000,'body-only extraction must preserve the actual forebody');
console.log('PASS body layer excludes face and preserves forebody');})();
