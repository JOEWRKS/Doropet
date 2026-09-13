const http=require('node:http'),fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'../../artifacts/repro/cheek-spring-20260913-preview-06');
const routes={'/':[path.join(__dirname,'index.html'),'text/html; charset=utf-8'],'/preview.mjs':[path.join(__dirname,'preview.mjs'),'text/javascript'],'/motion.mjs':[path.join(__dirname,'motion.mjs'),'text/javascript'],'/old.png':[path.join(root,'old.png'),'image/png'],'/new.png':[path.join(root,'new.png'),'image/png']};
const server=http.createServer((req,res)=>{const entry=routes[new URL(req.url,'http://localhost').pathname];if(!entry){res.writeHead(404);return res.end();}fs.readFile(entry[0],(err,data)=>{if(err){res.writeHead(500);return res.end('Preview asset unavailable');}res.writeHead(200,{'Content-Type':entry[1],'Cache-Control':'no-store'});res.end(data);});});
server.listen(2801,'127.0.0.1',()=>console.log('Cheek spring preview http://127.0.0.1:2801/'));
