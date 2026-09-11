(async function(){
  const $=id=>document.getElementById(id),image=$('sourceCanonical'),closed=$('sourceClosed'),sleep=$('sourceSleep'),finalSeated=$('sourceFinalSeated');
  const finalBank=$('sourceFinalBank');
  await Promise.all([image.decode(),closed.decode(),sleep.decode(),finalSeated.decode(),finalBank.decode()]);
  const renderer=createLocomotionRenderer(image,closed,sleep,finalSeated,finalBank),rig=locomotion,before=$('before').getContext('2d'),after=$('after').getContext('2d');
  let mode='sequence',time=0,paused=false,last=performance.now(),lastRender=-1,frame=null;
  const cache=new Map();
  function stage(ctx,s,original){
    const black=$('black').checked,flip=$('mirror').checked!==!!s.flip;
    ctx.clearRect(0,0,640,280);ctx.fillStyle=black?'#111115':'#fcfaf7';ctx.fillRect(0,0,640,280);
    ctx.strokeStyle=black?'#353039':'#d9cbd0';ctx.lineWidth=1;ctx.beginPath();ctx.moveTo(22,232.5);ctx.lineTo(618,232.5);ctx.stroke();
    for(let x=24+(((s.belt||0)*(flip?-1:1))%32);x<620;x+=32){ctx.beginPath();ctx.moveTo(x,233);ctx.lineTo(x-5,239);ctx.stroke();}
    ctx.save();ctx.translate(320+s.position*($('mirror').checked?-1:1),24);if(flip)ctx.scale(-1,1);ctx.translate(-128,0);
    ctx.imageSmoothingEnabled=true;
    if(original)ctx.drawImage(renderer.original,32,32,192,192);else ctx.drawImage(frame,0,0);
    if(!original&&$('joints').checked){const p=rig.pose(s);ctx.fillStyle='#d55288';for(const leg of p.legs){ctx.beginPath();ctx.arc((leg.tip[0]+16)*2,(leg.tip[1]+16)*2,3,0,Math.PI*2);ctx.fill();}}
    ctx.restore();
  }
  function tick(now){
    const dt=Math.min(.05,(now-last)/1000);last=now;if(!paused)time+=dt*Number($('speed').value);
    const s=rig.scenario(time,mode),p=rig.pose(s);p.blink=time%3.7>3.47&&time%3.7<3.61;
    // Cache phase samples, not alpha crossfades between different silhouettes.
    const key=[Math.round((s.distance%rig.cycle)*12),Math.round(s.walk*40),Math.round(s.sit*64),p.blink].join('/');
    if(key!==lastRender){
      if(cache.has(key))frame=cache.get(key);else{
        const rendered=renderer.render(p),copy=document.createElement('canvas');copy.width=copy.height=256;copy.getContext('2d').drawImage(rendered,0,0);frame=copy;
        if(cache.size>=600)cache.delete(cache.keys().next().value);cache.set(key,copy);
      }lastRender=key;
    }
    stage(before,s,true);stage(after,s,false);$('phase').textContent=s.label;
    $('status').textContent=paused?'정지 · 구간을 움직여 비교':'미리보기 재생 중';
    if(mode==='sequence')$('timeline').value=time%8;
    requestAnimationFrame(tick);
  }
  for(const m of ['sequence','walk','sit','stand'])$(m).onclick=()=>{mode=m;time=0;paused=false;lastRender=-1;$('pause').textContent='일시정지';for(const id of ['sequence','walk','sit','stand'])$(id).classList.toggle('active',id===m);};
  $('pause').onclick=()=>{paused=!paused;$('pause').textContent=paused?'계속 재생':'일시정지';};
  $('timeline').oninput=()=>{mode='sequence';time=Number($('timeline').value);paused=true;lastRender=-1;$('pause').textContent='계속 재생';for(const id of ['sequence','walk','sit','stand'])$(id).classList.toggle('active',id===mode);};
  $('black').onchange=()=>document.body.classList.toggle('dark',$('black').checked);
  $('speed').oninput=()=>$('speedLabel').value=Number($('speed').value).toFixed(1)+'×';
  window.previewControl={set(t,m='sequence'){mode=m;time=t;paused=true;lastRender=-1;},state:()=>({mode,time,paused})};
  requestAnimationFrame(tick);
})().catch(error=>{document.getElementById('status').textContent='미리보기 오류: '+error.message;console.error(error);});
