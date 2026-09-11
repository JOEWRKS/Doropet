const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const canvas=require(process.env.LOCOMOTION_CANVAS || path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));

const epsilon=1e-8;
function orientation(a,b,c){return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]);}
function onSegment(a,b,p){return Math.abs(orientation(a,b,p))<=epsilon&&p.every((v,k)=>v>=Math.min(a[k],b[k])-epsilon&&v<=Math.max(a[k],b[k])+epsilon);}
function intersects(a,b,c,d){
  const ac=orientation(a,b,c),ad=orientation(a,b,d),ca=orientation(c,d,a),cb=orientation(c,d,b);
  return ((ac>epsilon&&ad< -epsilon)||(ac< -epsilon&&ad>epsilon))&&((ca>epsilon&&cb< -epsilon)||(ca< -epsilon&&cb>epsilon))
    ||onSegment(a,b,c)||onSegment(a,b,d)||onSegment(c,d,a)||onSegment(c,d,b);
}

(async()=>{
  // Expose the actual private contour only inside this test's VM. Keep the
  // preview renderer's public API unchanged and do not duplicate its curves.
  const scope={document:{createElement:()=>canvas.createCanvas(96,96)}};vm.createContext(scope);
  vm.runInContext(fs.readFileSync(path.join(__dirname,'rig.js'),'utf8'),scope);
  const source=fs.readFileSync(path.join(__dirname,'render.js'),'utf8'),marker='return {render,original:input,parts};';
  assert.equal(source.split(marker).length,2,'renderer instrumentation point changed');
  vm.runInContext(source.replace(marker,'return {rearArc,seatedArc,chains};'),scope);
  const image=await canvas.loadImage(path.resolve(__dirname,'../../src/Dororong.App/Assets/dororong-canonical.png'));
  const sleep=await canvas.loadImage(path.resolve(__dirname,'../../src/Dororong.App/Assets/dororong-sleep.png'));
  const {rearArc,seatedArc,chains}=scope.createLocomotionRenderer(image,null,sleep);
  assert.equal(chains.length,1,'authored rear outline must form one connected chain');
  assert.ok(rearArc.length>3);assert.equal(rearArc.length,seatedArc.length);
  assert.ok(rearArc[0][1]<rearArc.at(-1)[1],'rear contour must run from shoulder to underside');
  for(let step=0;step<=1000;step++){
    const amount=step/1000,polygon=rearArc.map((a,i)=>a.map((v,k)=>v+(seatedArc[i][k]-v)*amount));
    // Include the same hidden forebody closure used by drawSeatedRear.
    polygon.push([45,rearArc.at(-1)[1]],[45,44]);
    for(let i=0;i<polygon.length;i++){
      const a=polygon[i],b=polygon[(i+1)%polygon.length];
      assert.ok(Math.hypot(b[0]-a[0],b[1]-a[1])>epsilon,`collapsed contour edge ${i} at sit ${amount}`);
      for(let j=i+2;j<polygon.length;j++){
        if(i===0&&j===polygon.length-1)continue; // Shared closing vertex.
        assert.ok(!intersects(a,b,polygon[j],polygon[(j+1)%polygon.length]),`rear outline intersects itself at sit ${amount}, edges ${i} and ${j}`);
      }
    }
  }
  console.log('PASS: connected, non-crossing seated contour across 1001 transition samples, including both endpoints');
})().catch(error=>{console.error(error);process.exitCode=1;});
