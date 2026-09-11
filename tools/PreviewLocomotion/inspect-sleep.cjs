const fs=require('node:fs'),path=require('node:path');
require('./raster-harness.cjs').renderer().then(async({canvas,renderer,rig})=>{
  const sheet=canvas.createCanvas(1152,620),ctx=sheet.getContext('2d');
  ctx.fillStyle='#eeeae5';ctx.fillRect(0,0,1152,620);
  for(const [i,file] of ['dororong-canonical.png','dororong-sleep.png'].entries()){
    const image=await canvas.loadImage(path.resolve(__dirname,'../../src/Dororong.App/Assets',file));
    ctx.imageSmoothingEnabled=false;ctx.drawImage(image,i*576,30,576,576);
    ctx.font='12px sans-serif';ctx.fillStyle='#555';ctx.fillText(file,i*576+10,16);
    for(let n=40;n<=90;n+=5){ctx.strokeStyle='#7775';ctx.beginPath();ctx.moveTo(i*576+n*6,30+40*6);ctx.lineTo(i*576+n*6,606);ctx.stroke();ctx.fillText(n,i*576+n*6,30+40*6);ctx.beginPath();ctx.moveTo(i*576+40*6,30+n*6);ctx.lineTo(i*576+570,30+n*6);ctx.stroke();ctx.fillText(n,i*576+555,30+n*6);}
  }
  fs.writeFileSync(path.resolve(__dirname,'../../artifacts/repro/locomotion/sleep-source-grid.png'),sheet.toBuffer('image/png'));
  const comparison=canvas.createCanvas(720,330),c=comparison.getContext('2d');
  c.fillStyle='#fcfaf7';c.fillRect(0,0,720,330);c.fillStyle='#55444b';c.font='16px sans-serif';
  c.fillText('Sleeping source',30,30);c.fillText('Same rear + standing front',385,30);
  c.drawImage(await canvas.loadImage(path.resolve(__dirname,'../../src/Dororong.App/Assets/dororong-sleep.png')),30,40,288,288);
  c.drawImage(renderer.render(rig.pose({sit:1})),342,-17,384,384);
  fs.writeFileSync(path.resolve(__dirname,'../../artifacts/repro/locomotion/sleep-source-comparison.png'),comparison.toBuffer('image/png'));
});
