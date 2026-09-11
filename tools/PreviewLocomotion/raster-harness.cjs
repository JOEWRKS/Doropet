// Exercise the browser renderer with a real Canvas2D implementation in Node.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const canvas=require(process.env.LOCOMOTION_CANVAS || path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));
async function renderer(paint,{authored=false}={}){
  const scope={document:{createElement:()=>canvas.createCanvas(96,96)}};vm.createContext(scope);
  for(const file of ['rig.js','../GenerateLayeredPull/contour.js','sit-correspondence.js','authored-sit.js','render.js'])vm.runInContext(fs.readFileSync(path.join(__dirname,file),'utf8'),scope);
  const assets=path.resolve(__dirname,'../../src/Dororong.App/Assets');
  let original=await canvas.loadImage(path.join(assets,'dororong-canonical.png'));
  if(paint){const marked=canvas.createCanvas(96,96),ctx=marked.getContext('2d');ctx.drawImage(original,0,0);paint(ctx);original=marked;}
  return {rig:scope.locomotion,renderer:scope.createLocomotionRenderer(original,await canvas.loadImage(path.join(assets,'dororong-closed-eyes.png')),await canvas.loadImage(path.join(assets,'dororong-sleep.png')),authored?await canvas.loadImage(path.join(__dirname,'assets/seated-final-10-2.png')):null),canvas};
}
module.exports={renderer};
if(require.main===module)renderer().then(({rig,renderer,canvas})=>{
  const output=path.resolve(__dirname,'../../artifacts/repro/locomotion');fs.mkdirSync(output,{recursive:true});
  const sheet=canvas.createCanvas(1024,768),ctx=sheet.getContext('2d');ctx.fillStyle='#151419';ctx.fillRect(0,0,1024,768);
  for(let i=0;i<12;i++){
    const p=i<8?rig.pose({walk:1,distance:i*1.25}):rig.pose({sit:(i-8)/3});
    ctx.drawImage(renderer.render(p),(i%4)*256,Math.floor(i/4)*256);ctx.fillStyle='white';ctx.fillText(i<8?'walk '+i*1.25:'sit '+((i-8)/3).toFixed(2),(i%4)*256+12,Math.floor(i/4)*256+20);
  }
  fs.writeFileSync(path.join(output,process.argv[2]||'sheet.png'),sheet.toBuffer('image/png'));
}).catch(e=>{console.error(e);process.exitCode=1;});
