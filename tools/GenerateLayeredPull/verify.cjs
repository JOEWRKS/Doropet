const assert=require('node:assert/strict'),fs=require('fs'),path=require('path'),zlib=require('zlib'),crypto=require('crypto');
const data=require('./poses.json'),factory=require('./layered.js');
const decode=s=>new Uint8Array(Buffer.from(s,'base64'));
const model=factory(data,decode),samples=[];
for(let k=0;k<8;k++){
 assert.deepEqual(model.sample(k/7),model.frames[k],`Authored key ${k+1}`);
 for(const direction of [-1,1]){
  const p=Math.max(0,Math.min(1,k/7+direction*1e-8)),near=model.sample(p);
  let max=0;for(let i=0;i<near.length;i++)max=Math.max(max,Math.abs(near[i]-model.frames[k][i]));
  assert.ok(max<=1,`Key ${k+1} has a boundary flash: ${max}`);
 }
}
const head=require('./morph.js')({...data,frames:model.layers.map(l=>l.head),headOnly:true},x=>x);
const headReference=require('./head-reference.json');
for(let i=0;i<=112;i++){
 const pixels=model.sample(i/112);samples.push(Buffer.from(pixels));
 const headPixels=head.sample(i/112);
 assert.equal(crypto.createHash('sha256').update(headPixels).digest('hex'),headReference[i],`Isolated head changed at ${i+1}`);
 // Head's fully opaque foreground must remain unchanged by body changes.
 for(let j=0;j<headPixels.length;j+=4)if(headPixels[j+3]===255)
  assert.deepEqual(pixels.slice(j,j+4),headPixels.slice(j,j+4),`Body covers head at ${i+1}:${j}`);
 for(let j=0;j<pixels.length;j+=4)for(let c=0;c<3;c++)assert.ok(pixels[j+c]<=pixels[j+3]);
}
assert.equal(new Set(samples.map(p=>p.toString('base64'))).size,113);
for(let i=112;i>=0;i--)assert.deepEqual(Buffer.from(model.sample(i/112)),samples[i],'Reverse scrub must retrace identical poses');
for(const value of [NaN,Infinity,-Infinity])assert.throws(()=>model.sample(value));
// Real rear-paw interior markers follow the rear joint, not the torso mesh.
const fixture=structuredClone(data);
for(const [key,x,y] of [[2,48,82],[3,46,81]]){
 const pixels=Buffer.from(fixture.frames[key],'base64');
 for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++)pixels.set([0,255,0,255],((y+dy)*96+x+dx)*4);
 fixture.frames[key]=pixels.toString('base64');
}
const marked=factory(fixture,decode).sample(40/112);
let sum=0,sx=0,sy=0;
for(let y=0;y<96;y++)for(let x=0;x<96;x++){
 const i=(y*96+x)*4,w=Math.max(0,marked[i+1]-Math.max(marked[i],marked[i+2])-20);
 sum+=w;sx+=w*x;sy+=w*y;
}
assert.ok(sum>400,'Rear paw disappears instead of moving');
assert.ok(Math.abs(sx/sum-47)<1.5&&Math.abs(sy/sum-81.5)<1.5,`Rear path ${sx/sum},${sy/sum}`);
if(process.argv.includes('--bank')){
 const bank=zlib.gunzipSync(fs.readFileSync(path.join(__dirname,'../../src/Dororong.App/Assets/layered-pull.pbgra.gz')));
 assert.deepEqual(bank,Buffer.concat(samples),'Product bank is stale');
}
console.log('PASS: 113 unique reversible Pbgra frames; 8 exact keys; both-side key continuity; head/ribbon unchanged; rear articulation.');
