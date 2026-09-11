const assert=require('node:assert/strict'),{create}=require('./four-leg-harness.cjs'),compose=require('./gaze-compose.js');
(async()=>{const {canvas}=await create(),make=(w,h)=>canvas.createCanvas(w,h),body=make(256,256),fixture=make(384,384),ctx=fixture.getContext('2d');
// Known image landmarks exercise the actual compositor: neck stays fixed,
// while the upper-head marker travels on an arc. This is not a mocked transform.
for(const [x,y,color] of [[43,66,'#00ff00'],[43,36,'#ff0000']]){ctx.fillStyle=color;ctx.beginPath();ctx.arc(x*4,y*4,4,0,2*Math.PI);ctx.fill()}
const comp=compose.create(body,fixture,{paint:()=>fixture},make);
const at=angle=>{const p=comp.render(0,0,{eyeX:0,eyeY:0,headX:0,headY:0,roll:angle}).getContext('2d').getImageData(0,0,256,256).data;let x=0,y=0,n=0;
// Measure green marker excess, not the green channel of the white torso
// underlay. The anatomical bridge is real rendering, not a neck marker.
for(let j=0;j<256;j++)for(let i=0;i<256;i++){const k=(j*256+i)*4,w=Math.max(0,p[k+1]-Math.max(p[k],p[k+2]))*p[k+3];x+=(i+.5)*w;y+=(j+.5)*w;n+=w}return [x/n,y/n]};
const rest=at(0);for(const angle of [-Math.PI/9,Math.PI/9]){const p=at(angle);assert.ok(Math.hypot(p[0]-rest[0],p[1]-rest[1])<.1,'gaze rotation must pin the neck joint')}
console.log('PASS actual head composite rotates around a fixed neck');})();
