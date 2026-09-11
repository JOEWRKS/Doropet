// Detect stale JPEG export, accidental walking changes and a bright seated fringe.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),zlib=require('node:zlib');
const canvas=require(process.env.LOCOMOTION_CANVAS||path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));
(async()=>{
  const root=path.resolve(__dirname,'../..'),size=96*96*4;
  const bank=zlib.gunzipSync(fs.readFileSync(path.join(root,'src/Dororong.App/Assets/locomotion.pbgra.gz')));
  assert.equal(bank.length,20+642*size);
  const source=await canvas.loadImage(path.join(__dirname,'assets/seated-final-10-2.png'));
  const registered=canvas.createCanvas(96,96);registered.getContext('2d').drawImage(source,3,-12);
  const closed=await canvas.loadImage(path.join(root,'src/Dororong.App/Assets/dororong-closed-eyes.png'));
  for(const blink of [false,true]){
    // Expected endpoint comes from the supplied asset, not the morph renderer.
    const render=canvas.createCanvas(256,256),rc=render.getContext('2d');rc.drawImage(registered,32,32,192,192);
    if(blink){rc.save();rc.beginPath();rc.rect(72,114,58,42);rc.clip();rc.drawImage(closed,32,32,192,192);rc.restore();}
    const native=canvas.createCanvas(96,96),nc=native.getContext('2d');nc.drawImage(render,32,32,192,192,0,0,96,96);
    const rgba=nc.getImageData(0,0,96,96).data,expected=Buffer.alloc(size);
    for(let i=0;i<size;i+=4){const a=rgba[i+3];expected[i+3]=a;for(let c=0;c<3;c++)expected[i+c]=Math.round(rgba[i+2-c]*a/255);}
    const frame=blink?385:64,actual=bank.subarray(20+frame*size,20+(frame+1)*size);
    assert.ok(actual.equals(expected),`seated ${blink?'closed':'open'} endpoint does not match transparent PNG export`);
    const i=(24*96+35)*4;
    assert.ok(actual[i+3]<110&&actual[i]<35,'white JPG halo remains at upper hair fringe');
  }
  if(process.argv[2]){
    const before=zlib.gunzipSync(fs.readFileSync(process.argv[2]));assert.equal(before.length,bank.length);
    let unchanged=0;
    for(let frame=0;frame<642;frame++)if(frame%321===0||frame%321>=65){
      const o=20+frame*size;assert.ok(bank.subarray(o,o+size).equals(before.subarray(o,o+size)),`unrelated standing/walking frame ${frame} changed`);unchanged++;
    }
    console.log(`PASS: all ${unchanged} standing/walking frames byte-exact`);
    if(process.argv[3]){
      const sheet=canvas.createCanvas(768,768),sc=sheet.getContext('2d');
      for(const [col,data]of [before,bank].entries()){
        const native=canvas.createCanvas(96,96),ctx=native.getContext('2d'),im=ctx.createImageData(96,96),o=20+64*size;
        for(let i=0;i<size;i+=4){const a=data[o+i+3];im.data[i+3]=a;for(let c=0;c<3;c++)im.data[i+c]=a?Math.round(data[o+i+2-c]*255/a):0;}
        ctx.putImageData(im,0,0);
        for(const [row,bg]of ['black','white'].entries()){sc.fillStyle=bg;sc.fillRect(col*384,row*384,384,384);sc.imageSmoothingEnabled=false;sc.drawImage(native,col*384,row*384,384,384);}
      }
      fs.writeFileSync(process.argv[3],sheet.toBuffer('image/png'));
    }
  }
  console.log('PASS: both seated endpoints match authored PNG, head fringe alpha/color preserved');
})().catch(error=>{console.error(error);process.exitCode=1;});
