(function(root){
  function makeCanvas(w=96,h=96){const c=document.createElement('canvas');c.width=w;c.height=h;return c;}
  function create(standing,finalSource){
    const original=makeCanvas(),octx=original.getContext('2d');octx.drawImage(standing,0,0);
    const source=makeCanvas(finalSource.width,finalSource.height),sctx=source.getContext('2d');sctx.drawImage(finalSource,0,0);
    const raw=sctx.getImageData(0,0,source.width,source.height),w=source.width,h=source.height;
    // An authored alpha channel is authoritative, including white paint at the
    // canvas border. Only legacy opaque/JPEG inputs need white-matte removal.
    const hasAuthoredAlpha=raw.data.some((value,index)=>index%4===3&&value<255);
    if(!hasAuthoredAlpha)removeWhiteMatte(raw,w,h,sctx);
    // Integer-only registration measured against unchanged colored head pixels.
    // No scaling, re-drawing, knee replacement, or previous procedural sit art.
    const registration={x:3,y:-12},target=makeCanvas(),tctx=target.getContext('2d');tctx.drawImage(source,registration.x,registration.y);
    const toLayer=c=>{const d=c.getContext('2d').getImageData(0,0,96,96).data,p=new Float32Array(d.length);
      for(let i=0;i<d.length;i+=4){for(let k=0;k<3;k++)p[i+k]=d[i+k]*d[i+3]/255;p[i+3]=d[i+3];}return root.contourTween.prepare(p);
    };
    const a=toLayer(original),b=toLayer(target),surfaces=new Map(),headMasks=new Map(),endpointPixels=new Map();
    function removeWhiteMatte(raw,w,h,sctx){
    const exterior=new Uint8Array(w*h),queue=[];
    function visit(x,y){if(x<0||y<0||x>=w||y>=h)return;const n=y*w+x,i=n*4;
      if(exterior[n]||Math.min(raw.data[i],raw.data[i+1],raw.data[i+2])<235)return;
      exterior[n]=1;queue.push(n);
    }
    for(let x=0;x<w;x++){visit(x,0);visit(x,h-1);}for(let y=0;y<h;y++){visit(0,y);visit(w-1,y);}
    for(let at=0;at<queue.length;at++){const n=queue[at],x=n%w,y=Math.floor(n/w);visit(x-1,y);visit(x+1,y);visit(x,y-1);visit(x,y+1);}
    // The JPEG has white already composited into its antialiased black ink.
    // Recover coverage only for neutral body pixels touching the flood-filled
    // exterior. Estimate ink from the darkest adjacent source sample, then
    // invert C = alpha * ink + (1-alpha) * white. This preserves the original
    // white-background drawing without importing an opaque grey outer rim.
    // Read an immutable copy: recovery must not propagate into the white body.
    const jpegPixels=raw.data.slice();
    for(let y=0;y<h;y++)for(let x=0;x<w;x++){
      const n=y*w+x,i=n*4;
      if(exterior[n]||root.locomotion.headDistance(x+3+.5,y-12+.5)<=2)continue;
      const rgb=jpegPixels.slice(i,i+3),light=(rgb[0]+rgb[1]+rgb[2])/3;
      if(Math.max(...rgb)-Math.min(...rgb)>8)continue;
      let boundary=false,ink=light;
      for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
        const xx=x+dx,yy=y+dy;if(xx<0||yy<0||xx>=w||yy>=h)continue;
        const adjacent=yy*w+xx,j=adjacent*4;
        if(exterior[adjacent]){boundary=true;continue;}
        const color=jpegPixels.slice(j,j+3);
        if(Math.max(...color)-Math.min(...color)<=8)ink=Math.min(ink,(color[0]+color[1]+color[2])/3);
      }
      if(!boundary||ink>=150||ink>=light)continue;
      const alpha=(255-light)/(255-ink);
      raw.data[i+3]=Math.round(alpha*255);
      for(let c=0;c<3;c++)raw.data[i+c]=Math.max(0,255+(rgb[c]-255)/alpha);
    }
    for(let n=0;n<exterior.length;n++)if(exterior[n])raw.data.fill(0,n*4,n*4+4);
    sctx.putImageData(raw,0,0);
    }
    function texel(layer,x,y){
      const ix=Math.floor(x),iy=Math.floor(y),fx=x-ix,fy=y-iy,out=[0,0,0,0];
      for(let dy=0;dy<2;dy++)for(let dx=0;dx<2;dx++){const xx=ix+dx,yy=iy+dy;if(xx<0||yy<0||xx>=96||yy>=96)continue;
        const i=(yy*96+xx)*4,w=(dx?fx:1-fx)*(dy?fy:1-fy);for(let k=0;k<4;k++)out[k]+=layer.pixels[i+k]*w;
      }return out;
    }
    function depth(layer,x,y){
      const ix=Math.floor(x),iy=Math.floor(y),fx=x-ix,fy=y-iy;
      const at=(x,y)=>x<0||y<0||x>=96||y>=96?-96:layer.field[y*96+x];
      return (at(ix,iy)*(1-fx)+at(ix+1,iy)*fx)*(1-fy)+(at(ix,iy+1)*(1-fx)+at(ix+1,iy+1)*fx)*fy;
    }
    function render(amount,scale=1){
      if(!Number.isFinite(amount)||![1,2].includes(scale))throw new Error('Finite transition amount and scale1 or2 required');
      const t=Math.max(0,Math.min(1,amount));
      if(!surfaces.has(scale))surfaces.set(scale,makeCanvas(96*scale,96*scale));
      const output=surfaces.get(scale),ctx=output.getContext('2d');ctx.clearRect(0,0,output.width,output.height);
      if(t===0||t===1){ctx.drawImage(t===0?original:target,0,0,output.width,output.height);return output;}
      if(!endpointPixels.has(scale)){
        endpointPixels.set(scale,[original,target].map(image=>{
          const c=makeCanvas(output.width,output.height),context=c.getContext('2d');
          context.drawImage(image,0,0,c.width,c.height);
          return context.getImageData(0,0,c.width,c.height).data;
        }));
      }
      // Match canvas-filtered coverage at both exact endpoints. The narrow
      // premultiplied handoff prevents a one-frame outline pop at 2x export.
      const endpoint=t<.06?0:t>.94?1:-1;
      const coverage=endpoint<0?1:root.locomotion.smooth((endpoint===0?t:1-t)/.06);
      if(!headMasks.has(scale)){
        const mask=new Float32Array(output.width*output.height);
        for(let y=0;y<output.height;y++)for(let x=0;x<output.width;x++)mask[y*output.width+x]=1-root.locomotion.smooth(root.locomotion.headDistance((x+.5)/scale,(y+.5)/scale)/2);
        headMasks.set(scale,mask);
      }
      const frame=ctx.createImageData(output.width,output.height),map=root.sitCorrespondence.createMap(t,scale);
      for(let y=0;y<output.height;y++)for(let x=0;x<output.width;x++){
        const p=[(x+.5)/scale-.5,(y+.5)/scale-.5],weight=headMasks.get(scale)[y*output.width+x],i=(y*output.width+x)*4;
        const aa=texel(a,...p),bb=texel(b,...p),fixed=aa.map((v,k)=>v*(1-t)+bb[k]*t);
        const pa=[map[i],map[i+1]],pb=[map[i+2],map[i+3]];
        // A rounded-cell boolean here made moving knee ink switch by129 RGB
        // levels in one instant. Continuous depth gives a continuous handoff.
        const interior=root.locomotion.smooth((Math.min(depth(a,...pa),depth(b,...pb))-1)/3);
        const ca=texel(a,...pa),cb=texel(b,...pb),transported=ca.map((v,k)=>v*(1-t)+cb[k]*t);
        const contour=weight===1||interior===1?transported:root.contourTween.sample(a,b,pa,pb,t);
        const moved=contour.map((v,k)=>v*(1-interior)+transported[k]*interior),rgba=moved.map((v,k)=>v*(1-weight)+fixed[k]*weight);
        if(endpoint>=0){
          const pixels=endpointPixels.get(scale)[endpoint],alpha=pixels[i+3];
          for(let c=0;c<3;c++)rgba[c]=rgba[c]*coverage+pixels[i+c]*alpha/255*(1-coverage);
          rgba[3]=rgba[3]*coverage+alpha*(1-coverage);
        }
        frame.data[i+3]=rgba[3];for(let c=0;c<3;c++)frame.data[i+c]=rgba[3]?rgba[c]*255/rgba[3]:0;
      }
      ctx.putImageData(frame,0,0);return output;
    }
    return {render,original,target,registration};
  }
  root.authoredSit={create};
})(globalThis);
