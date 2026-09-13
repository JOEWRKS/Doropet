(function(root){
  const rig=root.locomotion;
  const foreground=[[0,0],[96,0],[96,48],[69,48],[69,51],[67,55],[65,59],[63,63],[60,62],[58,58],[56,62],[52,66],[49,70],[46,72],[43,72],[42,68],[23,68],[20,70],[17,70],[0,70]];
  function inside(poly,x,y){let yes=false;for(let i=0,j=poly.length-1;i<poly.length;j=i++){
    const a=poly[i],b=poly[j];if((a[1]>y)!=(b[1]>y)&&x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0])yes=!yes;
  }return yes;}
  function protectedAt(x,y){
    x+=.5;y+=.5;if(inside(foreground,x,y))return true;
    return foreground.some((a,i)=>{const b=foreground[(i+1)%foreground.length],dx=b[0]-a[0],dy=b[1]-a[1],t=Math.max(0,Math.min(1,((x-a[0])*dx+(y-a[1])*dy)/(dx*dx+dy*dy)));
      return Math.hypot(x-a[0]-t*dx,y-a[1]-t*dy)<=.8;});
  }
  function createRenderer(image,closed,sleep,finalSeated,finalBank){
    if(!sleep)throw new Error('Sleeping source artwork is required for the seated rear');
    const input=document.createElement('canvas');input.width=input.height=96;
    const ictx=input.getContext('2d',{willReadFrequently:true});ictx.drawImage(image,0,0);
    const original=ictx.getImageData(0,0,96,96),parts=Array.from({length:5},()=>new Float32Array(96*96*4));
    // Remove only faint exterior rump specks with no adjacent visible edge.
    // Keep the connected antialias fringe; never edit the source asset on disk.
    const sourceAlpha=original.data.slice();
    for(let y=60;y<96;y++)for(let x=60;x<96;x++){
      const i=(y*96+x)*4;if(!sourceAlpha[i+3]||sourceAlpha[i+3]>=40)continue;
      let attached=false;for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
        const xx=x+dx,yy=y+dy;if(xx>=0&&xx<96&&yy>=0&&yy<96&&sourceAlpha[(yy*96+xx)*4+3]>=40)attached=true;
      }
      if(!attached)original.data.fill(0,i,i+4);
    }
    ictx.putImageData(original,0,0);
    const authored=finalSeated&&!finalBank?root.authoredSit.create(input,finalSeated):null;
    // Trace the authored *outer* boundary, not the head/body ownership cut.
    const outline=[];
    const alpha=(x,y)=>x<0||y<0||x>=96||y>=96?0:original.data[(y*96+x)*4+3]/255;
    for(let y=47;y<89;y++)for(let x=12;x<85;x++){
      const q=[[x,y],[x+1,y],[x+1,y+1],[x,y+1]],cuts=[];
      for(let e=0;e<4;e++){const a=q[e],b=q[(e+1)%4],aa=alpha(...a),ab=alpha(...b);
        if((aa>=.5)===(ab>=.5))continue;const t=(.5-aa)/(ab-aa);
        cuts.push([a[0]+.5+(b[0]-a[0])*t,a[1]+.5+(b[1]-a[1])*t]);
      }
      for(let k=0;k+1<cuts.length;k+=2){const a=cuts[k],b=cuts[k+1],mx=(a[0]+b[0])/2,my=(a[1]+b[1])/2;
        if(rig.headDistance(mx,my)>1.5)outline.push([a,b,mx,my]);
      }
    }
    // Order the actual authored rear silhouette, then move corresponding
    // contour landmarks into a rounded tucked paw. No distance/color dissolve.
    const rearEdges=outline.filter(e=>e[2]>=50),key=p=>p.map(v=>Math.round(v*1e5)).join(','),links=new Map();
    rearEdges.forEach((edge,i)=>edge.slice(0,2).forEach(p=>{const k=key(p);if(!links.has(k))links.set(k,[]);links.get(k).push(i);}));
    const used=new Set(),chains=[];
    for(let n=0;n<rearEdges.length;n++)if(!used.has(n)){
      const edge=rearEdges[n],ends=edge.slice(0,2),start=ends.find(p=>links.get(key(p)).length===1)||ends[0];
      const chain=[start];let at=start,index=n;
      while(index!==undefined&&!used.has(index)){used.add(index);const e=rearEdges[index];at=key(e[0])===key(at)?e[1]:e[0];chain.push(at);index=links.get(key(at)).find(i=>!used.has(i));}
      chains.push(chain);
    }
    const rearArc=chains.sort((a,b)=>b.length-a.length)[0];if(rearArc[0][1]>rearArc.at(-1)[1])rearArc.reverse();
    // Take the actual sleeping rump boundary. Only translate it to the standing
    // forefeet's floor; never invent another haunch or curled paw with Beziers.
    const sleepCanvas=document.createElement('canvas');sleepCanvas.width=sleepCanvas.height=96;
    const sleepCtx=sleepCanvas.getContext('2d');sleepCtx.drawImage(sleep,0,0);
    const sleepPixels=sleepCtx.getImageData(0,0,96,96).data,sleepEdges=[],sleepLinks=new Map();
    const kneeCanvas=document.createElement('canvas');kneeCanvas.width=kneeCanvas.height=96;
    const kneeCtx=kneeCanvas.getContext('2d'),kneeInk=kneeCtx.createImageData(96,96);
    for(let y=72;y<=81;y++)for(let x=57;x<=67;x++){
      let strength=0;for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
        const xx=x+dx,yy=y+dy;if(xx<58||xx>66||yy<73||yy>80)continue;
        const j=(yy*96+xx)*4;
        strength=Math.max(strength,Math.min(1,(255-Math.min(sleepPixels[j],sleepPixels[j+1],sleepPixels[j+2]))/110)-Math.hypot(dx,dy)*.8);
      }
      const i=(y*96+x)*4;kneeInk.data.set([80,52,61,Math.max(0,strength)*255],i);
    }kneeCtx.putImageData(kneeInk,0,0);
    for(let y=50;y<88;y++)for(let x=50;x<88;x++){
      const q=[[x,y],[x+1,y],[x+1,y+1],[x,y+1]],cuts=[];
      for(let e=0;e<4;e++){const a=q[e],b=q[(e+1)%4],aa=sleepPixels[(a[1]*96+a[0])*4+3]/255,ab=sleepPixels[(b[1]*96+b[0])*4+3]/255;
        if((aa>=.5)===(ab>=.5))continue;const t=(.5-aa)/(ab-aa);cuts.push([a[0]+.5+(b[0]-a[0])*t,a[1]+.5+(b[1]-a[1])*t]);}
      for(let k=0;k+1<cuts.length;k+=2)sleepEdges.push([cuts[k],cuts[k+1]]);
    }
    sleepEdges.forEach((e,i)=>e.forEach(p=>{if(!sleepLinks.has(key(p)))sleepLinks.set(key(p),[]);sleepLinks.get(key(p)).push(i);}));
    const sleepUsed=new Set(),sleepChains=[];
    for(let n=0;n<sleepEdges.length;n++)if(!sleepUsed.has(n)){
      // Start at an endpoint of this connected component, not an interior edge.
      const component=new Set([n]),pending=[n];while(pending.length){const i=pending.pop();for(const p of sleepEdges[i])for(const j of sleepLinks.get(key(p)))if(!component.has(j)){component.add(j);pending.push(j);}}
      let start=sleepEdges[n][0],index=n;for(const i of component){const end=sleepEdges[i].find(p=>sleepLinks.get(key(p)).length===1);if(end){start=end;index=i;break;}}
      const chain=[start];let at=start;
      while(index!==undefined&&!sleepUsed.has(index)){sleepUsed.add(index);const e=sleepEdges[index];at=key(e[0])===key(at)?e[1]:e[0];chain.push(at);index=sleepLinks.get(key(at)).find(i=>!sleepUsed.has(i));}
      sleepChains.push(chain);
    }
    const sleepArc=sleepChains.sort((a,b)=>b.length-a.length)[0];if(sleepArc[0][1]>sleepArc.at(-1)[1])sleepArc.reverse();
    const lengths=points=>{const d=[0];for(let i=1;i<points.length;i++)d.push(d.at(-1)+Math.hypot(points[i][0]-points[i-1][0],points[i][1]-points[i-1][1]));return d;};
    const sourceLengths=lengths(rearArc),sleepLengths=lengths(sleepArc);
    const seatedArc=rearArc.map((a,i)=>{
      const distance=sourceLengths[i]/sourceLengths.at(-1)*sleepLengths.at(-1);let j=1;while(j<sleepLengths.length-1&&sleepLengths[j]<distance)j++;
      const t=(distance-sleepLengths[j-1])/(sleepLengths[j]-sleepLengths[j-1]);
      const b=sleepArc[j-1].map((v,k)=>v+(sleepArc[j][k]-v)*t+(k===1?3:0));
      // Short hidden joins only. The visible rump and hindpaw keep source shape.
      const entry=1-rig.smooth(sourceLengths[i]/7),exit=1-rig.smooth((sourceLengths.at(-1)-sourceLengths[i])/7);
      return b.map((v,k)=>v+(rearArc[0][k]-sleepArc[0][k]-(k===1?3:0))*entry+(rearArc.at(-1)[k]-sleepArc.at(-1)[k]-(k===1?3:0))*exit);
    });
    // Let the belly meet the back of the planted front leg below its old
    // standing root. Keeping that high root created a sharp six-pixel notch.
    // Extend only along the authored rear edge of that paw; its sole stays put.
    const bridgeUsed=new Set();let bridgePoint=rearArc.at(-1);
    while(bridgePoint[1]<83.8){
      const edgeIndex=outline.findIndex((e,i)=>!bridgeUsed.has(i)&&e[2]<49.5&&e[3]>77&&(key(e[0])===key(bridgePoint)||key(e[1])===key(bridgePoint)));
      if(edgeIndex<0)throw new Error('Unable to connect the seated belly to the front paw');
      bridgeUsed.add(edgeIndex);const edge=outline[edgeIndex];bridgePoint=key(edge[0])===key(bridgePoint)?edge[1]:edge[0];
      rearArc.push(bridgePoint);seatedArc.push(bridgePoint.slice());
    }
    let bridgeStart=seatedArc.length-1;while(bridgeStart>0&&seatedArc[bridgeStart][0]<61)bridgeStart--;
    const bridgeRight=seatedArc[bridgeStart],bridgeLeft=rearArc.at(-1),bridgeDistances=lengths(rearArc.slice(bridgeStart));
    const bridgeC1=[bridgeLeft[0]+1,bridgeLeft[1]-2.35],bridgeC2=[bridgeRight[0]-4,bridgeRight[1]];
    for(let i=bridgeStart;i<seatedArc.length;i++){
      const t=1-bridgeDistances[i-bridgeStart]/bridgeDistances.at(-1),v=1-t;
      seatedArc[i]=bridgeLeft.map((a,k)=>v**3*a+3*v*v*t*bridgeC1[k]+3*v*t*t*bridgeC2[k]+t**3*bridgeRight[k]);
    }
    const rearCanvas=document.createElement('canvas');rearCanvas.width=rearCanvas.height=256;const rearCtx=rearCanvas.getContext('2d');
    function drawSeatedRear(amount){
      rearCtx.clearRect(0,0,256,256);rearCtx.beginPath();
      rearArc.forEach((a,i)=>{const b=seatedArc[i],x=(a[0]+(b[0]-a[0])*amount+16)*2,y=(a[1]+(b[1]-a[1])*amount+16)*2;i?rearCtx.lineTo(x,y):rearCtx.moveTo(x,y);});
      // Continue underneath the protected head and existing forebody. A direct
      // chord between the arc endpoints would cut a diagonal hole in the torso.
      rearCtx.lineTo(122,(rearArc.at(-1)[1]+16)*2);rearCtx.lineTo(122,120);
      rearCtx.closePath();rearCtx.fillStyle='white';rearCtx.fill();rearCtx.save();rearCtx.globalCompositeOperation='source-atop';rearCtx.strokeStyle='#50343d';rearCtx.lineWidth=5.6;rearCtx.lineJoin=rearCtx.lineCap='round';
      rearCtx.beginPath();rearArc.forEach((a,i)=>{const b=seatedArc[i],x=(a[0]+(b[0]-a[0])*amount+16)*2,y=(a[1]+(b[1]-a[1])*amount+16)*2;i?rearCtx.lineTo(x,y):rearCtx.moveTo(x,y);});rearCtx.stroke();
      // Copy the authored folded-knee ink itself, including its diagonal slope.
      // Reinforce that thin source ink without importing any white crop box.
      rearCtx.globalAlpha=rig.smooth((amount-.15)/.85);rearCtx.globalCompositeOperation='source-atop';
      rearCtx.save();rearCtx.translate(-3*amount,(83.75+16)*2);rearCtx.scale(1,1-.35*amount);rearCtx.translate(0,-(83.75+16)*2);
      rearCtx.drawImage(kneeCanvas,32,38,192,192);
      rearCtx.restore();
      rearCtx.restore();
      return rearCtx.getImageData(0,0,256,256);
    }
    for(let y=0;y<96;y++)for(let x=0;x<96;x++){
      const i=(y*96+x)*4,isHead=protectedAt(x,y),owner=isHead?4:3;
      const a=original.data[i+3];for(let c=0;c<4;c++)parts[owner][i+c]=c===3?a:original.data[i+c]*a/255;
    }
    const closedHead=parts[4].slice();
    if(closed){ictx.clearRect(0,0,96,96);ictx.drawImage(closed,0,0);const data=ictx.getImageData(0,0,96,96).data;
      for(let y=0;y<96;y++)for(let x=0;x<96;x++)if(protectedAt(x,y)){const i=(y*96+x)*4,a=data[i+3];for(let c=0;c<4;c++)closedHead[i+c]=c===3?a:data[i+c]*a/255;}
      ictx.putImageData(original,0,0);
    }
    // Existing body texels extend underneath the stationary foreground edge.
    // Never use face/hair/ribbon pixels as body donors.
    const donor=parts[3].slice();
    const stationaryBody=donor.slice();
    let bodyMeshLeft=96,bodyMeshTop=96;
    for(let y=0;y<96;y++)for(let x=0;x<96;x++)if(protectedAt(x,y)){
      let best=5,chosen=-1;for(let dy=-2;dy<=2;dy++)for(let dx=-2;dx<=2;dx++){
        const xx=x+dx,yy=y+dy,d=dx*dx+dy*dy;if(xx<0||xx>=96||yy<0||yy>=96||d>=best)continue;
        const j=(yy*96+xx)*4;if(donor[j+3]>0){best=d;chosen=j;}
      }if(chosen>=0){
        bodyMeshLeft=Math.min(bodyMeshLeft,x);bodyMeshTop=Math.min(bodyMeshTop,y);
        const target=(y*96+x)*4;
        stationaryBody.set(donor.subarray(chosen,chosen+4),target);
        // Right of the foreground's (69,48) corner, a donor is no longer
        // hidden behind the ribbon at transparent/soft edge pixels. Extending
        // it there creates a small opaque spur when the head rolls away.
        const exteriorJoin=x>=69&&y<50;
        if(!exteriorJoin)parts[3].set(donor.subarray(chosen,chosen+4),target);
        else{
          const coverage=original.data[target+3]>=254?Math.min(1,original.data[target+3]/donor[chosen+3]):0;
          for(let c=0;c<4;c++)parts[3][target+c]=donor[chosen+c]*coverage;
        }
      }
    }
    // Independent head rotation needs an articulated upper-back connection,
    // not the old stationary cap. Only the isolated preview layer omits it;
    // the compositor supplies the connection to the current ribbon position.
    const articulatedBody=parts[3].slice();
    for(let y=0;y<74;y++)for(let x=67;x<96;x++)articulatedBody.fill(0,(y*96+x)*4,(y*96+x)*4+4);
    // The old front cap likewise cannot stay at the old chin position when
    // looking upward. Keep the authored paw below the short neck join intact.
    for(let y=0;y<73;y++)for(let x=0;x<20;x++)articulatedBody.fill(0,(y*96+x)*4,(y*96+x)*4+4);
    const bounds=parts.map(p=>{let l=96,t=96,r=0,b=0;for(let y=0;y<96;y++)for(let x=0;x<96;x++)if(p[(y*96+x)*4+3]){l=Math.min(l,x);t=Math.min(t,y);r=Math.max(r,x);b=Math.max(b,y);}return [l-1,t-1,r+1,b+1];});
    // Keep the original two-pixel mesh lattice when trimming donor coverage.
    // Moving its origin would retriangulate/resample unrelated body texels.
    bounds[3][0]=Math.min(bounds[3][0],bodyMeshLeft-1);bounds[3][1]=Math.min(bounds[3][1],bodyMeshTop-1);
    const surface=document.createElement('canvas');surface.width=surface.height=256;
    const ctx=surface.getContext('2d'),layer=document.createElement('canvas');layer.width=layer.height=256;
    const lctx=layer.getContext('2d'),raster=lctx.createImageData(256,256),pixels=raster.data;
    // The three authored inner paw joins can lose ink when their texels warp.
    // Match only a measured deficit there; the rest of the walking outline
    // comes from the approved body texture without a second painted stroke.
    const jointSites=[[27,76],[34,79],[61,78]];
    function jointInk(data,center,normal){
      let mass=0;
      for(let t=-4;t<=4;t+=.25){
        const px=(center[0]+16)*2+normal[0]*t,py=(center[1]+16)*2+normal[1]*t;
        const x=Math.floor(px),y=Math.floor(py),fx=px-x,fy=py-y;
        for(let dy=0;dy<2;dy++)for(let dx=0;dx<2;dx++){
          const i=((y+dy)*256+x+dx)*4,w=(dx?fx:1-fx)*(dy?fy:1-fy);
          mass+=(255-Math.min(data[i],data[i+1],data[i+2]))*data[i+3]/255*w*.25;
        }
      }
      return mass;
    }
    const restCanvas=document.createElement('canvas');restCanvas.width=restCanvas.height=256;
    const restCtx=restCanvas.getContext('2d');restCtx.drawImage(input,32,32,192,192);
    const restPixels=restCtx.getImageData(0,0,256,256).data;
    const restingJointInk=jointSites.map(point=>jointInk(restPixels,point,[-1,0]));
    const protectedNativeMask=new Uint8Array(96*96);
    for(let y=0;y<96;y++)for(let x=0;x<96;x++)protectedNativeMask[y*96+x]=protectedAt(x,y)?1:0;
    function sample(tex,u,v,o){
      const x=Math.floor(u),y=Math.floor(v),fx=u-x,fy=v-y;
      let alpha=0,colors=[0,0,0];
      for(let dy=0;dy<2;dy++)for(let dx=0;dx<2;dx++){
        const xx=x+dx,yy=y+dy;if(xx<0||xx>=96||yy<0||yy>=96)continue;
        const w=(dx?fx:1-fx)*(dy?fy:1-fy),i=(yy*96+xx)*4;
        alpha+=tex[i+3]*w;for(let c=0;c<3;c++)colors[c]+=tex[i+c]*w;
      }
      pixels[o+3]=alpha;for(let c=0;c<3;c++)pixels[o+c]=alpha>0?colors[c]*255/alpha:0;
    }
    function triangle(tex,sa,sb,sc,a,b,c){
      a=a.map(v=>(v+16)*2);b=b.map(v=>(v+16)*2);c=c.map(v=>(v+16)*2);
      const den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1]);if(Math.abs(den)<1e-7)return;
      const l=Math.max(0,Math.floor(Math.min(a[0],b[0],c[0]))),r=Math.min(255,Math.ceil(Math.max(a[0],b[0],c[0])));
      const t=Math.max(0,Math.floor(Math.min(a[1],b[1],c[1]))),bot=Math.min(255,Math.ceil(Math.max(a[1],b[1],c[1])));
      for(let y=t;y<=bot;y++)for(let x=l;x<=r;x++){
        const wa=((b[1]-c[1])*(x+.5-c[0])+(c[0]-b[0])*(y+.5-c[1]))/den;
        const wb=((c[1]-a[1])*(x+.5-c[0])+(a[0]-c[0])*(y+.5-c[1]))/den,wc=1-wa-wb;
        if(wa<-.00001||wb<-.00001||wc<-.00001)continue;
        // Mesh coordinates describe pixel edges; bilinear texels are centers.
        sample(tex,wa*sa[0]+wb*sb[0]+wc*sc[0]-.5,wa*sa[1]+wb*sb[1]+wc*sc[1]-.5,(y*256+x)*4);
      }
    }
    function render(p,{bodyOnly=false}={}){
      ctx.clearRect(0,0,256,256);
      if((authored||finalBank)&&p.sit>0){
        if(finalBank){const frame=Math.round(Math.max(0,Math.min(1,p.sit))*64);ctx.drawImage(finalBank,(frame%8)*256,Math.floor(frame/8)*256,256,256,0,0,256,256);}
        else ctx.drawImage(authored.render(p.sit,2),32,32);
        if(p.blink&&closed){ctx.save();ctx.beginPath();ctx.rect(72,114,58,42);ctx.clip();ctx.drawImage(closed,32,32,192,192);ctx.restore();}
        return surface;
      }
      // Exact standing endpoint uses the same authored bitmap path as the
      // reference panel, rather than re-filtering five stationary layers.
      if(!bodyOnly&&p.sit===0&&p.bob===0&&p.roll===0&&p.legs.every((l,i)=>l.tip.every((v,k)=>v===rig.rest[i].tip[k]))){
        ctx.drawImage(input,32,32,192,192);
        // A standing blink changes only the eyes. Rebuilding stationaryBody
        // here exposed old upper-rump seam donors for the closed-eye interval.
        if(p.blink&&closed){ctx.save();ctx.beginPath();ctx.rect(72,114,58,42);ctx.clip();ctx.drawImage(closed,32,32,192,192);ctx.restore();}
        return surface;
      }
      if(p.walk>0&&p.sit===0){
        // Reuse the transparent authored hindpaw, not a new vector outline or
        // the reference JPEG's matte. Reference1-2 places the distant paw below
        // the belly, partially hidden by the closer fore/hind legs. Draw it
        // first so the continuous body naturally occludes its attachment.
        pixels.fill(0);
        const near=rig.rest[2].tip,far=rig.rest[3].tip;
        const map=(x,y)=>rig.limb(far[0]+(x-near[0])*.72,far[1]+(y-near[1])*.84,3,p);
        for(let y=72;y<86;y+=2)for(let x=58;x<74;x+=2){
          const a=[x,y],b=[x+2,y],c=[x+2,y+2],d=[x,y+2];
          const aa=map(...a),bb=map(...b),cc=map(...c),dd=map(...d);
          triangle(donor,a,b,c,aa,bb,cc);triangle(donor,a,c,d,aa,cc,dd);
        }
        lctx.putImageData(raster,0,0);
        ctx.save();ctx.globalAlpha=p.walk;ctx.drawImage(layer,0,0);ctx.restore();
      }
      // One continuous body texture and field: no independently rotating cuts,
      // alpha-composited paw seams or fabricated white patches at attachments.
      const moving=p,bodyTex=bodyOnly?articulatedBody:p.walk>0?parts[3]:stationaryBody;
      for(const index of (bodyOnly?[3]:[3,4])){
        pixels.fill(0);const tex=index===4?(p.blink?closedHead:parts[4]):bodyTex,[l,t,r,b]=index===3?[bounds[3][0],bounds[3][1],84,88]:bounds[index];
        const map=index===4?(x,y)=>{const a=p.roll,dx=x-43,dy=y-48;return [43+dx*Math.cos(a)-dy*Math.sin(a),48+dx*Math.sin(a)+dy*Math.cos(a)+p.bob];}
          :(x,y)=>rig.body(x,y,moving);
        const grid=[];for(let y=t;y<=b+2;y+=2){const row=[];for(let x=l;x<=r+2;x+=2)row.push(map(x,y));grid.push(row);}
        for(let y=t;y<b;y+=2)for(let x=l;x<r;x+=2){
          const a=[x,y],b=[x+2,y],c=[x+2,y+2],d=[x,y+2];
          const ix=(x-l)/2,iy=(y-t)/2,aa=grid[iy][ix],bb=grid[iy][ix+1],cc=grid[iy+1][ix+1],dd=grid[iy+1][ix];
          triangle(tex,a,b,c,aa,bb,cc);triangle(tex,a,c,d,aa,cc,dd);
        }
        lctx.putImageData(raster,0,0);
        if(index===3&&(p.walk>0||p.sit>0)){
          lctx.save();lctx.globalCompositeOperation='source-atop';
          lctx.globalAlpha=Math.min(1,p.walk+p.sit);lctx.strokeStyle='#50343d';lctx.lineWidth=5.6;lctx.lineCap=lctx.lineJoin='round';lctx.beginPath();
          for(const [a,b,mx,my] of outline){
            if(p.sit===0&&my<70)continue;
            if(p.walk===0&&mx<50)continue;
            const aa=map(...a),bb=map(...b);lctx.moveTo((aa[0]+16)*2,(aa[1]+16)*2);lctx.lineTo((bb[0]+16)*2,(bb[1]+16)*2);
          }
          lctx.stroke();lctx.restore();
          // Reinforce only: never lighten original ink or change coverage.
          const ink=lctx.getImageData(0,0,256,256);
          for(let i=0;i<pixels.length;i+=4){for(let c=0;c<3;c++)ink.data[i+c]=Math.min(ink.data[i+c],pixels[i+c]);ink.data[i+3]=pixels[i+3];}
          if(p.sit===0&&p.walk>0){
            const repairs=jointSites.map(([x,y],index)=>{
              const center=map(x,y),a=map(x,y-.1),b=map(x,y+.1),length=Math.hypot(b[0]-a[0],b[1]-a[1]);
              const normal=[-(b[1]-a[1])/length,(b[0]-a[0])/length];
              const before=jointInk(pixels,center,normal),after=jointInk(ink.data,center,normal);
              return {center,strength:Math.max(0,Math.min(1,(restingJointInk[index]-before)/Math.max(1e-8,after-before)))};
            });
            for(let y=0;y<256;y++)for(let x=0;x<256;x++){
              const i=(y*256+x)*4;if(!pixels[i+3])continue;
              const sx=(x+.5)/2-16,sy=(y+.5)/2-16;
              // Preserve every subpixel of the protected native foreground,
              // including its antialias fringe before the native downsample.
              if(sx>=0&&sy>=0&&sx<96&&sy<96&&protectedNativeMask[Math.floor(sy)*96+Math.floor(sx)])continue;
              let strength=0;
              for(const repair of repairs)strength=Math.max(strength,repair.strength*rig.smooth((2.5-Math.hypot(sx-repair.center[0],sy-repair.center[1]))/1));
              for(let c=0;c<3;c++)ink.data[i+c]=pixels[i+c]+(ink.data[i+c]-pixels[i+c])*strength;
            }
          }
          lctx.putImageData(ink,0,0);
        }
        if(index===3&&p.sit>0){
          const seated=drawSeatedRear(p.sit),combined=lctx.getImageData(0,0,256,256);
          // Match the original soft pixel coverage at entry; finish the ink
          // handoff before the rear moves visibly away from its source shape.
          // Both paths already move, so this is not an endpoint-image dissolve.
          const inkMix=rig.smooth(p.sit/.12);
          for(let y=0;y<256;y++)for(let x=125;x<256;x++){
            const sx=(x+.5)/2-16,sy=(y+.5)/2-16;if(protectedAt(Math.floor(sx),Math.floor(sy)))continue;
            const i=(y*256+x)*4,oldAlpha=combined.data[i+3],newAlpha=seated.data[i+3],alpha=oldAlpha*(1-inkMix)+newAlpha*inkMix;
            if(x<129){
              // Erase the now-internal standing-root ink under the bridge,
              // without replacing the paw silhouette or its planted sole.
              if(sy>75&&newAlpha>240){
                for(let c=0;c<3;c++)combined.data[i+c]=alpha?((1-inkMix)*combined.data[i+c]*oldAlpha+inkMix*seated.data[i+c]*newAlpha)/alpha:0;
                combined.data[i+3]=alpha;
              }
              continue;
            }
            for(let c=0;c<3;c++)combined.data[i+c]=alpha?((1-inkMix)*combined.data[i+c]*oldAlpha+inkMix*seated.data[i+c]*newAlpha)/alpha:0;
            combined.data[i+3]=alpha;
          }lctx.putImageData(combined,0,0);
          // The authored front soles sit at81.75 and87.25, while the sleeping
          // rear rests at83.75. Align only the seated lower body to that shared
          // floor. The upper body/head and all walking frames are untouched.
          const aligned=lctx.createImageData(256,256);aligned.data.set(combined.data);
          for(let x=32;x<150;x++){
            const sx=(x+.5)/2-16,front=rig.smooth((sx-27)/7),rear=rig.smooth((sx-47)/12);
            const shift=p.sit*(2*(1-front)-3.5*front*(1-rear));if(Math.abs(shift)<1e-9)continue;
            for(let y=170;y<224;y++){
              const target=(y+.5)/2-16;let sy=target;
              for(let iteration=0;iteration<6;iteration++){
                const u=Math.max(0,Math.min(1,(sy-69)/9)),weight=u*u*(3-2*u),derivative=u>0&&u<1?6*u*(1-u)/9:0;
                sy-=(sy+shift*weight-target)/(1+shift*derivative);
              }
              const sampleY=(sy+16)*2-.5,lo=Math.floor(sampleY),f=sampleY-lo,i=(y*256+x)*4,j=(lo*256+x)*4,k=j+1024;
              const a=combined.data[j+3]*(1-f)+combined.data[k+3]*f;
              for(let c=0;c<3;c++)aligned.data[i+c]=a?(combined.data[j+c]*combined.data[j+3]*(1-f)+combined.data[k+c]*combined.data[k+3]*f)/a:0;
              aligned.data[i+3]=a;
            }
          }lctx.putImageData(aligned,0,0);
        }
        ctx.drawImage(layer,0,0);
      }
      return surface;
    }
    return {render,original:input,parts};
  }
  root.createLocomotionRenderer=createRenderer;
})(globalThis);
