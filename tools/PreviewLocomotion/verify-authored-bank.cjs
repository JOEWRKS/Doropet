const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
require('./raster-harness.cjs').renderer(null,{authored:true}).then(async({rig,renderer,canvas})=>{
  const out=process.argv[2]?path.resolve(process.argv[2]):path.resolve(__dirname,'../../artifacts/repro/locomotion/authored-10-2');
  const manifest=JSON.parse(fs.readFileSync(path.join(out,'animation.json'),'utf8'));
  assert.equal(manifest.sit.length,65);assert.deepEqual(manifest.stand,manifest.sit.toReversed());
  const c=canvas.createCanvas(256,256),ctx=c.getContext('2d');
  const bank=await canvas.loadImage(path.join(__dirname,'assets/seated-final-10-2-bank.png'));
  assert.equal(bank.width,2048);assert.equal(bank.height,2304);
  let previous,worst=0,where=0;
  for(let n=0;n<65;n++){
    ctx.clearRect(0,0,256,256);ctx.drawImage(await canvas.loadImage(path.join(out,manifest.sit[n])),0,0);
    const d=ctx.getImageData(0,0,256,256).data;
    ctx.clearRect(0,0,256,256);ctx.drawImage(bank,(n%8)*256,Math.floor(n/8)*256,256,256,0,0,256,256);
    assert.deepEqual(ctx.getImageData(0,0,256,256).data,d,`sprite bank frame ${n} differs from exported PNG`);
    if(n===0||n===64)assert.deepEqual(renderer.render(rig.pose({sit:n/64})).getContext('2d').getImageData(0,0,256,256).data,d,`exported endpoint ${n} changed`);
    if(previous){let delta=0;for(let i=0;i<d.length;i+=4){for(let k=0;k<3;k++)delta+=Math.abs(d[i+k]*d[i+3]-previous[i+k]*previous[i+3])/255;delta+=Math.abs(d[i+3]-previous[i+3]);}
      delta/=d.length;if(delta>worst){worst=delta;where=n;}
      assert.ok(delta<1,`export frame ${n} jumps: mean premultiplied step ${delta}`);
    }
    previous=d;
  }
  console.log(`PASS:65 exported/banked frames match, exact endpoints, reverse order, maximum frame step ${worst.toFixed(3)}/255 at ${where}`);
}).catch(e=>{console.error(e);process.exitCode=1;});
