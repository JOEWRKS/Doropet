// Product layer generation; no source PNG mutation. Normal preview rendering
// remains the visual authority; only static body pixels are precomputed.
const fs=require('node:fs'),path=require('node:path'),zlib=require('node:zlib');
const {create}=require('./four-leg-harness.cjs'),hunt=require('./hunt-motion.js');
const compose=require('./gaze-compose.js'),eyes=require('./gaze-eyes.js');
(async()=>{
 const {renderer,rig,canvas}=await create(),make=(w,h)=>canvas.createCanvas(w,h);
 const root=path.resolve(__dirname,'../..'),assets=path.join(root,'src/Dororong.App/Assets');
 const atlas=make(4096,2560),ac=atlas.getContext('2d');rig.body=(x,y,p)=>hunt.map(x,y,p.hunt);
 for(let f=0;f<157;f++){const p=rig.pose();p.hunt=hunt.sample(f/60);ac.drawImage(renderer.render(p,{bodyOnly:true}),f%16*256,Math.floor(f/16)*256);}
 const heads=[];
 const closed=await canvas.loadImage(path.join(assets,'dororong-closed-eyes.png'));
 for(const eyeClosed of [false,true]){
  const head=make(96,96),hc=head.getContext('2d');if(eyeClosed)hc.drawImage(closed,0,0);
  const hd=hc.getImageData(0,0,96,96),part=renderer.parts[4];
  for(let i=0;i<part.length;i+=4){
   if(eyeClosed){if(!part[i+3])hd.data.fill(0,i,i+4);}
   else {hd.data[i+3]=part[i+3];for(let c=0;c<3;c++)hd.data[i+c]=part[i+3]?part[i+c]*255/part[i+3]:0;}
  }hc.putImageData(hd,0,0);heads.push(head);
 }
 const comp=compose.create(atlas,heads[0],eyes.create(heads[0],make),make);
 const chunks=[],header=Buffer.alloc(20);header.write('HUNT');[1,256,256,157].forEach((v,i)=>header.writeUInt32LE(v,4+i*4));chunks.push(header);
 const premult=data=>{const b=Buffer.alloc(data.length);for(let i=0;i<b.length;i+=4){const a=data[i+3];b[i]=Math.round(data[i+2]*a/255);b[i+1]=Math.round(data[i+1]*a/255);b[i+2]=Math.round(data[i]*a/255);b[i+3]=a;}return b;};
 for(let f=0;f<157;f++)chunks.push(premult(comp.render(f,hunt.sample(f/60).amount,{eyeX:0,eyeY:0,roll:0},{layerOnly:true}).getContext('2d').getImageData(0,0,256,256).data));
 for(const head of heads){
  chunks.push(Buffer.from(head.getContext('2d').getImageData(0,0,96,96).data));
  const base=make(384,384);base.getContext('2d').drawImage(head,0,0,384,384);
  chunks.push(Buffer.from(base.getContext('2d').getImageData(0,0,384,384).data));
 }
 const dest=path.join(assets,'hunting.pbgra.gz');fs.writeFileSync(dest,zlib.gzipSync(Buffer.concat(chunks),{level:9}));
 const out=path.join(root,'artifacts/repro/hunt-product-20260911');fs.mkdirSync(out,{recursive:true});
 const native=make(96,96),nc=native.getContext('2d');
 const fixtures=path.join(__dirname,'fixtures/approved-hunt-2799');fs.mkdirSync(fixtures,{recursive:true});
 for(const [name,frame,gaze] of [['rest',156,{eyeX:0,eyeY:0,roll:0}],['hunt',60,{eyeX:0,eyeY:0,roll:0}],['up',60,{eyeX:-.85,eyeY:-.65,roll:Math.PI/9}],['down',60,{eyeX:.85,eyeY:.65,roll:-Math.PI/9}]]){
  nc.clearRect(0,0,96,96);nc.drawImage(comp.render(frame,hunt.sample(frame/60).amount,gaze),32,32,192,192,0,0,96,96);
  fs.writeFileSync(path.join(out,'preview-'+name+'.png'),native.toBuffer('image/png'));
  // Approved references are not silently blessed again on future exports.
  const fixture=path.join(fixtures,name+'.png');
  if(!fs.existsSync(fixture))fs.writeFileSync(fixture,native.toBuffer('image/png'));
 }
 console.log('Embedded hunt layers '+fs.statSync(dest).size+' bytes');
})().catch(e=>{console.error(e);process.exitCode=1});
