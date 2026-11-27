// XLSX OOXML mínimo sin dependencias. ZIP sin compresión, cadenas inline (no fórmulas).
const enc=new TextEncoder();
const xml=s=>String(s??"").replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F]/g,"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;").replace(/'/g,"&apos;");
function crc32(bytes){let c=0xffffffff;for(const b of bytes){c^=b;for(let i=0;i<8;i++)c=(c>>>1)^((c&1)?0xedb88320:0);}return (c^0xffffffff)>>>0;}
function zip(archivos){
 const partes=[],central=[];let pos=0,tamCentral=0;
 for(const [nombre,texto] of Object.entries(archivos)){
  const n=enc.encode(nombre),d=enc.encode(texto),crc=crc32(d);
  const h=new Uint8Array(30+n.length),v=new DataView(h.buffer);
  v.setUint32(0,0x04034b50,true);v.setUint16(4,20,true);v.setUint16(12,33,true);v.setUint32(14,crc,true);v.setUint32(18,d.length,true);v.setUint32(22,d.length,true);v.setUint16(26,n.length,true);h.set(n,30);
  const z=new Uint8Array(46+n.length),w=new DataView(z.buffer);
  w.setUint32(0,0x02014b50,true);w.setUint16(4,20,true);w.setUint16(6,20,true);w.setUint16(14,33,true);w.setUint32(16,crc,true);w.setUint32(20,d.length,true);w.setUint32(24,d.length,true);w.setUint16(28,n.length,true);w.setUint32(42,pos,true);z.set(n,46);
  partes.push(h,d);central.push(z);pos+=h.length+d.length;tamCentral+=z.length;
 }
 const e=new Uint8Array(22),v=new DataView(e.buffer);v.setUint32(0,0x06054b50,true);v.setUint16(8,central.length,true);v.setUint16(10,central.length,true);v.setUint32(12,tamCentral,true);v.setUint32(16,pos,true);
 return new Blob([...partes,...central,e],{type:"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"});
}
const ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
const rel="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
const pkg="http://schemas.openxmlformats.org/package/2006/relationships";
const decl='<?xml version="1.0" encoding="UTF-8" standalone="yes"?>';
export function crearExcel(hojas){
 const archivos={
 "[Content_Types].xml":decl+'<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>'+hojas.map((_,i)=>'<Override PartName="/xl/worksheets/sheet'+(i+1)+'.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>').join("")+'</Types>',
 "_rels/.rels":decl+'<Relationships xmlns="'+pkg+'"><Relationship Id="rId1" Type="'+rel+'/officeDocument" Target="xl/workbook.xml"/></Relationships>',
 "xl/workbook.xml":decl+'<workbook xmlns="'+ns+'" xmlns:r="'+rel+'"><sheets>'+hojas.map((h,i)=>'<sheet name="'+xml(h.nombre.slice(0,31))+'" sheetId="'+(i+1)+'" r:id="rId'+(i+1)+'"/>').join("")+'</sheets></workbook>',
 "xl/_rels/workbook.xml.rels":decl+'<Relationships xmlns="'+pkg+'">'+hojas.map((_,i)=>'<Relationship Id="rId'+(i+1)+'" Type="'+rel+'/worksheet" Target="worksheets/sheet'+(i+1)+'.xml"/>').join("")+'</Relationships>'
 };
 hojas.forEach((h,i)=>{
  archivos["xl/worksheets/sheet"+(i+1)+".xml"]=decl+'<worksheet xmlns="'+ns+'"><sheetData>'+h.filas.map((fila,j)=>'<row r="'+(j+1)+'">'+fila.map(v=>typeof v==="number"&&Number.isFinite(v)?'<c><v>'+v+'</v></c>':'<c t="inlineStr"><is><t xml:space="preserve">'+xml(v)+'</t></is></c>').join("")+'</row>').join("")+'</sheetData></worksheet>';
 });
 return zip(archivos);
}
export function descargarExcel(hojas,nombre){
 const url=URL.createObjectURL(crearExcel(hojas));const a=document.createElement("a");a.href=url;a.download=nombre;document.body.append(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),1000);
}
