const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const canvas=require(process.env.LOCOMOTION_CANVAS||path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));
(async()=>{
  const scope={document:{createElement:()=>canvas.createCanvas(96,96)}};vm.createContext(scope);
  for(const file of ['rig.js','../GenerateLayeredPull/contour.js','sit-correspondence.js','authored-sit.js'])vm.runInContext(fs.readFileSync(path.join(__dirname,file),'utf8'),scope);
  const jpeg=await canvas.loadImage(path.join(__dirname,'assets/seated-final-10-2.jpg'));
  const standing=await canvas.loadImage(path.resolve(__dirname,'../../src/Dororong.App/Assets/dororong-canonical.png'));
  const raw=canvas.createCanvas(jpeg.width,jpeg.height),ctx=raw.getContext('2d');ctx.drawImage(jpeg,0,0);
  const source=ctx.getImageData(0,0,raw.width,raw.height),old=Uint8ClampedArray.from(source.data),exterior=new Set(),queue=[];
  function visit(x,y){if(x<0||y<0||x>=raw.width||y>=raw.height)return;const n=y*raw.width+x,i=n*4;if(exterior.has(n)||Math.min(...old.slice(i,i+3))<235)return;exterior.add(n);queue.push(n);}
  for(let x=0;x<raw.width;x++){visit(x,0);visit(x,raw.height-1);}for(let y=0;y<raw.height;y++){visit(0,y);visit(raw.width-1,y);}
  for(let at=0;at<queue.length;at++){const n=queue[at],x=n%raw.width,y=Math.floor(n/raw.width);visit(x-1,y);visit(x+1,y);visit(x,y-1);visit(x,y+1);}
  for(const n of exterior)old.fill(0,n*4,n*4+4);
  const transition=scope.authoredSit.create(standing,jpeg),actual=transition.render(1).getContext('2d').getImageData(0,0,96,96).data;
  // Regression: the grey JPEG sample at the outside of the right seated paw
  // is mostly white matte, not an opaque grey contour texel.
  const edge=(85*96+67)*4;
  assert.ok(actual[edge+3]<80,`white matte remains opaque at seated (67,85): ${Array.from(actual.slice(edge,edge+4))}`);
  assert.ok(actual[edge]<150,'recovered edge still carries a bright grey foreground');
  let changed=0,protectedCount=0;
  for(let y=0;y<96;y++)for(let x=0;x<96;x++){
    const sx=x-3,sy=y+12;if(sx<0||sy>=raw.height)continue;
    const i=(y*96+x)*4,j=(sy*raw.width+sx)*4,before=Array.from(old.slice(j,j+4)),after=Array.from(actual.slice(i,i+4));
    const protectedPixel=scope.locomotion.headDistance(x+.5,y+.5)<=2;
    if(protectedPixel){assert.deepEqual(after,before,`head/ribbon changed at ${x},${y}`);protectedCount++;}
    if(before.every((v,k)=>v===after[k]))continue;
    changed++;
    assert.ok(!protectedPixel,'matte recovery escaped the body guard');
    assert.ok(before[3]>0&&after[3]>0,'matte recovery erased or added silhouette pixels');
    let touchesOutside=false;for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++)if(exterior.has((sy+dy)*raw.width+sx+dx))touchesOutside=true;
    assert.ok(touchesOutside,`body interior changed at ${x},${y}`);
    for(let c=0;c<3;c++)assert.ok(Math.abs(after[c]*after[3]/255+255-after[3]-before[c])<=2,`white composite changed at ${x},${y}`);
  }
  assert.ok(changed>20,'real body edge coverage was not recovered');
  assert.ok(protectedCount>5000,'head preservation check did not run');
  const outArg=process.argv.indexOf('--proof');
  if(outArg>=0){
    const out=path.resolve(process.argv[outArg+1]);fs.mkdirSync(out,{recursive:true});
    source.data.set(old);ctx.putImageData(source,0,0);const before=canvas.createCanvas(96,96);before.getContext('2d').drawImage(raw,3,-12);
    const after=transition.render(1),sheet=canvas.createCanvas(768,768),sc=sheet.getContext('2d');
    for(const [row,bg] of ['#000000','#ffffff'].entries())for(const [col,frame] of [before,after].entries()){
      sc.fillStyle=bg;sc.fillRect(col*384,row*384,384,384);sc.imageSmoothingEnabled=false;sc.drawImage(frame,col*384,row*384,384,384);
    }
    fs.writeFileSync(path.join(out,'seated-matte-before-after-black-white-4x.png'),sheet.toBuffer('image/png'));
    fs.writeFileSync(path.join(out,'seated-import-before.png'),before.toBuffer('image/png'));
    fs.writeFileSync(path.join(out,'seated-import-after.png'),after.toBuffer('image/png'));
  }
  console.log(`PASS: recovered ${changed} exterior body pixels, ${protectedCount} protected head/ribbon pixels exact; silhouette support, opaque interior and white-background appearance preserved`);
})().catch(e=>{console.error(e);process.exitCode=1;});
