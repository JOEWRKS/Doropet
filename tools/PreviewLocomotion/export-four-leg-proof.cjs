const fs=require('node:fs'),path=require('node:path');
const {create,proof}=require('./four-leg-harness.cjs');
(async()=>{
  const old=await create({oldRenderer:true,oldRig:true}),next=await create(),canvas=next.canvas;
  const atlases=[];
  for(const item of [old,next]){
    const atlas=canvas.createCanvas(8*256,4*256),ctx=atlas.getContext('2d');
    for(let phase=0;phase<32;phase++)ctx.drawImage(item.renderer.render(item.rig.pose({walk:1,distance:phase*10/32})),(phase%8)*256,Math.floor(phase/8)*256);
    atlases.push(atlas);
  }
  const sheet=canvas.createCanvas(1024,576),sc=sheet.getContext('2d');
  for(let row=0;row<2;row++)for(let col=0;col<4;col++){
    const x=col*256,y=row*288,phase=col*8;
    sc.fillStyle=row?'#151419':'#faf8f5';sc.fillRect(x,y,256,288);
    sc.drawImage(atlases[1],phase%8*256,Math.floor(phase/8)*256,256,256,x,y+24,256,256);
    sc.fillStyle=row?'#ddd':'#333';sc.font='14px sans-serif';sc.fillText('NEW / phase '+phase,x+16,y+22);
  }
  fs.writeFileSync(path.join(proof,'contact-sheet.png'),sheet.toBuffer('image/png'));
  for(let i=0;i<2;i++)fs.writeFileSync(path.join(proof,i?'new-atlas.png':'old-atlas.png'),atlases[i].toBuffer('image/png'));
  const reference=fs.readFileSync('C:/Users/tjdwo/Downloads/doro/1-2.jpg').toString('base64');
  const html=`<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>도로롱 · 대각선 네 발 걷기</title>
<style>*{box-sizing:border-box}body{margin:0;background:#f4f1ed;color:#302b2d;font:14px/1.6 system-ui}main{max-width:680px;padding:20px 16px}h1{font-size:23px;margin:0 0 8px}p{color:#796b73}.ref{float:right;width:84px;height:84px;margin-left:12px}.panel{border:1px solid #d6ccd0;border-radius:12px;overflow:hidden;margin:14px 0;background:white}.title{padding:8px 12px;font-weight:650;border-bottom:1px solid #ded5d9}canvas{width:100%;height:auto;display:block}.controls{display:flex;flex-wrap:wrap;align-items:center;gap:12px}button,select{font:inherit;padding:7px 12px;border:1px solid #c8b9c1;border-radius:7px;background:white}input{accent-color:#89536b}#phase{width:100%}.note{font-size:12px}#phaseLabel{font-variant-numeric:tabular-nums}</style>
<main><img class="ref" alt="사용자가 준 숨은 뒷발 참고 그림" src="data:image/jpeg;base64,${reference}"><h1>대각선 두 쌍으로 걷기.</h1><p>앞왼 + 뒤오 / 앞오 + 뒤왼<br>몸 뒤에서 살짝 보이는 네 번째 발 · 기존 아트 사용</p>
<div class="panel"><div class="title">기존 걷기 · 세 발</div><canvas id="old" width="512" height="256" aria-label="기존 세 발 걷기"></canvas></div>
<div class="panel"><div class="title">새 걷기 · 대각선 네 발</div><canvas id="new" width="512" height="256" aria-label="새 대각선 네 발 걷기"></canvas></div>
<div class="controls"><button id="pause">일시정지</button><label>재생 속도 <select id="speed"><option value="1">1×</option><option value=".5">0.5×</option><option value=".25">0.25×</option></select></label><label><input id="flip" type="checkbox">좌우 반전</label><label><input id="black" type="checkbox">검은 배경</label></div>
<input id="phase" aria-label="걷기 프레임" type="range" min="0" max="31" step="1" value="0"><output id="phaseLabel">0 / 31</output>
<p class="note">제품 미적용 비교본입니다. 앉기와 서 있는 모습은 기존 PNG 그대로 유지했습니다. 같은 크기·같은 프레임 수·같은 재생 속도로 비교합니다. 이 화면에서는 눈 깜빡임을 생략했습니다.</p><p id="status">이미지 불러오는 중…</p>
</main><script>
const sources=${JSON.stringify(atlases.map(a=>'data:image/png;base64,'+a.toBuffer('image/png').toString('base64')))};
const images=sources.map(src=>{const image=new Image;image.src=src;return image});
const get=id=>document.getElementById(id);let paused=false,time=0,last=0;
get('pause').onclick=()=>{paused=!paused;get('pause').textContent=paused?'계속 재생':'일시정지'};
get('phase').oninput=e=>{paused=true;get('pause').textContent='계속 재생';time=Number(e.target.value)/32*10/14};
Promise.all(images.map(im=>im.decode())).then(()=>{get('status').textContent='재생 준비 완료';requestAnimationFrame(tick)}).catch(e=>get('status').textContent='미리보기 오류: '+e.message);
function tick(now){if(last&&!paused)time+=Math.min(.05,(now-last)/1000)*Number(get('speed').value);last=now;const phase=Math.floor(time*14/10*32+1e-8)%32;get('phase').value=phase;get('phaseLabel').value=phase+' / 31';
for(let n=0;n<2;n++){const ctx=get(n?'new':'old').getContext('2d'),black=get('black').checked;ctx.fillStyle=black?'#151419':'#fcfaf7';ctx.fillRect(0,0,512,256);ctx.strokeStyle=black?'#49404b':'#ddcfd5';ctx.beginPath();ctx.moveTo(12,208.5);ctx.lineTo(500,208.5);ctx.stroke();const dir=get('flip').checked?-1:1;for(let x=12+((time*28*dir)%24);x<500;x+=24){ctx.beginPath();ctx.moveTo(x,209);ctx.lineTo(x-4,215);ctx.stroke()}ctx.save();ctx.translate(256,0);ctx.scale(dir,1);ctx.drawImage(images[n],phase%8*256,Math.floor(phase/8)*256,256,256,-128,0,256,256);ctx.restore()}requestAnimationFrame(tick)}
</script></html>`;
  fs.writeFileSync(path.join(proof,'index.html'),html);
  console.log(proof);
  if(process.argv.includes('--serve'))require('node:http').createServer((req,res)=>{
    if(req.url!=='/'){res.writeHead(404);res.end();return;}
    res.writeHead(200,{'Content-Type':'text/html; charset=utf-8','Cache-Control':'no-store'});res.end(html);
  }).listen(2787,'127.0.0.1',()=>console.log('Four-leg proof http://127.0.0.1:2787/'));
})().catch(e=>{console.error(e);process.exitCode=1});
