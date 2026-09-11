// Release gate: shipped bytes must match the user-approved preview and keep
// all previous non-walking frames. Golden files are frozen, not regenerated.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),zlib=require('node:zlib'),crypto=require('node:crypto');
const {create}=require('./four-leg-harness.cjs');
const size=96*96*4,fixtures=path.join(__dirname,'fixtures/four-leg-baseline');
const frame=(bank,n)=>bank.subarray(20+n*size,20+(n+1)*size);
function pbgra(rgba){
  const bytes=Buffer.alloc(size);
  for(let i=0;i<size;i+=4){const a=rgba[i+3];bytes[i+3]=a;for(let k=0;k<3;k++)bytes[i+k]=Math.round(rgba[i+2-k]*a/255);}
  return bytes;
}
(async()=>{
  const file=process.argv[2]||path.resolve(__dirname,'../../src/Dororong.App/Assets/locomotion.pbgra.gz');
  const bank=zlib.gunzipSync(fs.readFileSync(file));
  assert.equal(bank.toString('ascii',0,4),'LOCO');
  assert.deepEqual([4,8,12,16].map(i=>bank.readUInt32LE(i)),[1,96,96,642]);
  assert.equal(bank.length,20+642*size);
  const baselineFile=fs.readFileSync(path.join(fixtures,'before-locomotion.pbgra.gz'));
  assert.equal(crypto.createHash('sha256').update(baselineFile).digest('hex'),'e1d7249c2427c0b021175c9b83b99d01f858ccce7383bbd86feff239321db394','original product fixture replaced');
  const baseline=zlib.gunzipSync(baselineFile);
  for(const eye of [0,321])for(let n=0;n<=64;n++)assert.ok(frame(bank,eye+n).equals(frame(baseline,eye+n)),`standing/seated frame ${eye+n} changed`);
  const {canvas,native,rig}=await create(),atlasFile=fs.readFileSync(path.join(fixtures,'approved-walk-atlas.png'));
  assert.equal(crypto.createHash('sha256').update(atlasFile).digest('hex'),'0db7e1068bc56c4ee8e98e947a371a45c7d4a2b120af36db3eb06c956c26dc57','approved preview fixture replaced');
  const atlas=await canvas.loadImage(atlasFile),surface=canvas.createCanvas(96,96),ctx=surface.getContext('2d');
  for(let phase=0;phase<32;phase++){
    ctx.clearRect(0,0,96,96);ctx.drawImage(atlas,(phase%8)*256+32,Math.floor(phase/8)*256+32,192,192,0,0,96,96);
    const expected=pbgra(ctx.getImageData(0,0,96,96).data);
    assert.ok(frame(bank,65+7*32+phase).equals(expected),`product walking phase ${phase} does not match approved four-leg preview`);
  }
  for(const blink of [false,true])for(let amplitude=1;amplitude<=8;amplitude++)for(let phase=0;phase<32;phase++){
    const expected=pbgra(native({...rig.pose({walk:amplitude/8,distance:phase*10/32}),blink}).data);
    const index=(blink?321:0)+65+(amplitude-1)*32+phase;
    assert.ok(frame(bank,index).equals(expected),`product amplitude/eye frame ${index} is stale`);
  }
  console.log('PASS: 130 non-walking frames unchanged; 32 approved preview phases exact; all 512 amplitude/eye walking frames match renderer');
})().catch(error=>{console.error(error.message);process.exitCode=1});
