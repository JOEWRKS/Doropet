// Test/proof-only renderer factory. Baseline code is immutable evidence,
// never selected by product rendering or by a user preference switch.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const canvas=require(process.env.LOCOMOTION_CANVAS||path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));
const root=path.resolve(__dirname,'../..'),proof=path.join(root,'artifacts/repro/four-leg-walk-20260910');
const fixtures=path.join(__dirname,'fixtures/four-leg-baseline');
async function create({oldRenderer=false,oldRig=false}={}){
  const scope={document:{createElement:()=>canvas.createCanvas(96,96)}};vm.createContext(scope);
  vm.runInContext(fs.readFileSync(oldRig?path.join(fixtures,'baseline-rig.js'):path.join(__dirname,'rig.js'),'utf8'),scope);
  vm.runInContext(fs.readFileSync(oldRenderer?path.join(fixtures,'baseline-render.js'):path.join(__dirname,'render.js'),'utf8'),scope);
  const assets=path.join(root,'src/Dororong.App/Assets'),load=p=>canvas.loadImage(p);
  const renderer=scope.createLocomotionRenderer(await load(path.join(assets,'dororong-canonical.png')),await load(path.join(assets,'dororong-closed-eyes.png')),await load(path.join(assets,'dororong-sleep.png')),null,await load(path.join(__dirname,'assets/seated-final-10-2-bank.png')));
  const surface=canvas.createCanvas(96,96),ctx=surface.getContext('2d');
  function native(p){ctx.clearRect(0,0,96,96);ctx.drawImage(renderer.render(p),32,32,192,192,0,0,96,96);return ctx.getImageData(0,0,96,96);}
  return {renderer,rig:scope.locomotion,canvas,native};
}
module.exports={create,proof};
