"""Original deterministic pixel-art production. Pillow only; updates approved ArakusRoom output and docs, never game code or source Castle/character art.
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

# StoneFortress redesign: composition from the supplied throne concept, drawn afresh
# in the game's pixel idiom. No photo pixels, blur, smooth gradients or color filtering.
THRONE_RED=[(18,8,16),(54,9,24),(108,14,33),(166,24,42),(211,38,49)]
THRONE_STONE=[(10,13,19),(17,19,26),(24,25,33),(34,32,41),(47,41,49),(62,51,57)]

# Stone family: outline + shadow/midtone/light + occasional chipped highlight.
# Red family is separate so the backlight, banners and martial symbols remain crimson.
THRONE_RENDER_PALETTE=[(9,13,20),(17,19,28),(29,29,39),(46,40,51),(66,49,60),(90,65,70),
    (18,8,16),(42,12,25),(65,12,29),(93,17,34),(135,37,48),
    (54,9,24),(108,14,33),(166,24,42),(211,38,49),(64,65,69)]

def throne_pixel_planes(im):
    """Map authored material colors to finite planes; no resampling, alpha or geometry changes."""
    rgba=im.convert('RGBA');lookup={}
    for color in set(pixels(rgba)):
        if color[3]==0:lookup[color]=color;continue
        chosen=min(THRONE_RENDER_PALETTE,key=lambda c:sum((c[i]-color[i])**2 for i in range(3)))
        lookup[color]=chosen+(color[3],)
    rgba.putdata([lookup[c] for c in pixels(rgba)])
    return rgba

def hewn_face(im,points,seed,base=(26,25,34),density=65):
    poly(im,points,base)
    rng=random.Random(seed);mask=Image.new('L',im.size);ImageDraw.Draw(mask).polygon(points,fill=255)
    box=mask.getbbox();d=ImageDraw.Draw(im)
    # Sparse 2-4px chips grouped into angular clusters, not fine grain across the face.
    for _ in range(max(1,(box[2]-box[0])*(box[3]-box[1])//(density*3))):
        x=rng.randrange(box[0],box[2]);y=rng.randrange(box[1],box[3])
        c=shift(base,rng.choice([-10,12]))
        for ox,oy in [(0,0),(1,0),(2,0),(0,1),(1,1),(0,2)]:
            xx,yy=x+ox,y+oy
            if xx<im.width and yy<im.height and mask.getpixel((xx,yy)):d.point((xx,yy),fill=c)
    # A few long fractured facets, rather than a regular brick grid.
    for _ in range(2):
        x=rng.randrange(box[0],box[2]);y=rng.randrange(box[1],box[3]);pts=[]
        for j in range(5):
            xx=x+rng.randint(-3,3);yy=y+j*5
            if 0<=xx<im.width and 0<=yy<im.height and mask.getpixel((xx,yy)):pts.append((xx,yy))
        if len(pts)>1:
            stroke(im,pts,shift(base,-12));stroke(im,[(x+1,y) for x,y in pts],shift(base,12))

def war_mark(im,cx,cy,size=1,color=(119,25,42)):
    # A cleft spear/war crest. No royal trim or cathedral ornament.
    stroke(im,[(cx,cy-12*size),(cx,cy+13*size)],color,max(1,size))
    poly(im,[(cx,cy-18*size),(cx-3*size,cy-10*size),(cx,cy-12*size),(cx+3*size,cy-10*size)],color)
    for sign in [-1,1]:
        stroke(im,[(cx+sign*2*size,cy+2*size),(cx+sign*8*size,cy-4*size),(cx+sign*6*size,cy+5*size),(cx+sign*2*size,cy+9*size)],color,max(1,size))

def basalt_column(im,cx,top,bottom,width,seed,near=False):
    rng=random.Random(seed);half=width//2;d=ImageDraw.Draw(im)
    # Broad bases and uneven shoulders imply mass; shallow vertical planes keep the style coarse.
    hewn_face(im,[(cx-half+4,top+9),(cx+half-5,top+9),(cx+half-8,bottom-17),(cx-half+7,bottom-17)],seed,(22,22,31))
    inner=1 if cx<384 else -1
    for offset,col in [(-half+8,(12,16,23)),(-half+15,(25,27,36)),(-3,(32,29,38)),(half-13,(17,19,28))]:
        poly(im,[(cx+offset,top+11),(cx+offset+5,top+11),(cx+offset+3,bottom-18),(cx+offset,bottom-18)],col)
    xedge=cx+inner*(half-9)
    stroke(im,[(xedge,top+13),(xedge-inner*2,bottom-20)],(73,23,36) if near else (58,22,34),2)
    for yy in range(top+69,bottom-24,65):
        stroke(im,[(cx-half+7,yy),(cx+half-8,yy-1)],(10,13,20),2)
        stroke(im,[(cx-half+8,yy+2),(cx+half-9,yy+1)],(40,33,43))
    for _ in range(12):
        x=cx+rng.randint(-half+9,half-10);y=rng.randint(top+15,bottom-24)
        stroke(im,[(x,y),(x+3,y+3),(x+1,y+8)],(9,13,20))
        d.point((x+4,y+3),fill=(59,36,45))
    for yy,extra in [(top,5),(top+7,1),(bottom-19,0),(bottom-11,6),(bottom-5,11)]:
        poly(im,[(cx-half-extra+3,yy),(cx+half+extra-4,yy),(cx+half+extra,yy+5),(cx-half-extra,yy+5)],(32,28,37))
        stroke(im,[(cx-half-extra+3,yy),(cx+half+extra-4,yy)],(66,40,47) if yy<bottom-10 else (46,36,43))
        stroke(im,[(cx-half-extra,yy+5),(cx+half+extra,yy+5)],(9,12,18),2)

def throne_far():
    im=Image.new('RGB',(W,H),(13,9,18));d=ImageDraw.Draw(im)
    # Five explicit light planes. 4px sampling creates stepped silhouettes;
    # only a few 2x2 clusters at band boundaries bridge adjacent values.
    for y in range(0,326,4):
        spread=104+(y//16)*3
        fall=1 if y<208 else max(.13,1-((y-208)//12)*.09)
        for x in range(144,624,4):
            strength=max(0,1-abs(x-384)/spread)*fall
            band=min(4,int(strength*5))
            if band>0:
                d.rectangle((x,y,x+3,min(325,y+3)),fill=THRONE_RED[band])
                fraction=strength*5-int(strength*5)
                if band>1 and fraction<.18 and (x//4+3*(y//4))%5==0:
                    d.rectangle((x,y,x+1,y+1),fill=THRONE_RED[band-1])
    # Clear descending shafts with angular, irregular sides and a darkened lower end.
    for coords,col in [([(348,0),(419,0),(407,141),(421,218),(407,274),(360,274),(347,206),(360,121)],(166,24,42)),
                       ([(367,0),(400,0),(395,92),(405,158),(396,231),(372,231),(364,154),(373,73)],(203,36,49))]:
        poly(im,coords,col)
    # Side walls are towering slabs, not a field of prison-cell brickwork.
    for side in [0,1]:
        mirror=lambda pts:[(W-x if side else x,y) for x,y in pts]
        hewn_face(im,mirror([(0,0),(183,0),(219,162),(192,339),(0,369)]),901+side,(24,16,27),45)
        poly(im,mirror([(173,0),(204,0),(237,171),(213,334),(190,337),(211,170)]),(42,15,29))
        for y in [88,190,281]:stroke(im,mirror([(0,y+14),(190,y)]),(63,21,34))
        poly(im,mirror([(0,55),(196,106),(213,124),(0,78)]),(17,17,26))
        stroke(im,mirror([(0,56),(196,107),(210,120)]),(77,24,37),2)
        poly(im,mirror([(0,80),(205,133),(207,147),(0,98)]),(9,13,20))
    # Heavy segmented stone arch, receding in depth behind the throne.
    cx,cy=384,174
    for k in range(18):
        a0=math.pi*k/18+.012;a1=math.pi*(k+1)/18-.012
        pts=[(cx+180*math.cos(a0),cy-166*math.sin(a0)),(cx+180*math.cos(a1),cy-166*math.sin(a1)),
             (cx+155*math.cos(a1),cy-142*math.sin(a1)),(cx+155*math.cos(a0),cy-142*math.sin(a0))]
        poly(im,pts,[(26,22,33),(33,25,36),(23,22,32)][k%3])
        stroke(im,[pts[3],pts[2]],(89,25,39),2)
        stroke(im,[pts[0],pts[1]],(45,29,40))
    for x in [224,544]:basalt_column(im,x,154,342,43,310+x)
    for x in [275,493]:basalt_column(im,x,175,318,29,901+x)
    # The lowest light is a low-value red recess, behind the dais and actors.
    poly(im,[(163,319),(605,319),(646,359),(122,359)],(42,12,25))
    # Broad stepped altar. Perspective widens towards the viewer, leaving the walk plane flat.
    for i in range(8):
        yy=302+i*7;hw=80+i*29
        poly(im,[(384-hw+10,yy),(384+hw-10,yy),(384+hw,yy+3),(384-hw,yy+3)],(39,32,42))
        poly(im,[(384-hw,yy+3),(384+hw,yy+3),(384+hw-1,yy+7),(384-hw+1,yy+7)],(15,18,26))
        stroke(im,[(384-hw+10,yy),(384+hw-10,yy)],(65,47,54))
        stroke(im,[(384-hw+3,yy+4),(384+hw-3,yy+4)],(23,24,33))
        rng=random.Random(521+i)
        for j in range(5):
            xx=rng.randint(384-hw+12,384+hw-12)
            stroke(im,[(xx,yy),(xx-3,yy+2),(xx+1,yy+5)],(8,12,19))
            d.point((xx+1,yy),fill=(84,57,61))
    # Distinct tall throne back with a central crown and rough blade-like stone shoulders.
    back=[(348,269),(348,212),(341,201),(341,178),(351,194),(359,175),(359,170),(365,178),
          (384,154),(403,178),(409,170),(409,175),(417,194),(427,178),(427,201),(420,212),(420,269)]
    hewn_face(im,back,819,(18,21,29),35)
    poly(im,[(384,160),(401,181),(401,261),(384,268)],(31,29,38))
    poly(im,[(365,185),(384,160),(384,268),(365,259)],(25,26,35))
    stroke(im,[(348,211),(348,265)],(72,27,40))
    stroke(im,[(420,211),(420,265)],(72,27,40))
    stroke(im,[(365,179),(384,155),(403,179)],(68,43,52))
    # Fissures frame a simple martial sigil; no ornate royal border.
    for pts in [[(367,192),(377,203),(374,216),(381,230)],[(402,187),(390,202),(395,217),(387,242)],[(360,244),(374,250),(371,262)]]:
        stroke(im,pts,(8,13,20));stroke(im,[(x+1,y) for x,y in pts],(54,39,49))
    war_mark(im,384,221,1,(137,28,43))
    # Seat and open shadow beneath make this unmistakably a chair rather than an obelisk.
    poly(im,[(350,265),(418,265),(427,274),(342,274)],(59,49,57))
    hewn_face(im,[(344,274),(425,274),(418,291),(353,290)],929,(29,29,38),24)
    stroke(im,[(346,274),(423,274)],(90,67,69))
    stroke(im,[(365,275),(373,281),(369,287)],(10,14,22))
    poly(im,[(353,290),(417,290),(414,303),(354,303)],(8,12,19))
    for xx in [339,429]:
        hewn_face(im,[(xx-6,265),(xx+7,265),(xx+5,299),(xx-5,299)],xx,(29,29,38),22)
        stroke(im,[(xx-5,269),(xx-5,298)],(62,42,51))
        poly(im,[(xx-11,297),(xx+11,297),(xx+13,304),(xx-13,304)],(35,31,40))
        stroke(im,[(xx-10,297),(xx+10,297)],(75,50,57))
        poly(im,[(xx-12,253),(xx+12,253),(xx+15,258),(xx+11,265),(xx-11,265),(xx-15,258)],(34,32,42))
        stroke(im,[(xx-12,253),(xx+12,253),(xx+15,258)],(94,64,68),2)
        stroke(im,[(xx-12,263),(xx+10,263)],(10,14,22),2)
    # Quiet foreground and a deep basalt foundation for the real 4:3 camera.
    poly(im,[(0,360),(W,360),(W,H),(0,H)],(16,16,24))
    for yy in [388,430,474]:
        stroke(im,[(0,yy),(W,yy)],(9,13,20),3)
        for xx in range(0,W,113):stroke(im,[(xx,yy),(xx+5,yy+16),(xx+1,yy+35)],(27,23,33))
    return im.convert('RGBA')

def throne_mid():
    im=Image.new('RGBA',(W,H));d=ImageDraw.Draw(im)
    for x in [122,646]:basalt_column(im,x,-19,366,72,1100+x,True)
    # Torn cloth hangs from stone pins, not bars, cages, chains or guard platforms.
    for side in [0,1]:
        x=190 if side==0 else 578;sign=1 if side==0 else -1
        poly(im,[(x-30,67),(x+30,67),(x+29,211),(x+20,200),(x+15,223),(x+7,204),(x-1,220),
                 (x-8,203),(x-15,216),(x-24,198),(x-29,208)],(65,12,29))
        poly(im,[(x-26,73),(x-16,73),(x-14,202),(x-22,197)],(93,17,34))
        poly(im,[(x-6,72),(x+5,72),(x+8,204),(x,211)],(48,10,25))
        stroke(im,[(x+22,76),(x+22,193),(x+18,204)],(108,21,38),2)
        stroke(im,[(x-30,67),(x+30,67)],(30,25,36),4)
        for off in [-23,23]:
            d.polygon([(x+off-2,64),(x+off+2,64),(x+off+3,71),(x+off-3,71)],fill=(59,35,45))
        war_mark(im,x,133,2,(135,37,48))
        # Real missing cloth shapes and stitch-width edge damage, not a rectangular flag silhouette.
        for pts in [[(x-29,119),(x-20,124),(x-29,128)],[(x+29,153),(x+19,161),(x+29,166)],
                    [(x-8,183),(x-4,172),(x-1,185),(x-4,191)]]:poly(im,pts,(0,0,0,0))
        stroke(im,[(x-27,83),(x-26,110)],(115,25,42))
    return im

def throne_floor():
    im=Image.new('RGBA',(128,48),(22,23,31,255));d=ImageDraw.Draw(im)
    # Quiet black stone paving, with finite, cool bevels above a hewn basalt face.
    for yy,c in enumerate([(64,65,69),(53,54,61),(39,41,51),(30,32,43)]):d.line((0,yy,127,yy),fill=c)
    rng=random.Random(8199)
    for pts,col in [([(4,6),(51,5),(46,26),(8,28)],(31,31,42)),
                    ([(57,7),(113,5),(122,28),(61,27)],(28,28,39)),
                    ([(0,32),(58,30),(61,47),(0,47)],(22,24,34)),
                    ([(67,33),(127,30),(127,47),(65,47)],(24,25,35))]:
        poly(im,pts,col)
    for x in [53,117]:
        stroke(im,[(x,3),(x-3,13),(x+1,20),(x-1,29)],(10,14,23),2)
        stroke(im,[(x+2,5),(x-1,13)],(52,43,54))
    for _ in range(22):
        x=rng.randint(3,124);y=rng.randint(6,46)
        d.line((x,y,min(125,x+rng.randint(1,3)),y),fill=rng.choice([(17,21,31),(38,33,45),(44,31,44)]))
    # Tiny low-value reflected crimson flecks, not glowing attack-shaped floor markings.
    for x,y in [(24,8),(75,12),(101,7)]:d.line((x,y,x+3,y),fill=(56,26,39))
    for y in range(48):im.putpixel((127,y),im.getpixel((0,y)))
    return im

def throne_pillar(broken=False):
    im=Image.new('RGBA',(192,192));d=ImageDraw.Draw(im)
    if not broken:
        pts=[(76,46),(81,42),(91,44),(97,40),(109,44),(117,49),(113,147),(125,155),(125,159),(65,159),(67,152),(76,146)]
        hewn_face(im,pts,4561,(27,29,39),24)
        poly(im,[(82,51),(96,49),(96,146),(81,146)],(39,36,47))
        poly(im,[(104,50),(113,51),(111,146),(104,146)],(15,21,32))
        stroke(im,[(78,52),(77,145)],(69,33,46),2)
        stroke(im,[(83,48),(97,42),(110,47)],(74,53,63))
        for yy in [55,141,150]:
            poly(im,[(73 if yy<141 else 69,yy),(115 if yy<141 else 121,yy),(118 if yy<141 else 125,yy+5),(70 if yy<141 else 65,yy+5)],(33,31,44))
            stroke(im,[(73 if yy<141 else 69,yy),(115 if yy<141 else 121,yy)],(64,43,55))
        war_mark(im,96,97,1,(104,24,41))
        stroke(im,[(107,68),(101,75),(106,83),(100,91)],(10,17,28))
        stroke(im,[(82,114),(90,120),(87,133)],(12,19,30))
        stroke(im,[(83,115),(91,120)],(57,44,57))
        stroke(im,[(67,159),(125,159)],(34,31,43))
    else:
        hewn_face(im,[(75,121),(82,115),(89,124),(99,114),(105,125),(113,120),(115,146),(128,159),(65,159),(73,145)],4571,(29,29,40),15)
        poly(im,[(75,121),(82,115),(89,124),(99,114),(105,125),(113,120),(110,130),(78,130)],(59,42,55))
        poly(im,[(69,150),(119,150),(128,158),(65,159)],(33,30,43))
        stroke(im,[(70,150),(119,150)],(63,43,56))
        for x,y,w,h in [(67,159,16,10),(112,159,20,12),(79,101,11,8),(105,91,10,7),(96,113,8,6)]:
            poly(im,[(x,y),(x+w,y-1),(x+w-2,y-h+2),(x+3,y-h)],(31,29,42))
            stroke(im,[(x+3,y-h),(x+w-2,y-h+2)],(68,42,56))
        # Preserve the original one-pixel chipped junction of the broken silhouette.
        d.point((89,123),fill=(29,29,39))
        stroke(im,[(97,131),(96,139)],(100,24,40))
        stroke(im,[(65,159),(131,159)],(34,31,43))
    return im


def far(variant):
    if variant=='StoneFortress':return throne_pixel_planes(throne_far())
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
    if variant=='StoneFortress':return throne_pixel_planes(throne_mid())
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
    if variant=='StoneFortress':return throne_pixel_planes(throne_floor())
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
    if variant=='StoneFortress':return throne_pixel_planes(throne_pillar(broken))
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
    panel_sheet([('Existing assets - offline approximation, NOT Unity capture',original),('A - Crimson Throne / current actors',mock('StoneFortress')),('B - Iron Warworks / current actors',mock('IronWarworks'))],'existing_vs_concepts.png',cols=1)
    panel_sheet([('A - Crimson Throne / planned ~2x-player boss',mock('StoneFortress',True)),('B - Iron Warworks / planned ~2x-player boss',mock('IronWarworks',True))],'concepts_future_comparison.png')
    old=Image.open(ROOT/'Assets/Resources/ArakusV1/Pillar.png').convert('RGBA');atlas=Image.new('RGBA',(1152,224),(22,24,29,255));d=ImageDraw.Draw(atlas)
    for i,(name,sheet) in enumerate([('Existing pillar reference',old),('A / War God stone pillars',Image.open(ART/'StoneFortress/pillar_sheet.png')),('B / Iron Warworks',Image.open(ART/'IronWarworks/pillar_sheet.png'))]):
        atlas.alpha_composite(sheet,(384*i,24));d.text((384*i+10,8),name,fill='white');d.line((384*i,184,384*(i+1)-1,184),fill=(83,94,98))
    preview(atlas,'pillar_comparison.png',2)
    paths=sorted(ART.rglob('*.png'))
    manifest={'design':'Crimson Throne of the War God / legacy Iron Warworks','originality':'Original deterministic Pillow material/architecture painting. Existing art only in mockups; no generated/recolored Castle backgrounds.',
      'base_commit':'f13bb6bd25f469e38acce05eceb712712dc30c3f','source_canvas':[W,H],'source_ground_row':GROUND,'source_pixels_per_world_unit':PPU,
      'pillar_contract':{'sheet':[384,192],'frame':[192,192],'frames':2,'pivot':[.5,32/192],'import_ppu':56,'existing_root_scale':1.4,'world_intact_height':3.0},
      'assets':[{'path':str(p.relative_to(ROOT)),'size':list(Image.open(p).size),'mode':Image.open(p).mode,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in paths]}
    (DOC/'asset_manifest.json').write_text(json.dumps(manifest,indent=2));(DOC/'palette.json').write_text(json.dumps({'legacy':P,'throne_red':THRONE_RED,'throne_stone':THRONE_STONE,'throne_render':THRONE_RENDER_PALETTE},indent=2))
    print('Created',len(paths),'runtime PNGs, two independent concept sets, and offline previews. No integration.')

if __name__=='__main__':main()
