const assert=require('node:assert/strict');
require('./raster-harness.cjs').renderer(null,{authored:true}).then(({rig,renderer})=>{
  // The foreleg cleft moves from x26 to x32.5 as the feet gather. It must
  // travel as one feature, not leave a translucent notch at the old position.
  const frame=renderer.render(rig.pose({sit:.5})),d=frame.getContext('2d').getImageData(0,0,256,256).data;
  const pixel=(x,y)=>{const i=((Math.round((y+16)*2))*256+Math.round((x+16)*2))*4;return Array.from(d.slice(i,i+4));};
  assert.ok(pixel(27,79)[3]>240,`old foreleg cleft remains instead of travelling: ${pixel(27,79)}`);
  assert.ok(pixel(30,81)[3]<60,`moving foreleg cleft is filled instead of separated: ${pixel(30,81)}`);
  return require('./raster-harness.cjs').renderer(ctx=>{ctx.fillStyle='#00c800';ctx.fillRect(57,69,2,2);},{authored:true});
}).then(({rig,renderer})=>{
  // Track an interior rump feature, independent of the white silhouette.
  // Its authored correspondence is (58,70)->(58,72); halfway is (58,71).
  const d=renderer.render(rig.pose({sit:.5})).getContext('2d').getImageData(0,0,256,256).data;
  const i=(174*256+148)*4;
  assert.ok(d[i+1]-d[i]>50,`rump feature did not travel to its intermediate position: ${Array.from(d.slice(i,i+4))}`);
  console.log('PASS: cleft and interior rump features travel with their corresponding anatomy');
}).catch(e=>{console.error(e);process.exitCode=1;});
