const assert=require('node:assert/strict'),fs=require('node:fs'),zlib=require('node:zlib'),path=require('node:path');
const before=zlib.gunzipSync(fs.readFileSync(process.argv[2]));
const after=zlib.gunzipSync(fs.readFileSync(path.join(__dirname,'../../src/Dororong.App/Assets/locomotion.pbgra.gz')));
assert.equal(after.length,before.length);assert.deepEqual(after.subarray(0,20),before.subarray(0,20));
const size=96*96*4;let changed=0;
for(let frame=0;frame<642;frame++){
  const a=before.subarray(20+frame*size,20+(frame+1)*size),b=after.subarray(20+frame*size,20+(frame+1)*size);
  if(a.equals(b))continue;
  assert.equal(frame,321,`unrelated motion frame changed: ${frame}`);changed++;
}
assert.equal(changed,1);
const open=after.subarray(20,20+size),closed=after.subarray(20+321*size,20+322*size);
let eyes=0;
for(let y=0;y<96;y++)for(let x=0;x<96;x++){
  const i=(y*96+x)*4;if(open.subarray(i,i+4).equals(closed.subarray(i,i+4)))continue;
  assert.ok(x>=14&&x<50&&y>=40&&y<64,`closed standing differs outside eyes at ${x},${y}`);eyes++;
}
assert.ok(eyes>100);
console.log(`PASS: only closed-standing frame321 changed; 641other frames exact; ${eyes} eye texels differ from open standing`);
