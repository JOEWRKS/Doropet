const fs=require('node:fs'),path=require('node:path');
const {current}=require('./verify-seated-contour-proof.cjs');
const canvas=require(process.env.LOCOMOTION_CANVAS||path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));
(async()=>{
 const before=current(),{image:after,width,ink}=await require('./seated-contour-proof.cjs').createCandidate(before,canvas);
 const out=path.resolve(__dirname,'../../artifacts/repro/seated-contour-proof-20260910');fs.mkdirSync(out,{recursive:true});
 for(const [name,image]of [['before',before],['candidate',after]])fs.writeFileSync(path.join(out,name+'.png'),image.toBuffer('image/png'));
 const sheet=canvas.createCanvas(768,820),ctx=sheet.getContext('2d');
 for(const [row,bg]of ['#101010','#f9f8f6'].entries())for(const [col,im]of [before,after].entries()){
  const x=col*384,y=row*410;ctx.fillStyle=bg;ctx.fillRect(x,y,384,410);ctx.imageSmoothingEnabled=false;ctx.drawImage(im,x,y+24,384,384);
  ctx.fillStyle=row?'#222':'#ddd';ctx.font='16px sans-serif';ctx.fillText(col?'BODY CONTOUR CANDIDATE':'CURRENT PRODUCT',x+16,y+22);
 }
 fs.writeFileSync(path.join(out,'comparison.png'),sheet.toBuffer('image/png'));
 const compact=canvas.createCanvas(640,470),cc=compact.getContext('2d');
 cc.fillStyle='#101010';cc.fillRect(0,0,640,310);cc.fillStyle='#f9f8f6';cc.fillRect(0,310,640,160);
 for(const [col,im]of [before,after].entries()){
  const x=col*320;cc.fillStyle='#ddd';cc.font='15px sans-serif';cc.fillText(col?'BODY CONTOUR CANDIDATE':'CURRENT PRODUCT',x+18,22);
  cc.imageSmoothingEnabled=false;cc.drawImage(im,x+112,32,96,96);
  cc.fillStyle='#999';cc.font='12px sans-serif';cc.fillText('NATIVE 96px',x+120,146);
  cc.drawImage(im,16,58,64,34,x+32,172,256,136);
  cc.drawImage(im,16,58,64,34,x+32,326,256,136);
 }
 fs.writeFileSync(path.join(out,'comparison-compact.png'),compact.toBuffer('image/png'));
 console.log(JSON.stringify({out,width,ink}));
})();
