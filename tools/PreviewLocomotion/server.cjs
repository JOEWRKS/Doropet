const http=require('node:http'),fs=require('node:fs'),path=require('node:path');
const files={
  '/':['index.html','text/html; charset=utf-8'],
  '/rig.js':['rig.js','text/javascript'], '/render.js':['render.js','text/javascript'],
  '/preview.js':['preview.js','text/javascript'],
  '/authored-sit.js':['authored-sit.js','text/javascript'],
  '/sit-correspondence.js':['sit-correspondence.js','text/javascript'],
  '/contour.js':['../GenerateLayeredPull/contour.js','text/javascript'],
  '/canonical.png':['../../src/Dororong.App/Assets/dororong-canonical.png','image/png'],
  '/closed.png':['../../src/Dororong.App/Assets/dororong-closed-eyes.png','image/png'],
  '/sleep.png':['../../src/Dororong.App/Assets/dororong-sleep.png','image/png'],
  '/seated-final.png':['assets/seated-final-10-2.png','image/png'],
  '/seated-bank.png':['assets/seated-final-10-2-bank.png','image/png']
};
// Keep textures in the delivered document: detached/captured previews must not
// resolve new root-relative Image requests against a different host.
const embeddedImages=[['sourceCanonical','/canonical.png'],['sourceClosed','/closed.png'],['sourceSleep','/sleep.png'],['sourceFinalSeated','/seated-final.png'],['sourceFinalBank','/seated-bank.png']]
  .map(([id,url])=>`<img id="${id}" hidden alt="" src="data:${files[url][1]};base64,${fs.readFileSync(path.resolve(__dirname,files[url][0])).toString('base64')}">`).join('');
const server=http.createServer((req,res)=>{
  const route=files[new URL(req.url,'http://localhost').pathname];
  if(!route){res.writeHead(404);res.end();return;}
  fs.readFile(path.resolve(__dirname,route[0]),(err,data)=>{
    if(err){res.writeHead(500);res.end('Unable to load preview');return;}
    if(route[0]==='index.html')data=data.toString('utf8').replace('<!-- preview-images -->',embeddedImages);
    res.writeHead(200,{'Content-Type':route[1],'Cache-Control':'no-store'});res.end(data);
  });
});
server.listen(2785,'127.0.0.1',()=>console.log('Locomotion preview http://127.0.0.1:2785/'));
