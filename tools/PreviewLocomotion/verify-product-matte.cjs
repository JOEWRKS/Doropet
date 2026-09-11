const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),zlib=require('node:zlib'),crypto=require('node:crypto');
const rig=require('./rig.js');
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const current=!process.argv[2]||process.argv[2]==='-';
const file=current?path.resolve(__dirname,'../../src/Dororong.App/Assets/locomotion.pbgra.gz'):process.argv[2];
const raw=zlib.gunzipSync(fs.readFileSync(file)),frameSize=96*96*4;
assert.equal(raw.toString('ascii',0,4),'LOCO');
assert.deepEqual([4,8,12,16].map(i=>raw.readUInt32LE(i)),[1,96,96,642]);
assert.equal(raw.length,20+642*frameSize);
const edge=20+64*frameSize+(85*96+67)*4;
// The old exported edge had Pbgra [149,149,149,195]: opaque white-matte
// contamination survived the 2x bank -> native96 downsample.
assert.ok(raw[edge+3]<130,`native seated outside edge retained excessive coverage: ${Array.from(raw.subarray(edge,edge+4))}`);
assert.ok(raw[edge]<90,'native seated outer edge remains bright on black');
for(let i=20;i<raw.length;i+=4)for(let c=0;c<3;c++)assert.ok(raw[i+c]<=raw[i+3],'invalid premultiplied channel');
if(current){
  const manifest=JSON.parse(fs.readFileSync(path.join(__dirname,'product-manifest.json'),'utf8'));
  assert.equal(sha(raw),manifest.rawSha256);
  for(const frame of manifest.frames)assert.equal(sha(raw.subarray(20+frame.index*frameSize,20+(frame.index+1)*frameSize)),frame.sha256);
  for(const [source,hash] of Object.entries(manifest.sources))assert.equal(sha(fs.readFileSync(path.join(__dirname,source))),hash,`stale source manifest: ${source}`);
}
if(process.argv[3]){
  const before=zlib.gunzipSync(fs.readFileSync(process.argv[3]));assert.equal(raw.length,before.length);
  let protectedPixels=0;
  for(let frame=0;frame<642;frame++){
    const offset=20+frame*frameSize,isSit=frame%321<=64;
    if(!isSit||frame%321===0){assert.deepEqual(raw.subarray(offset,offset+frameSize),before.subarray(offset,offset+frameSize),`standing/walking frame ${frame} changed`);continue;}
    for(let y=0;y<96;y++)for(let x=0;x<96;x++)if(rig.headDistance(x+.5,y+.5)===0){
      const i=offset+(y*96+x)*4;assert.deepEqual(raw.subarray(i,i+4),before.subarray(i,i+4),`protected head/ribbon changed in native frame ${frame} at ${x},${y}`);protectedPixels++;
    }
  }
  console.log(`PASS: standing + all512 walking frames exact; ${protectedPixels} protected head/ribbon pixels exact across128 seated frames`);
  if(process.argv[4]){
    const canvas=require(process.env.LOCOMOTION_CANVAS||path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));
    const out=path.resolve(process.argv[4]);fs.mkdirSync(out,{recursive:true});
    const sheet=canvas.createCanvas(768,768),sc=sheet.getContext('2d');
    for(const [col,bank] of [before,raw].entries()){
      const native=canvas.createCanvas(96,96),ctx=native.getContext('2d'),image=ctx.createImageData(96,96),offset=20+64*frameSize;
      for(let i=0;i<frameSize;i+=4){const a=bank[offset+i+3];image.data[i+3]=a;for(let c=0;c<3;c++)image.data[i+c]=a?Math.round(bank[offset+i+2-c]*255/a):0;}
      ctx.putImageData(image,0,0);fs.writeFileSync(path.join(out,`seated-native-${col?'after':'before'}.png`),native.toBuffer('image/png'));
      for(const [row,bg] of ['#000000','#ffffff'].entries()){
        sc.fillStyle=bg;sc.fillRect(col*384,row*384,384,384);sc.imageSmoothingEnabled=false;sc.drawImage(native,col*384,row*384,384,384);
      }
    }
    fs.writeFileSync(path.join(out,'seated-native-before-after-black-white-4x.png'),sheet.toBuffer('image/png'));
  }
}
console.log(`PASS: native seated edge Pbgra ${Array.from(raw.subarray(edge,edge+4))}; valid642-frame bank`);
