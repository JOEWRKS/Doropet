const assert=require('node:assert/strict'),fs=require('node:fs'),zlib=require('node:zlib'),path=require('node:path');
const before=zlib.gunzipSync(fs.readFileSync(process.argv[2]));
const after=zlib.gunzipSync(fs.readFileSync(path.join(__dirname,'../../src/Dororong.App/Assets/locomotion.pbgra.gz')));
assert.equal(after.length,before.length);assert.deepEqual(after.subarray(0,20),before.subarray(0,20));
const size=96*96*4;let changed=0,frames=0,sits=0,bounds=[96,96,0,0];
for(let frame=0;frame<642;frame++){
 const offset=20+frame*size;
 if(frame%321<=64){assert.deepEqual(after.subarray(offset,offset+size),before.subarray(offset,offset+size),`sit frame ${frame} changed`);sits++;continue;}
 let n=0;
 for(let y=0;y<96;y++)for(let x=0;x<96;x++){
  const i=offset+(y*96+x)*4;
  if(after.subarray(i,i+4).equals(before.subarray(i,i+4)))continue;
  assert.ok(x>=68&&x<=75&&y>=44&&y<=51,`unrelated head/body texel changed: frame${frame},${x},${y}`);
  assert.ok(after[i+3]<=before[i+3],`new exterior coverage: frame${frame},${x},${y}`);
  bounds=[Math.min(bounds[0],x),Math.min(bounds[1],y),Math.max(bounds[2],x),Math.max(bounds[3],y)];n++;
 }
 if(n)frames++;changed+=n;
 // A solid interior across the ribbon/rump join remains opaque in both eyes.
 for(const [x,y] of [[68,53],[68,54],[67,55]])assert.ok(after[offset+(y*96+x)*4+3]>=240,`join opened: frame${frame},${x},${y}`);
}
assert.equal(frames,512,'each walking phase should lose the stray donor');
console.log(`PASS: ${sits} sit frames exact; ${frames} walking frames differ only at upper-rump ${bounds}; ${changed} changed pixels; no new coverage; solid joins retained`);
