const fs=require('node:fs'),path=require('node:path');
require('./raster-harness.cjs').renderer(null,{authored:true}).then(({rig,renderer,canvas})=>{
  const out=process.argv[2]?path.resolve(process.argv[2]):path.resolve(__dirname,'../../artifacts/repro/locomotion/authored-10-2');fs.mkdirSync(out,{recursive:true});
  const atlas=canvas.createCanvas(768,768),ctx=atlas.getContext('2d');ctx.fillStyle='#19171b';ctx.fillRect(0,0,768,768);
  const bank=canvas.createCanvas(2048,2304),bctx=bank.getContext('2d');
  const frames=[];
  for(let i=0;i<=64;i++){
    const name=`sit-${String(i).padStart(3,'0')}.png`,frame=renderer.render(rig.pose({sit:i/64}));
    fs.writeFileSync(path.join(out,name),frame.toBuffer('image/png'));frames.push(name);
    const snapshot=frame.getContext('2d').getImageData(0,0,256,256);
    bctx.putImageData(snapshot,(i%8)*256,Math.floor(i/8)*256);
    if(i%8===0){const n=i/8;ctx.putImageData(snapshot,(n%3)*256,Math.floor(n/3)*256);ctx.fillStyle='white';ctx.fillText(`${i}/64`,(n%3)*256+12,Math.floor(n/3)*256+20);}
  }
  fs.writeFileSync(path.join(out,'atlas.png'),atlas.toBuffer('image/png'));
  fs.writeFileSync(path.join(__dirname,'assets/seated-final-10-2-bank.png'),bank.toBuffer('image/png'));
  fs.writeFileSync(path.join(out,'animation.json'),JSON.stringify({source:'seated-final-10-2.png',registration:{x:3,y:-12},canvas:[256,256],durationMs:650,sampling:'65 equally spaced pose amounts; apply smoothstep timing at playback',sit:frames,stand:frames.toReversed()},null,2));
  console.log(`Exported65 reversible pose frames to ${out}`);
}).catch(e=>{console.error(e);process.exitCode=1;});
