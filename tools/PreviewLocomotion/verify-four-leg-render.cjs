const assert=require('node:assert/strict');
const {create}=require('./four-leg-harness.cjs');
(async()=>{
  const current=await create(),withoutPaw=await create({oldRenderer:true}),original=await create({oldRenderer:true,oldRig:true});
  let visibleFrames=0,maxAddedArea=0,maxPixelCount=0;
  for(let phase=0;phase<32;phase++){
    const pose=current.rig.pose({walk:1,distance:phase*10/32});
    const a=withoutPaw.native(pose).data,b=current.native(pose).data;
    let addedArea=0,visible=0;
    for(let y=0;y<96;y++)for(let x=0;x<96;x++){
      const i=(y*96+x)*4,delta=b[i+3]-a[i+3];
      assert.ok(delta>=-1,'rear-layer paw erased foreground coverage');
      if(x<44||x>65||y<71||y>84) {
        for(let k=0;k<4;k++)assert.equal(b[i+k],a[i+k],`hidden paw escaped its lower-body region at phase ${phase},${x},${y}`);
      }
      addedArea+=Math.max(0,delta)/255;
      if(delta>40)visible++;
    }
    if(visible>=3)visibleFrames++;
    maxAddedArea=Math.max(maxAddedArea,addedArea);maxPixelCount=Math.max(maxPixelCount,visible);
  }
  assert.ok(visibleFrames>=8,`fourth paw never meaningfully appears: ${visibleFrames}/32 phases`);
  assert.ok(maxAddedArea<40,`occluded paw is too exposed: ${maxAddedArea} native pixels`);
  // Every seated frame and both standing-eye endpoints stay exactly as before.
  for(const blink of [false,true])for(let n=0;n<=64;n++){
    const p={...current.rig.pose({sit:n/64}),blink},old={...original.rig.pose({sit:n/64}),blink};
    assert.deepEqual(current.native(p).data,original.native(old).data,`idle/seated artwork changed at ${n},blink=${blink}`);
  }
  // Reveal the rear paw gradually with gait amplitude; no silhouette pop.
  for(const phase of [0,2.5,5,7.5]){
    const stopped=current.native(current.rig.pose({walk:0,distance:phase})).data;
    const almost=current.native(current.rig.pose({walk:1e-7,distance:phase})).data;
    let alphaError=0;for(let i=3;i<stopped.length;i+=4)alphaError+=Math.abs(stopped[i]-almost[i]);
    assert.ok(alphaError/(96*96)<.15,'fourth paw pops in when starting');
  }
  console.log(JSON.stringify({visibleFrames,maxAddedArea,maxPixelCount,seatedAndStandingFramesPreserved:130}));
  console.log('PASS: small walking-only rear paw, foreground unchanged, seated/idle preserved, gradual gait entry');
})().catch(error=>{console.error(error);process.exitCode=1});
