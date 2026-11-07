"""Original deterministic pixel-art production. Pillow only; never writes existing assets/code.
Run from this repository. Art is painted from geometric/material primitives, not recolored Castle art.
Existing character/Castle PNGs are read solely for clearly labeled offline comparison mockups.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageOps, ImageFont
import random, math, json, hashlib, uuid

ROOT = Path(__file__).resolve().parents[3]
DOC = ROOT/'Docs/Art/ArakusRoomConcept'
ART = ROOT/'Assets/StreamingAssets/Art/ArakusRoom'
W,H,GROUND = 768,512,366
PPU = 360/16.4
SEEDS = {'StoneFortress':73091, 'IronWarworks':73163}
P = {
 'void':(11,12,17), 'mortar':(16,15,20), 'stone':(37,29,31),
 'stone_dark':(24,23,28), 'stone_light':(58,44,43), 'edge':(77,60,55),
 'iron':(25,29,34), 'iron_edge':(68,71,75), 'iron_dark':(13,17,22),
 'rust':(75,41,33), 'crimson':(90,30,33), 'ember':(155,55,43),
 'floor':(57,57,60), 'floor_edge':(103,100,94), 'dust':(75,65,58),
}

def pixels(im): return im.get_flattened_data() if hasattr(im,'get_flattened_data') else im.getdata()

def shift(c, value): return tuple(max(0,min(255,v+value)) for v in c[:3])
def pixel_noise(x,y,seed):
    n=(x*374761393+y*668265263+seed*69069)&0xffffffff
    n=((n^(n>>13))*1274126177)&0xffffffff
    return (n^(n>>16))&255

def poly(im,pts,color): ImageDraw.Draw(im).polygon([(int(x),int(y)) for x,y in pts],fill=color)
def stroke(im,pts,color,width=1): ImageDraw.Draw(im).line([(int(x),int(y)) for x,y in pts],fill=color,width=width)

def stone(im,rect,rng,base,detail=1):
    x,y,x1,y1=map(int,rect); d=ImageDraw.Draw(im)
    if x1-x<10 or y1-y<7:return
    cut=rng.randint(1,3)
    pts=[(x+cut,y),(x1-2,y),(x1,y+2),(x1,y1-cut),(x1-cut,y1),(x+1,y1),(x,y1-3),(x,y+2)]
    poly(im,pts,base)
    stroke(im,[(x+cut,y+1),(x1-2,y+1)],shift(base,10))
    stroke(im,[(x+1,y+3),(x+1,y1-3)],shift(base,5))
    stroke(im,[(x+2,y1-1),(x1-cut,y1-1),(x1-1,y1-cut)],shift(base,-10),2)
    # Broad chipped face planes and granular mineral clusters establish actual material depth.
    poly(im,[(x+3,y+3),(x1-5,y+3),(x1-8,y+max(4,(y1-y)//3)),
             (x+max(4,(x1-x)//3),y+max(4,(y1-y)//2))],shift(base,4))
    for _ in range(max(1,int((x1-x)*(y1-y)/240))):
        px=rng.randint(x+3,x1-3);py=rng.randint(y+3,y1-3)
        d.polygon([(px,py),(min(px+5,x1-2),py-1),(min(px+7,x1-2),min(py+2,y1-2)),(px+1,min(py+3,y1-2))],fill=shift(base,-5))
    # Deliberately clustered pitting and short mineral seams, never antialiased noise.
    for i in range(int((x1-x)*(y1-y)/65)*detail):
        px=rng.randint(x+3,x1-3);py=rng.randint(y+3,y1-3)
        c=shift(base,rng.choice([-8,-5,4,7]))
        d.line((px,py,min(px+rng.randint(1,4),x1-2),py),fill=c)
        if i%3==0:d.point((px,py-1),fill=shift(c,3))
    if rng.random()<.28:
        cx=rng.randint(x+5,x1-4);cy=y+2
        crack=[(cx,cy),(cx-2,cy+4),(cx+1,cy+7),(cx-2,min(y1-3,cy+12))]
        stroke(im,crack,shift(base,-15));stroke(im,[(px+1,py) for px,py in crack],shift(base,5))

def masonry(im,seed,base,zone=(0,0,W,H),large=False):
    rng=random.Random(seed);x0,y0,xmax,ymax=zone
    d=ImageDraw.Draw(im);d.rectangle(zone,fill=shift(base,-14))
    y=y0
    while y<ymax:
        rowh=rng.randint(24,37) if large else rng.randint(15,25)
        x=x0-rng.randint(10,40)
        while x<xmax:
            wid=rng.randint(43,76) if large else rng.randint(30,61)
            # Central fighting silhouettes get quiet, darker wall values.
            quiet=230<x<535 and 255<y<GROUND
            c=shift(base,rng.randint(-5,5)-(9 if quiet else 0))
            stone(im,(x+1,y+1,min(x+wid-1,xmax),min(y+rowh-1,ymax)),rng,c)
            x+=wid
        y+=rowh

def rivet(im,x,y,small=False):
    d=ImageDraw.Draw(im);r=1 if small else 2
    d.ellipse((x-r,y-r,x+r,y+r),fill=(12,15,19))
    d.point((x-1,y-1),fill=(84,80,75));d.point((x,y),fill=(56,57,60))
    if not small:d.point((x+1,y+1),fill=(20,23,27))

def beam(im,a,b,width=13,seed=0):
    dx,dy=b[0]-a[0],b[1]-a[1];length=max(1,math.hypot(dx,dy));nx,ny=-dy/length*width/2,dx/length*width/2
    poly(im,[(a[0]+nx,a[1]+ny),(b[0]+nx,b[1]+ny),(b[0]-nx,b[1]-ny),(a[0]-nx,a[1]-ny)],P['iron_dark'])
    stroke(im,[(a[0]+nx*.66,a[1]+ny*.66),(b[0]+nx*.66,b[1]+ny*.66)],P['iron_edge'],2)
    stroke(im,[a,b],P['iron'],max(1,width-5))
    stroke(im,[(a[0]-nx*.7,a[1]-ny*.7),(b[0]-nx*.7,b[1]-ny*.7)],(10,13,18),2)
    count=max(2,int(length/31))
    for i in range(count+1):
        t=i/count;rivet(im,round(a[0]+dx*t),round(a[1]+dy*t),True)

def chain(im,x,y,length,sway=0,heavy=False):
    d=ImageDraw.Draw(im)
    for i in range(length//8):
        xx=round(x+math.sin(i*.22)*sway);yy=y+i*8
        if i%2==0:
            d.ellipse((xx-3,yy,xx+3,yy+10),fill=(13,16,20),outline=(55,53,54),width=1)
            d.arc((xx-3,yy,xx+3,yy+10),190,290,fill=(88,76,69),width=1)
        else:
            d.rectangle((xx-1,yy,xx+1,yy+9),fill=(21,24,29));d.line((xx-1,yy,xx-1,yy+8),fill=(73,66,63))
    yy=y+length
    if heavy:
        d.ellipse((x-8,yy-3,x+8,yy+15),outline=(65,59,57),width=3)
        d.ellipse((x-6,yy-2,x+7,yy+13),outline=(21,24,29),width=2)

def torch(im,x,y,seed):
    # Low-amplitude ordered pixel halo; no Gaussian blur or neon gradient.
    pix=im.load()
    for yy in range(max(0,y-40),min(im.height,y+22)):
        for xx in range(max(0,x-34),min(im.width,x+35)):
            r=((xx-x)/34)**2+((yy-y+8)/40)**2
            if r<1:
                band=int((1-r)*6)
                if (xx+yy)%4<2 and band:
                    old=pix[xx,yy];alpha=old[3] if len(old)==4 else None
                    c=(min(94,old[0]+band*3),old[1],old[2])
                    pix[xx,yy]=c+(alpha,) if alpha is not None else c
    d=ImageDraw.Draw(im)
    d.polygon([(x-7,y+15),(x+7,y+15),(x+4,y+24),(x-4,y+24)],fill=(31,29,30))
    d.line((x-7,y+14,x+7,y+14),fill=(81,65,56),width=2)
    d.line((x,y+22,x,y+35),fill=(16,18,23),width=4)
    d.line((x-4,y+34,x+5,y+34),fill=(72,58,52),width=2)
    d.polygon([(x-4,y+14),(x-6,y+4),(x-2,y-5),(x-1,y+1),(x+3,y-13),(x+5,y-3),(x+3,y+5),(x+6,y+9),(x+4,y+14)],fill=(115,39,35))
    d.polygon([(x-2,y+13),(x-3,y+7),(x+1,y-4),(x+2,y+4),(x+4,y+10),(x+2,y+13)],fill=(172,69,44))
    d.line((x,y+11,x+1,y+5),fill=(208,117,64),width=1)

def gate(im,variant,seed):
    rng=random.Random(seed);d=ImageDraw.Draw(im)
    # A stepped military lintel, not a pointed ecclesiastical arch.
    opening=[(274,348),(274,146),(287,127),(308,117),(460,117),(481,127),(494,146),(494,348)]
    poly(im,[(x+( -12 if x<384 else 12),y-14) for x,y in opening],(14,15,20))
    poly(im,opening,(10,12,17))
    for side in [-1,1]:
        x=260 if side<0 else 495
        for j in range(7):stone(im,(x,139+j*29,x+12,166+j*29),rng,(49,36,35) if variant=='StoneFortress' else (35,35,40))
    for i in range(6):
        x=274+i*37
        stone(im,(x,102,x+35,126),rng,(56,39,38) if variant=='StoneFortress' else (37,38,43))
    # Recessed doors, narrow gaps, central locking straps and forged irregular hinges.
    for i in range(12):
        x=280+i*17
        poly(im,[(x,145),(x+14,140),(x+14,347),(x,347)],(22+i%3*2,21+i%2,26+i%3))
        d.line((x+1,147,x+1,346),fill=(32,29,33));d.line((x+13,145,x+13,346),fill=(11,14,19))
    for yy in [168,226,302]:
        for x0,x1 in [(283,379),(389,484)]:
            poly(im,[(x0,yy),(x1,yy-2),(x1,yy+9),(x0,yy+10)],(30,31,36))
            d.line((x0,yy,x1,yy-2),fill=(53,49,50))
            for xx in range(x0+8,x1,19):rivet(im,xx,yy+4,True)
    beam(im,(384,131),(384,348),7)
    d.rectangle((368,240,399,263),fill=(14,17,22));d.rectangle((371,243,396,260),outline=(49,45,45),width=2)
    for xx in [364,404]:
        d.ellipse((xx-5,251,xx+5,262),outline=(74,60,53),width=2)
    # Pale dust and a broken threshold make the door read as deep, massive and used.
    poly(im,[(262,350),(505,350),(530,365),(239,365)],(26,25,29))
    stroke(im,[(262,350),(505,350)],(64,48,44),2)

def scars(im,seed,variant):
    rng=random.Random(seed);d=ImageDraw.Draw(im)
    for i in range(15):
        x=rng.choice([rng.randint(150,243),rng.randint(530,620)]);y=rng.randint(164,322)
        # Axe/sword gouges with stone chips and lit lower lip.
        points=[(x,y),(x+3,y+5),(x+1,y+10),(x+5,y+17)]
        stroke(im,points,(12,14,18),2);stroke(im,[(a+2,b) for a,b in points],(54,40,38))
        for j in range(3):d.point((x+rng.randint(-5,5),y+rng.randint(0,18)),fill=(58,42,39))
    # Impact crater, hand-shaped silhouette of cracked radial stone rather than uniform blocks.
    for cx,cy in [(207,182),(564,262)]:
        poly(im,[(cx-9,cy-5),(cx-4,cy-11),(cx+6,cy-7),(cx+10,cy+3),(cx+1,cy+11),(cx-8,cy+6)],(16,17,22))
        stroke(im,[(cx-7,cy+6),(cx+1,cy+11),(cx+10,cy+3)],(56,39,37))
        for ang in [0.3,1.6,2.8,4.5]:
            dx,dy=math.cos(ang),math.sin(ang)
            stroke(im,[(cx+dx*8,cy+dy*8),(cx+dx*15-2,cy+dy*15),(cx+dx*22,cy+dy*22)],(15,16,21))

def far(variant):
    seed=SEEDS[variant];rng=random.Random(seed)
    im=Image.new('RGB',(W,H),P['void'])
    base=(37,27,30) if variant=='StoneFortress' else (29,28,34)
    masonry(im,seed,base,zone=(0,0,W,GROUND),large=True)
    # Upper shadowed vault: layered corbeling and weight-bearing, damaged ledges.
    for yy in [21,50,77]:
        poly(im,[(0,yy),(W,yy),(W,yy+12),(0,yy+12)],(22,22,27))
        stroke(im,[(0,yy+1),(W,yy+1)],(48,34,35));stroke(im,[(0,yy+11),(W,yy+11)],(10,13,18),2)
    gate(im,variant,seed+4)
    if variant=='StoneFortress':
        for cx in [111,657]:
            poly(im,[(cx-36,80),(cx+30,80),(cx+23,325),(cx+40,366),(cx-45,366),(cx-25,321)],(14,15,20))
            for row in range(10):
                yy=81+row*28
                stone(im,(cx-24,yy,cx+20,yy+26),rng,(53,38,37))
                poly(im,[(cx+21,yy),(cx+30,yy+3),(cx+30,yy+25),(cx+21,yy+26)],(25,23,28))
            for yy in [127,242,327]:beam(im,(cx-28,yy),(cx+27,yy),9)
        torch(im,190,210,seed);torch(im,578,210,seed+1)
        chain(im,219,104,79,1,True);chain(im,549,104,79,-1,True)
    else:
        # Warworks: structural iron gantry, diagonal cross-bracing and a winch above the gate.
        for x in [116,652]:
            beam(im,(x,79),(x,365),23)
            for yy in [128,240,334]:
                poly(im,[(x-16,yy-12),(x+16,yy-12),(x+16,yy+12),(x-16,yy+12)],(30,33,37))
                for dx in [-10,10]:rivet(im,x+dx,yy-6);rivet(im,x+dx,yy+6)
        beam(im,(98,91),(671,91),23)
        for aa,bb in [((116,108),(255,198)),((652,108),(513,198)),((116,289),(225,348)),((652,289),(543,348))]:beam(im,aa,bb,14)
        for x in range(150,641,38):
            poly(im,[(x-8,72),(x+8,72),(x+8,110),(x-8,110)],(23,27,33));rivet(im,x,79);rivet(im,x,102)
        for cx in [225,543]:
            poly(im,[(cx-12,350),(cx+12,350),(cx+15,365),(cx-15,365)],(27,28,33))
            stroke(im,[(cx-12,350),(cx+12,350)],(55,52,53))
        d=ImageDraw.Draw(im)
        d.ellipse((365,61,402,98),fill=(14,18,23),outline=(61,57,57),width=3)
        d.ellipse((373,69,394,90),outline=(40,42,47),width=3)
        chain(im,376,95,56,0);chain(im,392,95,56,0)
        torch(im,202,208,seed);torch(im,566,208,seed+1)
        for side in [0,1]:
            x0=153 if side==0 else 567
            for yy in range(258,344,19):
                poly(im,[(x0,yy),(x0+43,yy-5),(x0+45,yy+14),(x0+2,yy+18)],(33,31,36))
                stroke(im,[(x0+1,yy),(x0+42,yy-4)],(56,44,43));rivet(im,x0+7,yy+5,True)
    scars(im,seed+53,variant)
    # Below-ground foundation extension prevents black reveal in the real 4:3 camera view.
    masonry(im,seed+89,(29,28,32),zone=(0,GROUND,W,H),large=True)
    for yy in [GROUND+44,GROUND+87]:
        stroke(im,[(0,yy),(W,yy)],(13,16,21),3)
    return im.convert('RGBA')

def mid(variant):
    rng=random.Random(SEEDS[variant]+20);im=Image.new('RGBA',(W,H));d=ImageDraw.Draw(im)
    for side in [0,1]:
        cx=63 if side==0 else 705
        # Massive near supports with lit bevel, chipped edges and a dark outer side.
        poly(im,[(cx-31,11),(cx+29,11),(cx+22,322),(cx+39,GROUND),(cx-43,GROUND),(cx-25,319)],(17,19,25,255))
        for yy in range(35,344,33):
            stone(im,(cx-21,yy,cx+18,yy+31),rng,(48,36,36) if variant=='StoneFortress' else (36,37,43))
        stroke(im,[(cx-22,36),(cx-22,320)],(67,45,43),2)
        for yy in [77,190,307]:beam(im,(cx-28,yy),(cx+24,yy),12)
        # Recessed iron hanging hardware stays outside the central combat corridor.
        x=151 if side==0 else 617
        poly(im,[(x-9,94),(x+10,94),(x+10,109),(x-9,109)],(24,27,33))
        rivet(im,x-5,100);rivet(im,x+5,100)
        chain(im,x,106,122,2 if side==0 else -2,True)
        # Rubble at frame edges, with distinctive irregular stone and chipped faces.
        for j in range(12):
            xx=rng.randint(cx-41,cx+45);yy=GROUND-rng.randint(1,10);ww=rng.randint(5,18);hh=rng.randint(4,12)
            poly(im,[(xx,yy),(xx+ww,yy),(xx+ww-2,yy-hh+2),(xx+3,yy-hh)],(46,38,39))
            stroke(im,[(xx+3,yy-hh),(xx+ww-2,yy-hh+2)],(81,62,54))
    if variant=='IronWarworks':
        beam(im,(52,28),(716,28),17)
        for x in [194,574]:
            beam(im,(x,30),(x+(-40 if x<384 else 40),75),10)
            chain(im,x,36,38,0)
    return im

def floor(variant):
    rng=random.Random(SEEDS[variant]+40);im=Image.new('RGBA',(128,48),(35,36,41,255));d=ImageDraw.Draw(im)
    # Pixel-strata and bevel define an exact walk line at top row, without red hazard-like accents.
    shades=[(64,65,69),(58,59,63),(50,52,57),(45,47,52),(41,43,48),(39,40,45)]
    for y in range(48):
        base=shades[min(y,5)] if y<6 else (48-y//5,49-y//6,53-y//7)
        d.line((0,y,127,y),fill=base+(255,))
    for x0,x1 in [(7,42),(46,92),(97,121)]:
        poly(im,[(x0,5),(x1,5),(x1-2,24),(x0+2,25)],(54,53,55))
        stroke(im,[(x0,6),(x1,6)],(74,70,67));stroke(im,[(x0+2,24),(x1-2,24)],(24,28,34))
    for x in [43,94]:
        stroke(im,[(x,1),(x+1,11),(x-2,19),(x+1,29)],(24,28,34),2)
        stroke(im,[(x+2,6),(x+3,12)],(92,84,75))
    for i in range(125):
        x=rng.randint(3,124);y=rng.randint(7,45)
        c=(69,64,61) if i%5==0 else (27,30,36)
        d.line((x,y,min(125,x+rng.randint(1,3)),y),fill=c)
    if variant=='IronWarworks':
        for x in [27,104]:
            poly(im,[(x-3,3),(x+3,3),(x+6,45),(x-2,45)],(27,32,39))
            stroke(im,[(x-3,4),(x-2,43)],(78,78,78))
            for y in [8,33]:rivet(im,x+1,y,True)
    # Periodic material edge. No alpha gap or color jump at the repeated join.
    for y in range(48):
        c=im.getpixel((0,y));im.putpixel((127,y),c)
    return im

def pillar(variant,broken=False):
    seed=SEEDS[variant]+(101 if broken else 99);rng=random.Random(seed)
    im=Image.new('RGBA',(192,192));d=ImageDraw.Draw(im)
    base=(68,54,50) if variant=='StoneFortress' else (59,57,60)
    if not broken:
        poly(im,[(76,42),(115,40),(123,49),(119,147),(127,157),(125,159),(66,159),(65,153),(73,145),(70,50)],(23,25,30))
        # Separate face planes, battered chamfers, coursed blocks, not a decorative classical column.
        poly(im,[(76,48),(108,46),(110,147),(74,148)],base)
        poly(im,[(109,46),(119,50),(117,147),(110,147)],shift(base,-20))
        stroke(im,[(76,49),(75,145)],shift(base,23),2)
        for yy in [50,79,107,133]:
            stone(im,(77,yy,108,min(yy+26,148)),rng,shift(base,rng.randint(-3,4)))
        for yy in ([65,118] if variant=='StoneFortress' else [58,91,125]):
            poly(im,[(70,yy),(119,yy-1),(121,yy+8),(70,yy+9)],(24,29,36))
            stroke(im,[(71,yy),(118,yy-1)],(94,84,73),2)
            stroke(im,[(72,yy+8),(120,yy+7)],(11,17,23),2)
            for xx in [78,95,112]:rivet(im,xx,yy+4)
        for yy in range(51,144,3):
            for xx in range(78,108,3):
                n=pixel_noise(xx//2,yy//2,seed)
                if n<45:d.line((xx,yy,min(108,xx+2),yy),fill=shift(base,-9 if n<20 else 8))
        for xx,yy in [(82,55),(106,83),(80,108),(104,135)]:
            poly(im,[(xx,yy),(xx+5,yy-1),(xx+3,yy+3),(xx,yy+2)],shift(base,-17))
            d.line((xx,yy-1,xx+4,yy-2),fill=shift(base,25))
        stroke(im,[(96,78),(92,85),(98,93),(95,101),(102,109)],(26,27,32))
        stroke(im,[(97,79),(93,85),(99,93)],(106,83,66))
        poly(im,[(75,145),(116,145),(125,153),(125,159),(66,159),(67,153)],(57,49,47))
        stroke(im,[(75,145),(116,145),(125,153)],(105,85,69))
        stroke(im,[(68,157),(123,157)],(27,29,34),2)
        poly(im,[(73,42),(111,40),(119,45),(109,49),(70,50)],(84,67,58))
        stroke(im,[(74,42),(110,41)],(122,93,72))
    else:
        # Same x pivot and y=160 foot line. Broken strap and rubble retain recognizable materials.
        poly(im,[(75,117),(82,111),(88,120),(96,112),(104,122),(113,116),(119,145),(126,159),(65,159),(73,144)],base)
        poly(im,[(75,117),(82,111),(88,120),(96,112),(104,122),(113,116),(111,127),(77,127)],(100,79,64))
        poly(im,[(111,122),(119,129),(119,147),(111,146)],shift(base,-20))
        for yy in [133,145]:stroke(im,[(76,yy),(113,yy)],shift(base,-21))
        # Bent iron, not stretched or floating full-height column art.
        stroke(im,[(71,127),(94,126),(104,119),(114,123),(122,121)],(24,29,36),6)
        stroke(im,[(71,124),(94,123),(103,116),(114,120),(122,118)],(100,85,71),2)
        for xx in [80,94,113]:rivet(im,xx,126)
        for xx,yy,ww,hh in [(66,159,17,12),(114,159,18,10),(82,159,20,8),(108,103,12,9),(77,96,10,8),(100,86,8,6)]:
            poly(im,[(xx,yy),(xx+ww,yy),(xx+ww-2,yy-hh+3),(xx+4,yy-hh)],shift(base,-rng.randint(0,15)))
            stroke(im,[(xx+4,yy-hh),(xx+ww-2,yy-hh+3)],shift(base,30))
        stroke(im,[(68,159),(128,159)],(32,33,38))
    return im

def new_meta(path,folder=False):
    meta=Path(str(path)+'.meta')
    # Existing files are never replaced. Stable per-path IDs make reruns reproducible.
    if meta.exists():return
    guid=uuid.uuid5(uuid.NAMESPACE_URL,'TeamRedBull2/ArakusRoom/'+str(path.relative_to(ROOT))).hex
    meta.write_text('fileFormatVersion: 2\nguid: '+guid+'\n'+('folderAsset: yes\n' if folder else '')+'DefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')

def save_asset(im,path):
    path.parent.mkdir(parents=True,exist_ok=True);im.save(path);new_meta(path)

# ---- Offline projection/mockup only. These functions do not ship in the game. ----
def layer_composite(variant):
    farim=Image.open(ART/variant/'room_far.png').convert('RGBA');midim=Image.open(ART/variant/'room_mid.png').convert('RGBA')
    result=Image.alpha_composite(farim,midim);tile=Image.open(ART/variant/'floor_tile.png').convert('RGBA')
    for x in range(0,W,128):result.alpha_composite(tile,(x,GROUND))
    return result

def camera_image(source,width=640,height=360):
    aspect=width/height;ortho=max(8.2,14.3/aspect);vw=ortho*2*aspect;vh=ortho*2
    box=(W/2-vw/2*PPU,GROUND-(5+vh/2)*PPU,W/2+vw/2*PPU,GROUND-(5-vh/2)*PPU)
    # Exact camera dimensions, nearest sampling; differs from a simple center crop at 4:3.
    im=source.transform((width,height),Image.Transform.EXTENT,box,Image.Resampling.NEAREST)
    return im,lambda x,y:(width/2+x*width/vw,height/2+(5-y)*height/vh),height/vh

def actor(im,world,ppu,who,future=False):
    if who=='player':
        frame=Image.open(ROOT/'Assets/Art/Player/SilverRogueExtended/SilverRogueV3_Idle.png').convert('RGBA').crop((0,0,192,192))
        frame=ImageOps.mirror(frame);scale=1.4;importppu=75;pivot=(192-100.8896,160);pos=(-6,0)
    else:
        frame=Image.open(ROOT/'Assets/Resources/ArakusV1/Idle.png').convert('RGBA').crop((0,0,192,192))
        frame=ImageOps.mirror(frame);scale=2.2 if future else 1.4;importppu=56;pivot=(96,160);pos=(5,0)
    k=ppu*scale/importppu
    size=round(192*k);frame=frame.resize((size,size),Image.Resampling.NEAREST);x,y=world(*pos)
    im.alpha_composite(frame,(round(x-pivot[0]*size/192),round(y-pivot[1]*size/192)))

def mock(variant,future=False,aspect43=False,cues=False):
    im,world,ppu=camera_image(layer_composite(variant),480 if aspect43 else 640,360)
    # Draw gameplay pillars at the actual +/-4 world positions, current effective PPU=40.
    frame=Image.open(ART/variant/'pillar_intact.png').convert('RGBA');k=ppu/40
    fr=frame.resize((round(192*k),round(192*k)),Image.Resampling.NEAREST)
    for xx in [-4,4]:
        x,y=world(xx,0);im.alpha_composite(fr,(round(x-96*fr.width/192),round(y-160*fr.height/192)))
    if cues:
        d=ImageDraw.Draw(im)
        x0,y=world(1.7,0);x1,_=world(5.3,0)
        d.rectangle((round(x0),round(y)-2,round(x1),round(y)),fill=(255,76,81,255))
        # Representative blue parry outline around the planned boss; exact production glow not emulated.
        bx,by=world(5,0);h=(3.49 if future else 2.2)*ppu
        d.line((round(bx-ppu*.85),round(by-h),round(bx-ppu*.85),round(by-4)),fill=(71,158,255,255),width=2)
        d.line((round(bx-ppu*.85),round(by-h),round(bx+ppu*.85),round(by-h)),fill=(184,224,255,255),width=1)
    actor(im,world,ppu,'player');actor(im,world,ppu,'boss',future)
    return im

def existing_proxy():
    # Existing background asset positions/tint follow WorldArtist; procedural details approximated.
    im=Image.new('RGBA',(640,360),(9,13,19,255));ortho=8.2;ppu=360/(ortho*2)
    world=lambda x,y:(320+x*ppu,180+(5-y)*ppu)
    def rect(x,y,w,h,c):
        xx,yy=world(x,y);ImageDraw.Draw(im).rectangle((round(xx-w*ppu/2),round(yy-h*ppu/2),round(xx+w*ppu/2),round(yy+h*ppu/2)),fill=c)
    for i in range(-3,4):
        x=i*4.8;rect(x,4.5,3.5,9,(11,18,24));rect(x-1.8,4.7,.28,10,(22,28,33));rect(x+1.8,4.7,.28,10,(22,28,33))
    for name,spacing,cy,ww,hh,tint in [('parallaxBG-interior2.png',11.2,6.7,11.2,14,(.65,.7,.8)),('parallaxBG-interior1.png',14,7,14,14,(.8,.85,.95))]:
        src=Image.open(ROOT/'Assets/StreamingAssets/Art/Castle'/name).convert('RGBA');key=src.getpixel((0,src.height-1))[:3]
        pix=[]
        for r,g,b,a in pixels(src):pix.append((int(r*tint[0]),int(g*tint[1]),int(b*tint[2]),0 if (r,g,b)==key else round(a*.75)))
        src.putdata(pix);src=src.resize((round(ww*ppu),round(hh*src.height/src.width*ppu)),Image.Resampling.NEAREST)
        for i in [-1,0,1]:
            x,y=world(i*spacing,cy);im.alpha_composite(src,(round(x-src.width/2),round(y-src.height/2)))
    rect(0,-.45,30,.9,(31,41,48));rect(0,-.04,30,.09,(56,71,84))
    tile=Image.open(ROOT/'Assets/StreamingAssets/Art/Castle/twilight-tiles.png').convert('RGBA').crop((0,0,48,16))
    tile=tile.resize((round(3*ppu),round(ppu/3)),Image.Resampling.NEAREST)
    for i in range(-5,6):
        x,y=world(i*3,-.48);im.alpha_composite(tile,(round(x-tile.width/2),round(y-tile.height/2)))
    old=Image.open(ROOT/'Assets/Resources/ArakusV1/Pillar.png').convert('RGBA').crop((0,0,192,192));sz=round(192*ppu/40);old=old.resize((sz,sz),Image.Resampling.NEAREST)
    for xx in [-4,4]:
        x,y=world(xx,0);im.alpha_composite(old,(round(x-96*sz/192),round(y-160*sz/192)))
    actor(im,world,ppu,'player');actor(im,world,ppu,'boss')
    return im

def preview(im,path,scale=2):
    im.convert('RGB').resize((im.width*scale,im.height*scale),Image.Resampling.NEAREST).save(DOC/path)

def panel_sheet(items,path,cols=2):
    cellw,cellh=640,394;rows=math.ceil(len(items)/cols)
    out=Image.new('RGB',(cellw*cols,cellh*rows),(17,19,24));d=ImageDraw.Draw(out)
    for i,(title,img) in enumerate(items):
        x=i%cols*cellw;y=i//cols*cellh;d.text((x+12,y+9),title,fill=(205,202,191))
        out.paste(img.convert('RGB'),(x,y+34))
    out.save(DOC/path)

def main():
    ART.mkdir(parents=True,exist_ok=True);new_meta(ART,True)
    for variant in SEEDS:
        folder=ART/variant;folder.mkdir(exist_ok=True);new_meta(folder,True)
        save_asset(far(variant),folder/'room_far.png');save_asset(mid(variant),folder/'room_mid.png');save_asset(floor(variant),folder/'floor_tile.png')
        intact,broken=pillar(variant),pillar(variant,True)
        save_asset(intact,folder/'pillar_intact.png');save_asset(broken,folder/'pillar_broken.png')
        sheet=Image.new('RGBA',(384,192));sheet.alpha_composite(intact);sheet.alpha_composite(broken,(192,0));save_asset(sheet,folder/'pillar_sheet.png')
        clean,_,_=camera_image(layer_composite(variant));preview(clean,variant+'_room_16x9.png')
        preview(clean.crop((80,0,560,360)),variant+'_room_4x3_crop.png')
        cam,_,_=camera_image(layer_composite(variant),480,360);preview(cam,variant+'_room_4x3_camera.png')
        for future in [False,True]:preview(mock(variant,future),variant+('_future_mockup.png' if future else '_current_mockup.png'))
        fm=mock(variant,True);preview(fm.crop((80,0,560,360)),variant+'_future_4x3_crop.png')
        preview(mock(variant,True,True),variant+'_future_4x3_camera.png')
        preview(mock(variant,True,cues=True),variant+'_readability.png')
        tile=Image.open(folder/'floor_tile.png');repeat=Image.new('RGBA',(128*6,48))
        for i in range(6):repeat.alpha_composite(tile,(128*i,0))
        preview(repeat,variant+'_floor_repeat.png')
    original=existing_proxy();preview(original,'existing_assets_proxy_16x9.png')
    panel_sheet([('Existing assets - offline approximation, NOT Unity capture',original),('A - Stone Fortress / current actors',mock('StoneFortress')),('B - Iron Warworks / current actors',mock('IronWarworks'))],'existing_vs_concepts.png',cols=1)
    panel_sheet([('A - Stone Fortress / planned ~2x-player boss',mock('StoneFortress',True)),('B - Iron Warworks / planned ~2x-player boss',mock('IronWarworks',True))],'concepts_future_comparison.png')
    old=Image.open(ROOT/'Assets/Resources/ArakusV1/Pillar.png').convert('RGBA');atlas=Image.new('RGBA',(1152,224),(22,24,29,255));d=ImageDraw.Draw(atlas)
    for i,(name,sheet) in enumerate([('Existing pillar reference',old),('A / Stone Fortress',Image.open(ART/'StoneFortress/pillar_sheet.png')),('B / Iron Warworks',Image.open(ART/'IronWarworks/pillar_sheet.png'))]):
        atlas.alpha_composite(sheet,(384*i,24));d.text((384*i+10,8),name,fill='white');d.line((384*i,184,384*(i+1)-1,184),fill=(83,94,98))
    preview(atlas,'pillar_comparison.png',2)
    paths=sorted(ART.rglob('*.png'))
    manifest={'design':'Brutal Crimson Fortress','originality':'Original deterministic Pillow material/architecture painting. Existing art only in mockups; no generated/recolored Castle backgrounds.',
      'base_commit':'f13bb6bd25f469e38acce05eceb712712dc30c3f','source_canvas':[W,H],'source_ground_row':GROUND,'source_pixels_per_world_unit':PPU,
      'pillar_contract':{'sheet':[384,192],'frame':[192,192],'frames':2,'pivot':[.5,32/192],'import_ppu':56,'existing_root_scale':1.4,'world_intact_height':3.0},
      'assets':[{'path':str(p.relative_to(ROOT)),'size':list(Image.open(p).size),'mode':Image.open(p).mode,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in paths]}
    (DOC/'asset_manifest.json').write_text(json.dumps(manifest,indent=2));(DOC/'palette.json').write_text(json.dumps(P,indent=2))
    print('Created',len(paths),'runtime PNGs, two independent concept sets, and offline previews. No integration.')

if __name__=='__main__':main()
