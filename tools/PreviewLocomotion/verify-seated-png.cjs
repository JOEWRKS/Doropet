// Catch alpha loss from legacy JPEG matting and stale JPG use in the active pipeline.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const {test}=require('node:test');
const canvas=require(process.env.LOCOMOTION_CANVAS||path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));
async function setup(){
  const scope={document:{createElement:()=>canvas.createCanvas(96,96)}};vm.createContext(scope);
  for(const file of ['rig.js','../GenerateLayeredPull/contour.js','sit-correspondence.js','authored-sit.js'])vm.runInContext(fs.readFileSync(path.join(__dirname,file),'utf8'),scope);
  const standing=await canvas.loadImage(path.resolve(__dirname,'../../src/Dororong.App/Assets/dororong-canonical.png'));
  return {scope,standing};
}
test('transparent source keeps border-connected white paint and partial alpha',async()=>{
  const {scope,standing}=await setup(),source=canvas.createCanvas(100,100),ctx=source.getContext('2d');
  ctx.fillStyle='white';ctx.fillRect(0,20,4,4);ctx.fillStyle='rgba(255,255,255,0.5)';ctx.fillRect(0,24,4,1);
  const input=ctx.getImageData(0,0,100,100).data;
  const output=scope.authoredSit.create(standing,source).render(1).getContext('2d').getImageData(0,0,96,96).data;
  assert.equal(output[(8*96+3)*4+3],255,'opaque authored white paint was removed as JPG background');
  assert.equal(output[(12*96+3)*4+3],input[(24*100)*4+3],'authored partial alpha changed');
});
test('supplied transparent seated endpoint is preserved exactly at integer registration',async()=>{
  const {scope,standing}=await setup(),source=await canvas.loadImage(path.join(__dirname,'assets/seated-final-10-2.png'));
  const expected=canvas.createCanvas(96,96);expected.getContext('2d').drawImage(source,3,-12);
  const actual=scope.authoredSit.create(standing,source).render(1);
  assert.ok(Buffer.from(actual.getContext('2d').getImageData(0,0,96,96).data).equals(Buffer.from(expected.getContext('2d').getImageData(0,0,96,96).data)),'import changed supplied PNG pixels');
  assert.equal(actual.getContext('2d').getImageData(35,24,1,1).data[3],67,'head fringe no longer matches user alpha');
});
test('active authored renderer consumes transparent PNG, not the obsolete white-matted JPG',async()=>{
  const {renderer,rig}=await require('./raster-harness.cjs').renderer(null,{authored:true});
  const source=await canvas.loadImage(path.join(__dirname,'assets/seated-final-10-2.png'));
  const native=canvas.createCanvas(96,96);native.getContext('2d').drawImage(source,3,-12);
  const expected=canvas.createCanvas(256,256);expected.getContext('2d').drawImage(native,32,32,192,192);
  const actual=renderer.render(rig.pose({sit:1}));
  assert.ok(Buffer.from(actual.getContext('2d').getImageData(0,0,256,256).data).equals(Buffer.from(expected.getContext('2d').getImageData(0,0,256,256).data)),'active endpoint differs from supplied PNG');
});
