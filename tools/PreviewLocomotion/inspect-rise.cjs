const fs=require('node:fs'),path=require('node:path');
require('./raster-harness.cjs').renderer(null,{authored:true}).then(({rig,renderer,canvas})=>{
  const sheet=canvas.createCanvas(1280,640),ctx=sheet.getContext('2d');ctx.fillStyle='#faf8f3';ctx.fillRect(0,0,1280,640);
  for(const [n,sit,blink] of [[0,0,false],[1,0,true],[2,.125,false],[3,.5,false],[4,1,false]]){
    const p=rig.pose({sit});p.blink=blink;
    const frame=renderer.render(p),copy=canvas.createCanvas(256,256);copy.getContext('2d').putImageData(frame.getContext('2d').getImageData(0,0,256,256),0,0);
    ctx.imageSmoothingEnabled=false;ctx.drawImage(copy,50,115,160,110,n*256,0,256,176);
    ctx.drawImage(copy,n*256,200);ctx.fillStyle='#111';ctx.fillText(`${sit}, blink ${blink}`,n*256+10,195);
    const d=copy.getContext('2d').getImageData(0,0,256,256).data,seen=new Uint8Array(65536),components=[];
    for(let i=0;i<65536;i++)if(!seen[i]&&d[i*4+3]>80){const queue=[i];seen[i]=1;let at=0;
      while(at<queue.length){const q=queue[at++],x=q%256,y=Math.floor(q/256);for(const v of [x? q-1:-1,x<255?q+1:-1,y?q-256:-1,y<255?q+256:-1])if(v>=0&&!seen[v]&&d[v*4+3]>80){seen[v]=1;queue.push(v);}}
      components.push({count:queue.length,bounds:[Math.min(...queue.map(v=>v%256)),Math.min(...queue.map(v=>Math.floor(v/256))),Math.max(...queue.map(v=>v%256)),Math.max(...queue.map(v=>Math.floor(v/256)))]});
    }
    console.log({sit,blink,components:components.sort((a,b)=>b.count-a.count).slice(0,10)});
  }
  fs.writeFileSync(path.resolve(__dirname,'../../artifacts/repro/locomotion/rise-diagnostic.png'),sheet.toBuffer('image/png'));
  const grid=canvas.createCanvas(1000,550),g=grid.getContext('2d');g.fillStyle='#fff';g.fillRect(0,0,1000,550);
  for(const [n,sit] of [[0,0],[1,1]]){
    const frame=renderer.render(rig.pose({sit})),copy=canvas.createCanvas(256,256);copy.getContext('2d').putImageData(frame.getContext('2d').getImageData(0,0,256,256),0,0);
    g.imageSmoothingEnabled=false;g.drawImage(copy,52,132,150,90,n*500,30,500,300);
    g.strokeStyle='#0088aa66';g.fillStyle='#003366';g.font='11px sans-serif';
    for(let x=15;x<=80;x+=5){const xx=n*500+(x-10)*20/3;g.beginPath();g.moveTo(xx,30);g.lineTo(xx,330);g.stroke();g.fillText(String(x),xx,22);}
    for(let y=50;y<=95;y+=5){const yy=30+(y-50)*20/3;g.beginPath();g.moveTo(n*500,yy);g.lineTo(n*500+500,yy);g.stroke();g.fillText(String(y),n*500,yy);}
  }
  fs.writeFileSync(path.resolve(__dirname,'../../artifacts/repro/locomotion/rise-grid.png'),grid.toBuffer('image/png'));
});
