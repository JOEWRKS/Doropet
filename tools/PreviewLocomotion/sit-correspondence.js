(function(root){
  // Corresponding authored joints, clefts and sole/haunch landmarks. The
  // upper attachment and image boundary are pinned; no endpoint is redrawn.
  const pairs=[
    [[0,0],[0,0]],[[48,0],[48,0]],[[96,0],[96,0]],
    [[0,52],[0,52]],[[16,66],[16,66]],[[32,67],[32,67]],[[48,69],[48,69]],
    [[64,60],[64,60]],[[69,52],[69,52]],[[96,52],[96,52]],
    [[17,70],[17,70]],[[23,81],[26,87]],[[26,75],[32.5,77]],
    [[34,78],[35,83]],[[43,86.5],[41,87]],[[48,77],[47,77]],
    [[58,70],[58,72]],[[59,73],[52,77]],[[64,83],[59,87]],
    [[70,81],[70,85]],[[70,73],[75,80]],[[73,63],[74,70]],
    [[0,96],[0,96]],[[25,96],[25,96]],[[45,96],[45,96]],[[65,96],[65,96]],[[96,96],[96,96]],
    [[60,78],[49,84]]
  ];
  const cross=(a,b,c)=>(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]);
  function triangulate(points){
    const n=points.length,p=points.concat([[-500,-500],[600,-500],[50,700]]);let triangles=[[n,n+1,n+2]];
    for(let k=0;k<n;k++){
      const edges=new Map(),keep=[];
      for(const tri of triangles){
        const [a,b,c]=tri.map(i=>[p[i][0]-p[k][0],p[i][1]-p[k][1]]);
        const det=(a[0]*a[0]+a[1]*a[1])*(b[0]*c[1]-b[1]*c[0])-(b[0]*b[0]+b[1]*b[1])*(a[0]*c[1]-a[1]*c[0])+(c[0]*c[0]+c[1]*c[1])*(a[0]*b[1]-a[1]*b[0]);
        if(det*cross(...tri.map(i=>p[i]))<=1e-7){keep.push(tri);continue;}
        for(let j=0;j<3;j++){const e=[tri[j],tri[(j+1)%3]],key=e.slice().sort((a,b)=>a-b).join('/');if(edges.has(key))edges.delete(key);else edges.set(key,e);}
      }
      triangles=keep.concat(Array.from(edges.values(),e=>[...e,k]));
    }
    return triangles.filter(tri=>tri.every(i=>i<n));
  }
  const triangles=triangulate(pairs.map(([a,b])=>a.map((v,k)=>(v+b[k])/2)));
  function createMap(t,scale){
    const size=96*scale,result=new Float32Array(size*size*4);
    for(let y=0;y<size;y++)for(let x=0;x<size;x++){const i=(y*size+x)*4;result.set([(x+.5)/scale-.5,(y+.5)/scale-.5,(x+.5)/scale-.5,(y+.5)/scale-.5],i);}
    for(const tri of triangles){
      const from=tri.map(i=>pairs[i][0]),to=tri.map(i=>pairs[i][1]),q=from.map((a,i)=>a.map((v,k)=>v+(to[i][k]-v)*t));
      const den=cross(...q);
      if(cross(...from)*cross(...to)<=0)throw new Error('Folded sitting correspondence '+tri.join(','));
      const l=Math.max(0,Math.floor(Math.min(...q.map(p=>p[0]))*scale)),r=Math.min(size-1,Math.ceil(Math.max(...q.map(p=>p[0]))*scale));
      const top=Math.max(0,Math.floor(Math.min(...q.map(p=>p[1]))*scale)),bottom=Math.min(size-1,Math.ceil(Math.max(...q.map(p=>p[1]))*scale));
      for(let y=top;y<=bottom;y++)for(let x=l;x<=r;x++){
        const p=[(x+.5)/scale-.5,(y+.5)/scale-.5],u=cross(q[1],q[2],p)/den,v=cross(q[2],q[0],p)/den,w=1-u-v;
        if(Math.min(u,v,w)<-1e-5)continue;
        const i=(y*size+x)*4;for(let k=0;k<2;k++){result[i+k]=u*from[0][k]+v*from[1][k]+w*from[2][k];result[i+2+k]=u*to[0][k]+v*to[1][k]+w*to[2][k];}
      }
    }
    return result;
  }
  root.sitCorrespondence={createMap};
})(globalThis);
