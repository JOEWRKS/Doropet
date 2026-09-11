const fs=require('node:fs'),path=require('node:path');
const {current}=require('./verify-seated-contour-proof.cjs');
const {render}=require('./verify-seated-vector.cjs');
const {createSvg}=require('./seated-vector.cjs');
const canvas=require(path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));
async function main(){
  const out=path.resolve(__dirname,'../../artifacts/repro/seated-vector-20260910');fs.mkdirSync(out,{recursive:true});
  const before=current(),svg=createSvg(before);
  fs.writeFileSync(path.join(out,'candidate.svg'),svg);
  const source=before.toBuffer('image/png').toString('base64');
  const sheet=canvas.createCanvas(720,540),ctx=sheet.getContext('2d');
  for(const [row,bg]of ['#101010','#faf9f7'].entries()){
    ctx.fillStyle=bg;ctx.fillRect(0,row*270,720,270);
    for(let col=0;col<2;col++){
      const x=col*360,y=row*270;
      ctx.fillStyle=row?'#222':'#ddd';ctx.font='14px sans-serif';ctx.fillText(col?'VECTOR / DISPLAY RESOLUTION':'CURRENT PRODUCT',x+20,y+25);
      const small=col?await render(svg,96):before;
      ctx.drawImage(small,x+130,y+24,96,96);
      ctx.imageSmoothingEnabled=true;ctx.imageSmoothingQuality='high';
      const large=col?await render(svg,384):before,s=col?4:1;
      ctx.drawImage(large,16*s,58*s,64*s,34*s,x+45,y+128,256,136);
    }
  }
  fs.writeFileSync(path.join(out,'comparison.png'),sheet.toBuffer('image/png'));
  const html=`<!doctype html><html lang="ko"><meta charset="utf-8"><title>앉은 몸선 · 벡터 비교</title>
<style>*{box-sizing:border-box}body{margin:0;background:#151515;color:#eee;font:15px system-ui}main{max-width:1000px;margin:32px auto;padding:0 24px}h1{font-size:24px}p{color:#aaa;line-height:1.7}.controls{display:flex;gap:12px;align-items:center;margin:24px 0}button,select{font:inherit;padding:8px 14px;border-radius:7px;border:1px solid #555;background:#252525;color:#fff}.grid{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);border:1px solid #444;border-radius:12px;overflow:hidden}.panel{min-height:430px;min-width:0;overflow:auto;display:flex;flex-direction:column;align-items:center;background:var(--bg,#080808)}h2{font-size:14px;color:#999;margin:24px 12px}.sprite{width:192px;height:192px;flex-shrink:0;margin:auto}.foot{font-size:13px}@media(max-width:620px){.grid{grid-template-columns:minmax(0,1fr)}.panel{min-height:280px}.panel+.panel{border-top:1px solid #444}}</style>
<main><h1>앞다리 틈을 보존한 곡선 몸선.</h1><p>왼쪽은 현재 제품, 오른쪽은 벡터 몸선 비교본입니다. 얼굴·리본은 원본, 몸 외곽과 다리 구분선은 곡선입니다.<br>제품 미적용 · 정지 자세 비교 · 실제 확대 렌더링</p>
<div class="controls"><label>크기 <select id="size"><option value="96">96px</option><option value="144">144px</option><option value="192" selected>192px</option><option value="384">384px</option></select></label><button id="bg">흰 배경</button></div>
<div class="grid"><section class="panel"><h2>현재 제품 · 96px 비트맵</h2><img class="sprite" alt="현재 앉은 모습" src="data:image/png;base64,${source}"></section><section class="panel"><h2>새 비교본 · 벡터 몸선</h2><img class="sprite" alt="곡선으로 렌더링한 앉은 모습" src="data:image/svg+xml;base64,${Buffer.from(svg).toString('base64')}"></section></div>
<p class="foot">앞다리 사이 / 엉덩이 곡선 / 머리와 몸 연결부를 비교해 주세요. 눈 깜빡임·앉고 일어나는 전환은 이번 비교에 포함하지 않았습니다.</p>
</main><script>document.querySelector('#size').onchange=e=>document.querySelectorAll('.sprite').forEach(el=>el.style.width=el.style.height=e.target.value+'px');let light=false;document.querySelector('#bg').onclick=e=>{light=!light;document.documentElement.style.setProperty('--bg',light?'#faf9f7':'#080808');e.target.textContent=light?'검은 배경':'흰 배경'}</script></html>`;
  fs.writeFileSync(path.join(out,'index.html'),html);
  console.log(out);
  if(process.argv.includes('--serve')) {
    require('node:http').createServer((req,res)=>{
      if(req.url!=='/'){res.writeHead(404);res.end();return;}
      res.writeHead(200,{'Content-Type':'text/html; charset=utf-8','Cache-Control':'no-store'});res.end(html);
    }).listen(2786,'127.0.0.1',()=>console.log('Seated vector proof http://127.0.0.1:2786/'));
  }
}
if(require.main===module)main().catch(e=>{console.error(e);process.exitCode=1});
