const $=id=>document.getElementById(id),body=new Image,head=new Image;
body.src='/body-atlas.png';head.src='/head.png';
let mode='auto',paused=false,time=0,last=0,mouse=null,composer,painted,prey=null;
let jump,watch,gaze,pose,age=2.6,launchAge=1.5,flip=false,previousCount=0;
function reset(){jump=pounceMotion.create();watch=huntMotion.createSession();gaze=gazeMotion.create();pose=jump.step(0,false,0);time=0;age=2.6;previousCount=0;flip=$('right').checked;}
function choose(value){mode=value;paused=false;reset();$('pause').textContent='일시정지';$('auto').classList.toggle('active',value==='auto');$('mouse').classList.toggle('active',value==='mouse');}
$('auto').onclick=()=>choose('auto');$('mouse').onclick=()=>choose('mouse');$('replay').onclick=()=>choose(mode);$('right').onchange=()=>choose(mode);
$('pause').onclick=()=>{paused=!paused;$('pause').textContent=paused?'계속 재생':'일시정지'};
$('stage').onpointermove=e=>{const r=$('stage').getBoundingClientRect();mouse=[(e.clientX-r.left)*$('stage').width/r.width,(e.clientY-r.top)*300/r.height]};
$('stage').onpointerleave=()=>mouse=null;
function update(dt){
 time+=dt;const center=$('stage').width/2;
 prey=mode==='auto'?[center+($('right').checked?108:-108),172]:mouse;
 const target=prey?(prey[0]-center)/2:NaN;
 const near=!!prey&&((prey[0]-center-pose.x*2)/155)**2+((prey[1]-170)/100)**2<1;
 const oldPhase=pose.phase;pose=jump.step(dt,near,target);
 if(pose.phase==='watch'){
  if(oldPhase==='track')watch=huntMotion.createSession();
  age=watch.update(time,near);if(age<.7)age=Math.min(.7,.25+age*1.8);
  if(near&&Math.abs(target-pose.x)>4)flip=target>pose.x;
 }else if(pose.phase==='flight'||pose.phase==='landing'){
  if(pose.count!==previousCount){launchAge=Math.max(.7,Math.min(1.65,age));flip=pose.direction>0;}
  const u=Math.min(1,pose.age/pounceMotion.flightTime);const eased=u*u*(3-2*u);
  age=pose.phase==='flight'?launchAge+(2.05-launchAge)*eased:2.05+.55*Math.min(1,(pose.age-pounceMotion.flightTime)/pounceMotion.landingTime);
 }else{
  // Post-landing tracks in the upright pose, without preparing a jump.
  age=2.6;flip=pose.direction>0;
 }
 previousCount=pose.count;
 const g=gaze.step(dt,prey?[prey[0]-(center+pose.x*2+(flip?32:-32)),prey[1]-162]:null,flip,pose.phase==='track'?!!prey:near);
 painted=composer.render(Math.min(156,Math.round(age*60)),huntMotion.sample(age).amount,g);
 $('label').textContent=pose.phase==='flight'?'폴짝 · 앞발 내밀기':pose.phase==='landing'?'앞발부터 · 폭신':pose.phase==='track'?'기본 자세 · 시선 추적 · '+pose.trackingRemaining.toFixed(1)+'초 후 준비':near?'집중 중 · '+pose.dwell.toFixed(1)+' / 1.5초':'가까이 오면 준비해요';
 $('status').textContent=pose.count+'회 도약 · 제품 미적용';$('timeline').value=Math.min(7.8,time);
}
function draw(canvas,small){
 const c=canvas.getContext('2d'),w=canvas.width,h=canvas.height,dark=$('black').checked,s=small?1:2,floor=small?110:232;
 c.fillStyle=dark?'#101014':'#fcfaf8';c.fillRect(0,0,w,h);
 c.strokeStyle=dark?'#51414c':'#ddd0d7';c.beginPath();c.moveTo(10,floor+.5);c.lineTo(w-10,floor+.5);c.stroke();
 c.save();c.translate(w/2+pose.x*s,floor+pose.y*s);c.scale(flip?-s/2:s/2,s/2);
 // Rotate around the near forepaw: it plants first while the rear settles.
 c.translate(-34,0);c.rotate(pose.angle);c.scale(pose.scaleX,pose.scaleY);c.drawImage(painted,-94,-204);c.restore();
 if(!small){
  if(prey){c.strokeStyle='#bd7094';c.beginPath();c.arc(...prey,5,0,Math.PI*2);c.stroke();}
  if(mode==='mouse'&&pose.phase==='watch'){c.setLineDash([3,6]);c.strokeStyle=dark?'#493640':'#e9dde3';c.beginPath();c.ellipse(w/2+pose.x*2,170,155,100,0,0,Math.PI*2);c.stroke();c.setLineDash([]);}
 }
}
$('timeline').oninput=e=>{
 if(!composer)return;const end=Number(e.target.value);choose('auto');paused=true;$('pause').textContent='계속 재생';
 while(time+1/120<end)update(1/120);update(Math.max(0,end-time));draw($('stage'),false);draw($('native'),true);
};
function tick(now){
 const dt=last?Math.min(.05,(now-last)/1000):0;last=now;
 const width=Math.round($('stage').clientWidth);if(width&&width!==$('stage').width)$('stage').width=width;
 if(!paused){if(mode==='auto'&&time>8.5)reset();update(dt*Number($('speed').value));}
 draw($('stage'),false);draw($('native'),true);requestAnimationFrame(tick);
}
Promise.all([body,head].map(im=>im.decode())).then(()=>{composer=gazeCompose.create(body,head,gazeEyes.create(head));reset();update(0);requestAnimationFrame(tick)}).catch(e=>{$('label').textContent='이미지 오류: '+e.message});
