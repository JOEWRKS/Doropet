// Preview-only gate. Until a candidate exists, measure the unchanged product.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),zlib=require('node:zlib');
const canvas=require(process.env.LOCOMOTION_CANVAS||path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));
const rig=require('./rig.js');
function current(){
 const raw=zlib.gunzipSync(fs.readFileSync(path.resolve(__dirname,'../../src/Dororong.App/Assets/locomotion.pbgra.gz')));
 const c=canvas.createCanvas(96,96),ctx=c.getContext('2d'),im=ctx.createImageData(96,96),offset=20+64*96*96*4;
 for(let i=0;i<im.data.length;i+=4){const a=raw[offset+i+3];im.data[i+3]=a;for(let k=0;k<3;k++)im.data[i+k]=a?Math.round(raw[offset+i+2-k]*255/a):0;}
 ctx.putImageData(im,0,0);return c;
}
function edgeRoughness(pixels,top=69){
 const edge=[];
 // y59..68 crosses the protected ribbon/head join. Measure improvement on
 // the fully editable rump arc; retain the wider measurement as diagnostics.
 for(let y=top;y<=78;y++){
  let x=85;while(x>60&&pixels[(y*96+x)*4+3]<128)x--;
  const a=pixels[(y*96+x)*4+3],b=pixels[(y*96+x+1)*4+3];
  edge.push(x+(128-a)/(b-a));
 }
 let total=0;for(let i=1;i<edge.length-1;i++)total+=(edge[i-1]-2*edge[i]+edge[i+1])**2;
 return Math.sqrt(total/(edge.length-2));
}
async function run(){
 const before=current(),file=path.join(__dirname,'seated-contour-proof.cjs');
 const result=!process.argv.includes('--baseline')&&fs.existsSync(file)?await require(file).createCandidate(before,canvas):{image:before};
 const a=before.getContext('2d').getImageData(0,0,96,96).data,b=result.image.getContext('2d').getImageData(0,0,96,96).data;
 let protectedCount=0,areaA=0,areaB=0,inkA=0,inkB=0,changed=0;
 for(let y=0;y<96;y++)for(let x=0;x<96;x++){
  const i=(y*96+x)*4;
  if(rig.headDistance(x+.5,y+.5)<=2){for(let k=0;k<4;k++)assert.equal(b[i+k],a[i+k],`protected head/ribbon changed at ${x},${y}`);protectedCount++;continue;}
  if(!a.subarray(i,i+4).every((v,k)=>v===b[i+k]))changed++;
  areaA+=a[i+3];areaB+=b[i+3];
  inkA+=(255-Math.min(a[i],a[i+1],a[i+2]))*a[i+3]/255;
  inkB+=(255-Math.min(b[i],b[i+1],b[i+2]))*b[i+3]/255;
 }
 for(const [x,y]of [[60,73],[65,75],[24,75],[39,77]]){
  const i=(y*96+x)*4;assert.ok(b.subarray(i,i+4).every((v,k)=>v===a[i+k]),`body interior changed at ${x},${y}`);
 }
 const roughA=edgeRoughness(a),roughB=edgeRoughness(b);
 console.log(JSON.stringify({protectedCount,changed,areaRatio:areaB/areaA,inkRatio:inkB/inkA,roughA,roughB,includingProtectedJoinBefore:edgeRoughness(a,59),includingProtectedJoinAfter:edgeRoughness(b,59)}));
 assert.ok(Math.abs(areaB/areaA-1)<.02,'seated silhouette changed by >=2%');
 assert.ok(inkB/inkA>.94&&inkB/inkA<1.06,'body stroke mass changed by >=6%');
 assert.ok(roughB<roughA*.8,'outer-rump staircase remains (need >=20% curvature-jitter reduction)');
 console.log('PASS: smoother rump contour, preserved head/interior, bounded silhouette and stroke weight');
 return {before,after:result.image,canvas};
}
module.exports={current,run};
if(require.main===module)run().catch(error=>{console.error(error);process.exitCode=1;});
