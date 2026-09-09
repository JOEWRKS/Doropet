const assert=require('node:assert/strict');
const factory=require('./morph-v1.js');
const original=require('./poses.json');
// Two opaque outlined rectangles, translated six pixels. Identity registration
// deliberately exposes a silhouette mismatch instead of hiding it in a fixture.
const rectangle=left=>{
 const p=new Uint8Array(96*96*4);
 for(let y=60;y<=84;y++)for(let x=left;x<left+12;x++){
  const edge=x===left||x===left+11||y===60||y===84;
  p.set([edge?70:255,edge?70:255,edge?70:255,255],(y*96+x)*4);
 }return p;
};
const pair={low:original.pairs[0].low,high:original.pairs[0].low};
const data={heads:Array(8).fill({x:35,y:30}),pairs:Array(7).fill(pair),
 frames:[rectangle(40),...Array.from({length:7},()=>rectangle(46))],contours:true};
const model=factory(data,x=>x),mid=model.sample(.5/7);
const px=(x,y)=>Array.from(mid.slice((y*96+x)*4,(y*96+x)*4+4));
assert.equal(px(41,70)[3],0,'Old contour must move, not remain as a half-transparent ghost');
assert.equal(px(44,70)[3],255,'Moving interior must stay opaque, not fade in');
assert.ok(px(43,70)[0]<130,'A single moving dark outline must remain at the new edge');
assert.ok(px(46,70)[0]>230,'Old/new source edges must not survive inside the moving body');
console.log('PASS: translating silhouette has one opaque body and one moving outline');
const layered=require('./layered.js')(original,s=>new Uint8Array(Buffer.from(s,'base64')));
const arm=layered.sample(24/112);
// The exposed far-paw outer edge at row75 currently has five consecutive
// translucent pixels (x27..31). An antialiased single contour is a narrow band.
let translucent=0;
for(let x=27;x<=32;x++){const a=arm[(75*96+x)*4+3];if(a>15&&a<240)translucent++;}
assert.ok(translucent<=2,`Far forepaw retains a broad double-edge band: ${translucent}`);
for(const [key,x,y] of [[1,49,83],[2,48,82],[3,46,81]]){
 const i=(y*96+x)*4;
 assert.equal(layered.layers[key].rear?.[i+3]??0,255,'Rear paw must own its visible opaque interior separately');
 assert.equal(layered.layers[key].body[i+3],0,'Rear paw must not also deform as torso');
}
// Reviewer reproduced an 44px texture lookup escape at this body correspondence.
// Uniform ink makes the otherwise-white fallback observable, without mocks.
const contour=require('./contour.js');
const uniform=source=>{
 const p=source.slice();for(let i=0;i<p.length;i+=4)for(let c=0;c<3;c++)p[i+c]=Math.round(70*p[i+3]/255);
 return contour.prepare(p);
};
const transported=contour.sample(uniform(layered.layers[2].body),uniform(layered.layers[3].body),
 [49.5,73.92857142857143],[46.5,62.07142857142857],.5);
assert.ok(Math.abs(transported[0]-70*transported[3]/255)<=1,`Contour transport escaped source ink: ${transported}`);
