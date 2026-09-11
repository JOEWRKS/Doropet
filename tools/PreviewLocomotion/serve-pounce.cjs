// Reuse the approved hunt proof atlas; never regenerate or change product art.
const fs=require('node:fs'),path=require('node:path'),http=require('node:http');
const proof=path.resolve(__dirname,'../../artifacts/repro/hunt-preview-20260910');
const routes={'/':['pounce-preview.html','text/html; charset=utf-8']};
for(const name of ['pounce-preview.js','pounce-motion.js','hunt-motion.js','gaze-motion.js','gaze-eyes.js','gaze-compose.js'])routes['/'+name]=[name,'text/javascript; charset=utf-8'];
for(const name of ['body-atlas.png','head.png']){const file=path.join(proof,name);fs.accessSync(file);routes['/'+name]=[file,'image/png'];}
const port=Number(process.argv.find(x=>x.startsWith('--port='))?.slice(7)||2800);
if(!Number.isInteger(port)||port<1024||port>65535)throw Error('Invalid port');
http.createServer((req,res)=>{
 const route=routes[new URL(req.url,'http://localhost').pathname];
 if(!route){res.writeHead(404);res.end();return;}
 fs.readFile(path.resolve(__dirname,route[0]),(error,data)=>{res.writeHead(error?500:200,{'Content-Type':route[1],'Cache-Control':'no-store'});res.end(error?'Preview file unavailable':data);});
}).listen(port,'127.0.0.1',()=>console.log('Pounce preview http://127.0.0.1:'+port+'/'));
