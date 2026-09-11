// Static visual experiment only: not consumed by the production exporter.
const rig=require('./rig.js');
const clamp=x=>Math.max(0,Math.min(1,x));
const smooth=x=>{x=clamp(x);return x*x*(3-2*x);};
function contours(data){
  const alpha=(x,y)=>x<0||x>=96||y<0||y>=96?0:data[(y*96+x)*4+3]/255;
  const segments=[],adj=new Map(),key=p=>p.map(v=>v.toFixed(6)).join(',');
  function add(a,b){const i=segments.length;segments.push([a,b]);for(const p of [a,b]){const k=key(p);if(!adj.has(k))adj.set(k,[]);adj.get(k).push(i);}}
  for(let y=-1;y<96;y++)for(let x=-1;x<96;x++){
    const p=[[x+.5,y+.5],[x+1.5,y+.5],[x+1.5,y+1.5],[x+.5,y+1.5]];
    const a=[alpha(x,y),alpha(x+1,y),alpha(x+1,y+1),alpha(x,y+1)],hits=[];
    for(let i=0;i<4;i++){const j=(i+1)%4;if((a[i]>=.5)===(a[j]>=.5))continue;const t=(.5-a[i])/(a[j]-a[i]);hits.push(p[i].map((v,k)=>v+t*(p[j][k]-v)));}
    if(hits.length===2)add(...hits);
    else if(hits.length===4){add(hits[0],hits[1]);add(hits[2],hits[3]);}
  }
  const used=new Set(),loops=[];
  for(let i=0;i<segments.length;i++){
    if(used.has(i))continue;used.add(i);const loop=[...segments[i]],start=key(loop[0]);
    while(key(loop.at(-1))!==start){
      const tip=key(loop.at(-1)),next=adj.get(tip).find(n=>!used.has(n));if(next===undefined)throw new Error('Open silhouette contour');
      used.add(next);const edge=segments[next];loop.push(key(edge[0])===tip?edge[1]:edge[0]);
    }
    loop.pop();if(loop.length>8)loops.push(loop);
  }
  return loops;
}
function resample(loop,step=.5){
  const lengths=[0];for(let i=0;i<loop.length;i++)lengths.push(lengths.at(-1)+Math.hypot(...loop[i].map((v,k)=>loop[(i+1)%loop.length][k]-v)));
  const count=Math.ceil(lengths.at(-1)/step),points=[];let j=0;
  for(let n=0;n<count;n++){const d=n*lengths.at(-1)/count;while(lengths[j+1]<d)j++;const t=(d-lengths[j])/(lengths[j+1]-lengths[j]);points.push(loop[j].map((v,k)=>v+t*(loop[(j+1)%loop.length][k]-v)));}
  return points;
}
function roundContour(loop){
  const p=resample(loop),n=p.length;
  return p.map((point,i)=>{
    const out=[0,0];let weight=0;
    for(let j=-9;j<=9;j++){const w=Math.exp(-.5*(j*.5/1.8)**2),q=p[(i+j+n)%n];weight+=w;out[0]+=q[0]*w;out[1]+=q[1]*w;}
    const amount=smooth((rig.headDistance(...point)-2)/2);
    return point.map((v,k)=>v+(out[k]/weight-v)*amount);
  });
}
function distance(point,loops){
  let best=Infinity;
  for(const loop of loops)for(let i=0;i<loop.length;i++){
    const a=loop[i],b=loop[(i+1)%loop.length],dx=b[0]-a[0],dy=b[1]-a[1],t=clamp(((point[0]-a[0])*dx+(point[1]-a[1])*dy)/(dx*dx+dy*dy));
    best=Math.min(best,Math.hypot(point[0]-a[0]-t*dx,point[1]-a[1]-t*dy));
  }
  return best;
}
async function createCandidate(before,canvas){
  const original=before.getContext('2d').getImageData(0,0,96,96).data;
  const loops=contours(original),rounded=loops.map(roundContour),mask=new Float32Array(96*96);
  let targetInk=0;const colors=[];
  for(let y=0;y<96;y++)for(let x=0;x<96;x++){
    const i=(y*96+x)*4,d=rig.headDistance(x+.5,y+.5);
    if(d<=2)continue;
    const rim=distance([x+.5,y+.5],loops);
    mask[y*96+x]=smooth((d-2)/2)*(1-smooth((rim-1.35)/1.0));
    targetInk+=(255-Math.min(...original.subarray(i,i+3)))*original[i+3]/255;
    if(rim<1.2&&original[i+3]>220&&Math.max(...original.subarray(i,i+3))<110)colors.push([...original.subarray(i,i+3)]);
  }
  if(!colors.length)throw new Error('No opaque body outline samples');
  colors.sort((a,b)=>a[0]+a[1]+a[2]-b[0]-b[1]-b[2]);
  const ink=colors[Math.floor(colors.length*.25)];
  const hi=canvas.createCanvas(768,768),hc=hi.getContext('2d');
  function render(width){
    hc.resetTransform();hc.clearRect(0,0,768,768);hc.save();hc.scale(8,8);hc.beginPath();
    for(const loop of rounded){hc.moveTo(...loop[0]);for(let i=1;i<loop.length;i++)hc.lineTo(...loop[i]);hc.closePath();}
    hc.fillStyle='white';hc.fill('evenodd');hc.clip('evenodd');hc.strokeStyle=`rgb(${ink.join(',')})`;hc.lineWidth=width*2;hc.lineJoin='round';hc.lineCap='round';hc.stroke();hc.restore();
    // Area-integrate all 8x8 samples. A single bilinear minification lookup
    // can miss the thin supersampled boundary and reintroduce stair steps.
    const highPixels=hc.getImageData(0,0,768,768).data,vector=new Uint8ClampedArray(original.length);
    for(let y=0;y<96;y++)for(let x=0;x<96;x++){
      const sum=[0,0,0,0];
      for(let dy=0;dy<8;dy++)for(let dx=0;dx<8;dx++){const j=((y*8+dy)*768+x*8+dx)*4,a=highPixels[j+3];sum[3]+=a;for(let c=0;c<3;c++)sum[c]+=highPixels[j+c]*a;}
      const i=(y*96+x)*4;vector[i+3]=sum[3]/64;for(let c=0;c<3;c++)vector[i+c]=sum[3]?sum[c]/sum[3]:0;
    }
    const out=new Uint8ClampedArray(original);let inkMass=0;
    for(let n=0;n<mask.length;n++){
      const w=mask[n],i=n*4;
      if(w>0){const alpha=original[i+3]*(1-w)+vector[i+3]*w;out[i+3]=alpha;for(let c=0;c<3;c++)out[i+c]=alpha?(original[i+c]*original[i+3]*(1-w)+vector[i+c]*vector[i+3]*w)/alpha:0;}
      const x=n%96,y=Math.floor(n/96);if(rig.headDistance(x+.5,y+.5)>2)inkMass+=(255-Math.min(...out.subarray(i,i+3)))*out[i+3]/255;
    }
    return {pixels:out,inkMass};
  }
  // Calibrate to the current body ink, rather than choosing a heavier stroke.
  let low=.15,high=1.8;
  for(let i=0;i<12;i++){const mid=(low+high)/2;if(render(mid).inkMass<targetInk)low=mid;else high=mid;}
  const width=(low+high)/2,result=render(width),image=canvas.createCanvas(96,96),ctx=image.getContext('2d'),data=ctx.createImageData(96,96);data.data.set(result.pixels);ctx.putImageData(data,0,0);
  return {image,width,ink,loops:loops.length};
}
module.exports={createCandidate};
