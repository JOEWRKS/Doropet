const fs=require('node:fs'),path=require('node:path');
const {create}=require('./four-leg-harness.cjs'),hunt=require('./hunt-motion.js');
const out=path.resolve(__dirname,'../../artifacts/repro/hunt-preview-20260910');
const port=Number(process.argv.find(a=>a.startsWith('--port='))?.slice(7)||2788);
if(!Number.isInteger(port)||port<1024||port>65535)throw Error('Invalid local preview port');
(async()=>{
  const {renderer,rig,canvas}=await create();
  // Isolated proof override; never write the production rig or sprite bank.
  rig.body=(x,y,p)=>hunt.map(x,y,p.hunt);
  const count=157,atlas=canvas.createCanvas(16*256,10*256),ctx=atlas.getContext('2d');
  const bodyAtlas=canvas.createCanvas(16*256,10*256),bodyContext=bodyAtlas.getContext('2d');
  for(let i=0;i<count;i++){
    const h=hunt.sample(i/60),p=rig.pose();p.hunt=h;p.bob=h.headBob;p.roll=h.headRoll;
    ctx.drawImage(renderer.render(p),i%16*256,Math.floor(i/16)*256);
    bodyContext.drawImage(renderer.render(p,{bodyOnly:true}),i%16*256,Math.floor(i/16)*256);
  }
  fs.mkdirSync(out,{recursive:true});fs.writeFileSync(path.join(out,'atlas.png'),atlas.toBuffer('image/png'));
  fs.writeFileSync(path.join(out,'body-atlas.png'),bodyAtlas.toBuffer('image/png'));
  const head=canvas.createCanvas(96,96),hc=head.getContext('2d'),hd=hc.createImageData(96,96),pixels=renderer.parts[4];
  for(let i=0;i<pixels.length;i+=4){const a=pixels[i+3];hd.data[i+3]=a;for(let c=0;c<3;c++)hd.data[i+c]=a?pixels[i+c]*255/a:0}
  hc.putImageData(hd,0,0);fs.writeFileSync(path.join(out,'head.png'),head.toBuffer('image/png'));
  const sheet=canvas.createCanvas(1024,576),sc=sheet.getContext('2d');
  for(let row=0;row<2;row++)for(let col=0;col<4;col++){
    const age=[0,.65,1,1.2][col],i=Math.round(age*60),x=col*256,y=row*288;
    sc.fillStyle=row?'#101014':'#fcfaf8';sc.fillRect(x,y,256,288);
    sc.drawImage(atlas,i%16*256,Math.floor(i/16)*256,256,256,x,y+24,256,256);
    sc.fillStyle=row?'#eee':'#333';sc.font='14px sans-serif';sc.fillText(age+'s / '+hunt.sample(age).label,x+12,y+22);
  }
  fs.writeFileSync(path.join(out,'contact-sheet.png'),sheet.toBuffer('image/png'));
  const html=fs.readFileSync(path.join(__dirname,'hunt-preview.html'),'utf8');
  fs.writeFileSync(path.join(out,'index.html'),html);
  console.log('Hunt preview: '+out+' / '+count+' frames');
  const files={};
  for(const name of ['hunt-motion.js','gaze-motion.js','gaze-eyes.js','gaze-compose.js'])files['/'+name]=['text/javascript; charset=utf-8',fs.readFileSync(path.join(__dirname,name))];
  for(const name of ['atlas.png','body-atlas.png','head.png'])files['/'+name]=['image/png',fs.readFileSync(path.join(out,name))];
  if(process.argv.includes('--serve'))require('node:http').createServer((req,res)=>{
    const route=req.url?.split('?')[0];
    // Reload only explicitly allowlisted local scripts for iterative previews.
    // This avoids restarting a sprite-bank server for a motion tuning change.
    const script=['/hunt-motion.js','/gaze-motion.js','/gaze-eyes.js','/gaze-compose.js'].includes(route);
    const entry=route==='/'?['text/html; charset=utf-8',fs.readFileSync(path.join(__dirname,'hunt-preview.html'))]:script?['text/javascript; charset=utf-8',fs.readFileSync(path.join(__dirname,route.slice(1)))]:files[route];
    if(!entry){res.writeHead(404);res.end();return;}
    res.writeHead(200,{'Content-Type':entry[0],'Cache-Control':'no-store'});res.end(entry[1]);
  }).listen(port,'127.0.0.1',()=>console.log('http://127.0.0.1:'+port+'/'));
})().catch(e=>{console.error(e);process.exitCode=1});
