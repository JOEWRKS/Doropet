(function(root){
 const hunt=typeof module!=='undefined'?require('./hunt-motion.js'):root.huntMotion;
 function create(bodyAtlas,headImage,eyes,makeCanvas=(w,h)=>{const c=document.createElement('canvas');c.width=w;c.height=h;return c}){
  const surface=makeCanvas(256,256),ctx=surface.getContext('2d');
  // Sample the small connecting curves at the same native density as the
  // authored bitmap before the shared 2x enlargement, avoiding razor-sharp
  // new ink beside the original softly sampled outline.
  const joins=makeCanvas(128,128),ink=joins.getContext('2d');
  const forelegs=makeCanvas(128,128),legInk=forelegs.getContext('2d');
  // Resolve visible outline ownership after body/head compositing. The white
  // neck underlay and mismatched feathered contours can extend beyond their
  // individual strokes. Ink the final boundary, without expanding its alpha.
  const inkBounds={x:64,y:178,w:80,h:30},edgeDistance=new Float32Array(80*30);
  const smooth=v=>{v=Math.max(0,Math.min(1,v));return v*v*(3-2*v)};
  function finishForebodyOutline(amount){
   const {x:ox,y:oy,w,h}=inkBounds,patch=ctx.getImageData(ox,oy,w,h),d=patch.data;
   edgeDistance.fill(99);
   const stamp=(px,py)=>{
    for(let y=Math.max(0,Math.floor(py)-3);y<=Math.min(h-1,Math.ceil(py)+3);y++)for(let x=Math.max(0,Math.floor(px)-3);x<=Math.min(w-1,Math.ceil(px)+3);x++){
     const distance=Math.hypot(x-px,y-py),i=y*w+x;
     if(distance<edgeDistance[i])edgeDistance[i]=distance;
    }
   };
   for(let y=0;y<h-1;y++)for(let x=0;x<w-1;x++){
    const i=(y*w+x)*4,a=d[i+3];
    for(const [dx,dy] of [[1,0],[0,1]]){
     const b=d[i+(dy*w+dx)*4+3];if((a>=128)===(b>=128))continue;
     const t=(127.5-a)/(b-a);stamp(x+dx*t,y+dy*t);
    }
   }
   const visibility=smooth(amount/.2);
   for(let y=0;y<h;y++)for(let x=0;x<w;x++){
    const i=(y*w+x)*4;if(!d[i+3])continue;
    const nx=(ox+x+.5)/2-16,ny=(oy+y+.5)/2-16;
    const region=Math.max(smooth((nx-17)/3)*smooth((32-nx)/3),smooth((nx-38)/4)*smooth((55-nx)/4));
    const weight=visibility*region*smooth((ny-73)/1.5)*smooth((84-ny)/1.5);
    // Body-neutral ink only; never recolor hair, eyes or ribbon as they pass.
    if(d[i]-d[i+1]>65||Math.abs(d[i+1]-d[i+2])>35)continue;
    const coverage=Math.max(0,Math.min(1,(2.4-edgeDistance[y*w+x])/1.6))*weight;
    for(let c=0;c<3;c++)d[i+c]=Math.min(d[i+c],255-(255-[80,52,61][c])*coverage);
   }
   ctx.putImageData(patch,ox,oy);
  }
  const region=makeCanvas(128,128),mask=region.getContext('2d');
  // Replace the two foreleg contours together with their belly connection.
  // The right feather sits inside the unchanged torso, before the hindpaw.
  mask.fillStyle='#fff';mask.fillRect(0,81,62,47);
  const edge=mask.createLinearGradient(62,0,68,0);edge.addColorStop(0,'#fff');edge.addColorStop(1,'transparent');
  mask.fillStyle=edge;mask.fillRect(62,81,6,47);
  function drawForelegs(pose){
   const t=pose.amount;
   legInk.clearRect(0,0,128,128);legInk.save();legInk.translate(16,16);
   // Interpolate corresponding curve controls, not two mismatched bitmaps.
   // Rounded distal arcs and broad roots are independent of the old shear.
   const point=(source,target)=>{const p=hunt.map(...source,pose);return p.map((v,k)=>v+(target[k]-v)*t);};
   const move=(a,b)=>legInk.moveTo(...point(a,b));
   const curve=(a,b,c,d,e,f)=>legInk.bezierCurveTo(...point(a,b),...point(c,d),...point(e,f));
   const outline=()=>{
    move([17,68],[23,68]);
    curve([17,73],[18,71],[18,78],[8,72],[21,80],[9,78]);
    curve([23,82],[9.5,82],[28,80],[17,82],[28,75],[24,76]);
    curve([30,75],[26,74],[33,78],[28,76],[35,78],[34,76]);
    curve([36,81],[31,78],[37,85],[25,79],[40,86],[27,83.5]);
    curve([43,88],[28.5,87],[47,86],[35,87],[48,81],[40,81]);
    curve([48,78],[42,79],[50,78],[47,77],[53,76],[53,76]);
   };
   legInk.beginPath();outline();legInk.lineTo(54,60);legInk.lineTo(14,60);legInk.closePath();legInk.fillStyle='#fff';legInk.fill();
   legInk.beginPath();outline();legInk.strokeStyle='#50343d';legInk.lineWidth=1.15;legInk.lineJoin=legInk.lineCap='round';legInk.stroke();legInk.restore();
   legInk.globalCompositeOperation='destination-in';legInk.drawImage(region,0,0);legInk.globalCompositeOperation='source-over';
   // Preserve the exact original at rest. Only the first small portion of
   // entry blends into its near-identical curve, then the outline morphs.
   const u=Math.min(1,t/.2),blend=u*u*(3-2*u);
   ctx.save();ctx.globalAlpha=blend;ctx.globalCompositeOperation='destination-out';ctx.drawImage(region,0,0,256,256);
   // Add premultiplied replacement coverage to the removed complement.
   // Source-over would attenuate the feather twice and punch an alpha seam.
   ctx.globalCompositeOperation='lighter';ctx.drawImage(forelegs,0,0,256,256);ctx.restore();
  }
  return {render(frame,amount,gaze,{layerOnly=false}={}){
   ctx.clearRect(0,0,256,256);
   ink.clearRect(0,0,128,128);
   // Add gaze as rotation about the neck at authored (43,66), not as a
   // translated head layer. The hunt lowering remains an independent pose.
   // A lowered head already pitches toward the forefeet; reserve a little
   // clearance there without weakening the standing look-only rotation.
   const angle=gaze.roll<0?gaze.roll*(1-amount)+Math.max(gaze.roll,-.08)*amount:gaze.roll;
   const pose=hunt.sample(frame/60);
   const headPoint=(x,y,rotation=angle)=>{
    const dx=x-43,dy=y-66;
    return hunt.head(43+dx*Math.cos(rotation)-dy*Math.sin(rotation),66+dx*Math.sin(rotation)+dy*Math.cos(rotation),pose);
   };
   // Fill the newly exposed front neck, rather than leaving the original
   // ownership cut floating below a raised head. Its short outer curve meets
   // the unchanged front paw; the other edges stay underneath the source art.
   const front=headPoint(16,68),chest=hunt.map(17.2,73,pose);
   ink.save();ink.translate(16,16);
   const frontCurve=()=>ink.bezierCurveTo(front[0]-1,front[1]+(chest[1]-front[1])*.35,chest[0]-.7,chest[1]-1.8,...chest);
   ink.beginPath();ink.moveTo(...front);frontCurve();ink.lineTo(...hunt.map(46,75,pose));ink.lineTo(...headPoint(47,65));ink.closePath();ink.fillStyle='#fff';ink.fill();
   ink.beginPath();ink.moveTo(...front);frontCurve();ink.strokeStyle='#50343d';ink.lineWidth=1.15;ink.lineCap='round';ink.stroke();ink.restore();
   // One rounded haunch from ribbon to hindleg, not a cap stitched onto the
   // old flat side. Both arcs have the same vertical tangent at the widest
   // point; the lower arc turns back into the unchanged authored hindleg.
   // When looking up the ribbon descends over the back. Let it occlude the
   // rounded shoulder instead of squeezing that shoulder into a pointed nub.
   const a=headPoint(69,47.5,Math.min(angle,0)),apex=hunt.map(77,61.5,pose),b=hunt.map(69.8,74,pose),inner=hunt.map(59,74,pose),under=headPoint(59,48);
   // Spread the outward turn vertically. When the ribbon drops toward it,
   // reserve shoulder height instead of compressing the arc into a sharp nub.
   const shoulder=a[1]+6;
   apex[1]=(apex[1]+shoulder+Math.hypot(apex[1]-shoulder,1))/2;
   const curve=()=>{
    ink.bezierCurveTo(a[0]+(apex[0]-a[0])*.55,a[1]+(apex[1]-a[1])*.1,apex[0],apex[1]-(apex[1]-a[1])*.55,...apex);
    ink.bezierCurveTo(apex[0],apex[1]+(b[1]-apex[1])*.55,b[0],b[1]-4,...b);
   };
   ink.save();ink.translate(16,16);
   ink.beginPath();ink.moveTo(...a);curve();ink.lineTo(...inner);ink.lineTo(...under);ink.closePath();ink.fillStyle='#fff';ink.fill();
   ink.beginPath();ink.moveTo(...a);curve();ink.strokeStyle='#50343d';ink.lineWidth=1.15;ink.lineCap='round';ink.stroke();ink.restore();
   if(!layerOnly)ctx.drawImage(joins,0,0,256,256);
   ctx.drawImage(bodyAtlas,frame%16*256,Math.floor(frame/16)*256,256,256,0,0,256,256);
   if(pose.amount>0)drawForelegs(pose);
   if(layerOnly)return surface;
   ctx.save();ctx.translate(118,128+pose.headBob*2);ctx.rotate(pose.headRoll);
   ctx.translate(0,36);ctx.rotate(angle);ctx.translate(0,-36);
   ctx.drawImage(eyes.paint(gaze.eyeX,gaze.eyeY),-86,-96,192,192);ctx.restore();
   if(pose.amount>0)finishForebodyOutline(pose.amount);
   return surface;
  }};
 }
 const api={create};if(typeof module!=='undefined')module.exports=api;else root.gazeCompose=api;
})(globalThis);
