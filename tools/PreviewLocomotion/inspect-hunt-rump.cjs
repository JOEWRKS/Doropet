const fs=require('node:fs'),path=require('node:path');
const {create}=require('./four-leg-harness.cjs');
(async()=>{
 const {renderer,rig,canvas}=await create(),sheet=canvas.createCanvas(1152,576),ctx=sheet.getContext('2d');
 const head=canvas.createCanvas(96,96),hc=head.getContext('2d'),hd=hc.createImageData(96,96),p=renderer.parts[4];
 for(let i=0;i<p.length;i+=4){hd.data[i+3]=p[i+3];for(let c=0;c<3;c++)hd.data[i+c]=p[i+3]?p[i+c]*255/p[i+3]:0}hc.putImageData(hd,0,0);
 const body=renderer.render(rig.pose(),{bodyOnly:true});
 ctx.fillStyle='#bfbfbf';ctx.fillRect(0,0,1152,576);ctx.imageSmoothingEnabled=false;
 ctx.drawImage(renderer.original,0,0,384,384);ctx.drawImage(head,384,0,384,384);ctx.drawImage(body,32,32,192,192,768,0,384,384);
 ctx.fillStyle='#222';ctx.font='15px monospace';ctx.fillText('Original / head ownership / body-only',12,410);
 ctx.drawImage(renderer.original,64,40,16,20,0,416,128,160);ctx.drawImage(head,64,40,16,20,384,416,128,160);ctx.drawImage(body,160,112,32,40,768,416,128,160);
 const out=path.resolve(__dirname,'../../artifacts/repro/hunt-gaze-20260910/rump-layers.png');fs.writeFileSync(out,sheet.toBuffer('image/png'));console.log(out);
})();
