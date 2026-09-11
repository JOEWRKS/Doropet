// Source-coordinate inspection only; does not alter source artwork.
const {create}=require('./four-leg-harness.cjs'),fs=require('node:fs'),path=require('node:path');
(async()=>{const {canvas,renderer}=await create();const c=canvas.createCanvas(768,768),ctx=c.getContext('2d');
ctx.fillStyle='#ddd';ctx.fillRect(0,0,768,768);ctx.imageSmoothingEnabled=false;ctx.drawImage(renderer.original,0,0,768,768);
ctx.strokeStyle='#ff440066';ctx.fillStyle='#a02000';ctx.font='10px monospace';
for(let x=15;x<=47;x++){ctx.beginPath();ctx.moveTo(x*8,42*8);ctx.lineTo(x*8,64*8);ctx.stroke();if(x%2===0)ctx.fillText(x,x*8,42*8-4)}
for(let y=42;y<=64;y++){ctx.beginPath();ctx.moveTo(15*8,y*8);ctx.lineTo(47*8,y*8);ctx.stroke();ctx.fillText(y,47*8+4,y*8)}
const out=path.resolve(__dirname,'../../artifacts/repro/hunt-gaze-20260910');fs.mkdirSync(out,{recursive:true});fs.writeFileSync(path.join(out,'source-grid.png'),c.toBuffer('image/png'));
})();
