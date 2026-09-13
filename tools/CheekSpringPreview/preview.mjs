import {releaseRemaining,releaseOffset,settlingMs} from './motion.mjs';
const canvas=document.querySelector('canvas'),ctx=canvas.getContext('2d'),out=document.querySelector('output'),range=document.querySelector('input');
const banks=await Promise.all(['old','new'].map(name=>new Promise((resolve,reject)=>{const image=new Image();image.onload=()=>resolve(image);image.onerror=reject;image.src=`/${name}.png`;})));
let value=0,released=null,origin=0,heldOffset=0,down=false,pressX=0,pressPull=0,mirror=false,demoTimer=0;
function hold(amount,offset=0){clearTimeout(demoTimer);released=null;heldOffset=offset;value=Math.max(-10,Math.min(20,amount));range.value=Math.max(0,value);}
function release(){if(released!==null)return;origin=value;released=performance.now();}
document.querySelector('#hold').onclick=()=>hold(20);
document.querySelector('#release').onclick=release;
document.querySelector('#demo').onclick=()=>{hold(20);demoTimer=setTimeout(release,500);};
document.querySelector('#light').onclick=()=>{hold(5);demoTimer=setTimeout(release,500);};
document.querySelector('#flip').onclick=()=>{mirror=!mirror;};
document.querySelector('#theme').onclick=()=>document.body.classList.toggle('dark');
range.oninput=()=>hold(Number(range.value));
canvas.onpointerdown=e=>{const elapsed=released===null?0:performance.now()-released;hold(released===null?value:origin*releaseRemaining(elapsed),released===null?heldOffset:releaseOffset(elapsed,origin,heldOffset));down=true;pressX=e.clientX;pressPull=value;canvas.setPointerCapture(e.pointerId);canvas.style.cursor='grabbing';};
canvas.onpointermove=e=>{if(!down)return;const scale=canvas.getBoundingClientRect().width/1024*2;hold(pressPull+(e.clientX-pressX)*(mirror?1:-1)/scale,heldOffset);};
function end(){if(!down)return;down=false;canvas.style.cursor='grab';release();}
canvas.onpointerup=end;canvas.onpointercancel=end;canvas.onlostpointercapture=end;
function drawBank(bank,pull,center,y,scale,offset=0){const index=Math.max(0,Math.min(120,Math.round((pull+10)*4))),size=bank.width;ctx.save();ctx.translate(center+(mirror?-1:1)*offset*scale,y);ctx.scale(mirror?-scale:scale,scale);ctx.drawImage(bank,0,index*size,size,size,-size/2,-size/2,size,size);ctx.restore();}
function paint(now){
  const elapsed=released===null?0:now-released;
  const newPull=released===null?value:origin*releaseRemaining(elapsed);
  const offset=released===null?heldOffset:releaseOffset(elapsed,origin,heldOffset);
  const oldPull=newPull; // Same cheek/timing; compare baseline vs grounded body reaction.
  ctx.clearRect(0,0,1024,330);ctx.imageSmoothingEnabled=true;
  for(let i=0;i<2;i++){const pull=i?newPull:oldPull;drawBank(banks[i],pull,256+i*512,124,2,i?offset:0);drawBank(banks[i],pull,256+i*512,278,1,i?offset:0);}
  out.textContent=released===null?`${value<0?'눌림':'당김'} ${Math.round(Math.abs(value)/20*100)}%`:`놓은 뒤 ${Math.min(Math.round(elapsed),settlingMs)}ms`;
  if(released!==null&&elapsed>=settlingMs){value=0;range.value=0;}
  requestAnimationFrame(paint);
}
requestAnimationFrame(paint);
