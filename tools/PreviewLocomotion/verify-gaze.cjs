const assert=require('node:assert/strict'),fs=require('node:fs');
// No tracker is the pre-feature behavior. This catches an inert tracker,
// reversed mirroring, equal eye/head timing, unbounded motion and no return.
const gaze=fs.existsSync(__dirname+'/gaze-motion.js')?require('./gaze-motion.js'):{create:()=>({step:()=>({eyeX:0,eyeY:0,headX:0,headY:0,roll:0})})};
const tracker=gaze.create();
assert.ok(gaze.create().step(1,[110,-85],false,true).roll>.34,'diagonal cursor must reach the requested twenty-degree range');
assert.ok(gaze.create().step(1,[0,-85],false,true).roll>0,'looking upward must raise the left-facing front of the head');
assert.ok(gaze.create().step(1,[0,85],false,true).roll<0,'looking downward must lower the front of the head');
let p=tracker.step(1/60,[120,60],false,true);
assert.ok(p.eyeX>0&&p.eyeY>0,'eyes must respond to the nearby cursor');
assert.equal(p.headX,0,'head gaze rotates instead of translating horizontally');
assert.equal(p.headY,0,'head gaze rotates instead of translating vertically');
assert.ok(p.roll>0&&p.eyeX/.85>p.roll/(Math.PI/9),'eyes arrive before the head angle');
for(let i=0;i<240;i++)p=tracker.step(1/60,[10000,-10000],false,true);
assert.ok(p.eyeX<=.85&&p.eyeY>=-.65&&p.headX===0&&p.headY===0&&Math.abs(p.roll)<=Math.PI/9+1e-12,'twenty-degree maximum without changing eye travel');
const mirrored=gaze.create().step(1,[120,60],true,true);
assert.ok(mirrored.eyeX<0&&mirrored.eyeY>0,'mirror horizontal eye motion only');
for(let i=0;i<180;i++)p=tracker.step(1/60,null,false,false);
assert.ok(Object.values(p).every(v=>Math.abs(v)<.0001),'return smoothly to neutral');
const a=gaze.create(),b=gaze.create();
for(let i=0;i<60;i++)a.step(1/60,[80,-30],false,true);
for(let i=0;i<30;i++)b.step(1/30,[80,-30],false,true);
assert.ok(Math.abs(a.step(0,[80,-30],false,true).eyeX-b.step(0,[80,-30],false,true).eyeX)<1e-8,'time-based, not frame-based lag');
console.log('PASS independent gaze: response, bounds, eye/head lag, mirroring, return, frame-rate independence');
