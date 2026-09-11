// Bake the approved preview, preserving its native coordinate system.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),zlib=require('node:zlib'),crypto=require('node:crypto');
const canvas=require(process.env.LOCOMOTION_CANVAS || path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
(async()=>{
  const scope={document:{createElement:()=>canvas.createCanvas(96,96)}}; vm.createContext(scope);
  for(const file of ['rig.js','../GenerateLayeredPull/contour.js','sit-correspondence.js','authored-sit.js','render.js'])vm.runInContext(fs.readFileSync(path.join(__dirname,file),'utf8'),scope);
  const assets=path.resolve(__dirname,'../../src/Dororong.App/Assets'),load=p=>canvas.loadImage(p);
  const renderer=scope.createLocomotionRenderer(await load(path.join(assets,'dororong-canonical.png')),await load(path.join(assets,'dororong-closed-eyes.png')),await load(path.join(assets,'dororong-sleep.png')),null,await load(path.join(__dirname,'assets/seated-final-10-2-bank.png')));
  const native=canvas.createCanvas(96,96),ctx=native.getContext('2d'),frames=[],rows=[];
  const proof=process.argv[2]?path.resolve(process.argv[2]):path.resolve(__dirname,'../../artifacts/repro/locomotion-product');fs.mkdirSync(proof,{recursive:true});
  function add(p,name){
    ctx.clearRect(0,0,96,96);ctx.imageSmoothingEnabled=true;
    ctx.drawImage(renderer.render(p),32,32,192,192,0,0,96,96);
    const rgba=ctx.getImageData(0,0,96,96).data,bytes=Buffer.alloc(rgba.length);
    for(let i=0;i<bytes.length;i+=4){const a=rgba[i+3];bytes[i]=Math.round(rgba[i+2]*a/255);bytes[i+1]=Math.round(rgba[i+1]*a/255);bytes[i+2]=Math.round(rgba[i]*a/255);bytes[i+3]=a;}
    rows.push({index:frames.length,name,sha256:sha(bytes)});frames.push(bytes);
    if(['sit-open-0','sit-open-32','sit-open-64','walk-open-8-8'].includes(name))fs.writeFileSync(path.join(proof,name+'.png'),native.toBuffer('image/png'));
  }
  for(const blink of [false,true]){
    const eyes=blink?'closed':'open';
    for(let n=0;n<=64;n++)add({...scope.locomotion.pose({sit:n/64}),blink},`sit-${eyes}-${n}`);
    for(let amplitude=1;amplitude<=8;amplitude++)for(let phase=0;phase<32;phase++)add({...scope.locomotion.pose({walk:amplitude/8,distance:phase*10/32}),blink},`walk-${eyes}-${amplitude}-${phase}`);
  }
  const header=Buffer.alloc(20);header.write('LOCO');[1,96,96,frames.length].forEach((v,i)=>header.writeUInt32LE(v,4+i*4));
  const raw=Buffer.concat([header,...frames]),gz=zlib.gzipSync(raw,{level:9});fs.writeFileSync(path.join(assets,'locomotion.pbgra.gz'),gz);
  const sourceFiles=['rig.js','render.js','authored-sit.js','sit-correspondence.js','../GenerateLayeredPull/contour.js','export-authored-sit.cjs','export-product.cjs','assets/seated-final-10-2.png','assets/seated-final-10-2-bank.png'];
  fs.writeFileSync(path.join(__dirname,'product-manifest.json'),JSON.stringify({format:'LOCO v1: magic, uint32le version,width,height,count; tightly packed Pbgra32',crop:[32,32,192,192],size:[96,96],sitSamples:65,walkPhases:32,walkAmplitudes:8,eyes:['open','closed'],sources:Object.fromEntries(sourceFiles.map(p=>[p,sha(fs.readFileSync(path.join(__dirname,p)))])),rawSha256:sha(raw),frames:rows},null,2)+'\n');
  console.log(`Exported ${frames.length} native96 frozen-ready frames; ${gz.length} compressed bytes; raw SHA256 ${sha(raw)}`);
})().catch(e=>{console.error(e);process.exitCode=1;});
