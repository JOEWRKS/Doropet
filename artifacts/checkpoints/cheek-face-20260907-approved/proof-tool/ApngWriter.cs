using System.Buffers.Binary;
using System.IO;
using System.Text;

internal static class ApngWriter
{
    internal static void Write(string path,string[] frames)
    {
        using var output=File.Create(path);output.Write(new byte[]{137,80,78,71,13,10,26,10});uint sequence=0;
        for(var n=0;n<frames.Length;n++)
        {
            var bytes=File.ReadAllBytes(frames[n]);var chunks=new List<(string Name,byte[] Data)>();
            for(var at=8;at<bytes.Length;){var size=(int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at,4));var name=Encoding.ASCII.GetString(bytes,at+4,4);chunks.Add((name,bytes.AsSpan(at+8,size).ToArray()));at+=size+12;}
            var header=chunks.Single(c=>c.Name=="IHDR").Data;
            if(header[8]!=8||header[9]!=6)throw new Exception("Expected8-bit RGBA PNG for lossless APNG");
            if(n==0){Chunk(output,"IHDR",header);var actl=new byte[8];U32(actl,0,(uint)frames.Length);Chunk(output,"acTL",actl);}
            var control=new byte[26];U32(control,0,sequence++);Array.Copy(header,0,control,4,8);
            BinaryPrimitives.WriteUInt16BigEndian(control.AsSpan(20,2),(ushort)(n==frames.Length-1?300:20));BinaryPrimitives.WriteUInt16BigEndian(control.AsSpan(22,2),1000);
            // Full frame, SOURCE blend, no disposal. Transparent pixels replace too.
            Chunk(output,"fcTL",control);
            foreach(var chunk in chunks.Where(c=>c.Name=="IDAT"))
            {if(n==0)Chunk(output,"IDAT",chunk.Data);else{var data=new byte[chunk.Data.Length+4];U32(data,0,sequence++);Array.Copy(chunk.Data,0,data,4,chunk.Data.Length);Chunk(output,"fdAT",data);}}
        }
        Chunk(output,"IEND",[]);
    }
    static void U32(byte[] data,int offset,uint value)=>BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset,4),value);
    static void Chunk(Stream stream,string type,byte[] data)
    {
        var name=Encoding.ASCII.GetBytes(type);var size=new byte[4];U32(size,0,(uint)data.Length);stream.Write(size);stream.Write(name);stream.Write(data);
        uint crc=0xffffffff;foreach(var b in name.Concat(data)){crc^=b;for(var j=0;j<8;j++)crc=(crc&1)!=0?(crc>>1)^0xedb88320:crc>>1;}U32(size,0,crc^0xffffffff);stream.Write(size);
    }
}
