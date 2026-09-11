const path=require('node:path'),fs=require('node:fs');
const canvas=require('C:/Users/tjdwo/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas');
(async()=>{
 const output=path.resolve(__dirname,'../../artifacts/repro/hunt-product-20260911');
 const proof=canvas.createCanvas(768,416),c=proof.getContext('2d');
 for(let row=0;row<2;row++)for(const [column,name] of ['rest','hunt','up','down'].entries()){
  const x=column*192,y=row*208;c.fillStyle=row?'#111':'#fff';c.fillRect(x,y,192,208);
  const image=await canvas.loadImage(path.join(output,'native-'+name+'.png'));c.drawImage(image,x,y,192,192);
  c.fillStyle=row?'#eee':'#222';c.font='12px sans-serif';c.fillText(name+' / native WPF',x+10,y+201);
 }
 fs.writeFileSync(path.join(output,'wpf-white-black.png'),proof.toBuffer('image/png'));
})().catch(e=>{console.error(e);process.exitCode=1});
