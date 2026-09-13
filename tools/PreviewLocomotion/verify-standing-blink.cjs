const assert=require('node:assert/strict');
require('./raster-harness.cjs').renderer().then(({renderer,rig})=>{
  const pose=rig.pose({walk:0,sit:0});
  const open=renderer.render({...pose,blink:false}).getContext('2d').getImageData(0,0,256,256).data;
  const closed=renderer.render({...pose,blink:true}).getContext('2d').getImageData(0,0,256,256).data;
  let changed=0;
  for(let y=0;y<256;y++)for(let x=0;x<256;x++){
    const i=(y*256+x)*4;
    if(open.slice(i,i+4).every((v,c)=>v===closed[i+c]))continue;
    assert.ok(x>=60&&x<132&&y>=112&&y<160,`standing blink changed body at ${x},${y}`);changed++;
  }
  assert.ok(changed>100,'closed eye art missing');
  console.log('PASS: generated standing blink changes eye region only');
}).catch(e=>{console.error(e);process.exitCode=1;});
