const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const canvas=require(path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));
(async()=>{
  const scope={document:{createElement:()=>canvas.createCanvas(96,96)}};vm.createContext(scope);
  for(const file of ['rig.js','../GenerateLayeredPull/contour.js','sit-correspondence.js','authored-sit.js'])if(fs.existsSync(path.join(__dirname,file)))vm.runInContext(fs.readFileSync(path.join(__dirname,file),'utf8'),scope);
  assert.ok(scope.authoredSit,'authored final-frame transition is missing');
  const original=await canvas.loadImage(path.resolve(__dirname,'../../src/Dororong.App/Assets/dororong-canonical.png'));
  const final=await canvas.loadImage(path.join(__dirname,'assets/seated-final-10-2.png'));
  const transition=scope.authoredSit.create(original,final),raw=canvas.createCanvas(100,100);raw.getContext('2d').drawImage(final,0,0);
  const expected=raw.getContext('2d').getImageData(0,0,100,100).data;
  const end=transition.render(1).getContext('2d').getImageData(0,0,96,96).data;
  for(const [x,y] of [[25,90],[36,92],[60,90],[30,54]]){
    const a=(y*100+x)*4,b=((y-12)*96+x+3)*4;
    assert.deepEqual(Array.from(end.slice(b,b+4)),Array.from(expected.slice(a,a+4)),`final artwork changed at ${x},${y}`);
  }
  assert.equal(end[3],0,'transparent outer background was lost');
  for(const [endpoint,near] of [[0,1e-8],[1,1-1e-8]]){
    const exact=Uint8ClampedArray.from(transition.render(endpoint,2).getContext('2d').getImageData(0,0,192,192).data);
    const adjacent=transition.render(near,2).getContext('2d').getImageData(0,0,192,192).data;
    let max=0;for(let i=3;i<exact.length;i+=4)max=Math.max(max,Math.abs(exact[i]-adjacent[i]));
    assert.ok(max<=1,`2x endpoint coverage pops by ${max} at ${endpoint}`);
  }
  const base=canvas.createCanvas(96,96);base.getContext('2d').drawImage(original,0,0);
  assert.deepEqual(Array.from(transition.render(0).getContext('2d').getImageData(0,0,96,96).data),Array.from(base.getContext('2d').getImageData(0,0,96,96).data),'standing endpoint changed');
  for(const t of [.001,.25,.5,.75,.999]){
    const d=transition.render(t).getContext('2d').getImageData(0,0,96,96).data;
    for(const [x,y] of [[29,72],[40,73],[65,72]])assert.ok(d[(y*96+x)*4+3]>230,`body/ribbon holes during transition at ${t}`);
    for(const [x,y] of [[28,49],[41,54],[35,62],[53,52]]){
      const i=(y*96+x)*4,start=base.getContext('2d').getImageData(0,0,96,96).data;
      for(let c=0;c<3;c++)assert.ok(Math.abs(d[i+c]-(start[i+c]*(1-t)+end[i+c]*t))<=2,`head color was transported at ${x},${y}, ${t}`);
    }
  }
  console.log('PASS: authored endpoint RGB, opaque white body, transparent outside, exact standing endpoint and connected intermediate body');
})().catch(e=>{console.error(e);process.exitCode=1;});
