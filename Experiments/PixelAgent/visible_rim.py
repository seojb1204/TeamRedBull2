"""Experimental visible blue-rim detector; NOT attack timing or parry eligibility.
Calibrated only at 640x480 on one actual scene/run. Shadow use only.
"""
def detect_visible_rim(width,height,rgb):
    if type(width) is not int or type(height) is not int or (width,height)!=(640,480):
        raise ValueError('Uncalibrated viewport')
    if type(rgb) is not bytes or len(rgb)!=width*height*3:
        raise ValueError('Malformed RGB')
    count=0
    for y in range(220,370):
        for x in range(width):
            k=(y*width+x)*3;r,g,b=rgb[k:k+3]
            count+=b>=180 and g>=100 and b-r>=50 and g-r>=20
    return {'visible_rim_candidate':count>=20,'pixels':count,
            'calibrated_independent_runs':0,'action_authorized':False,
            'limitation':'whole arena: blue projectiles or other FX may trigger; not imminent attack'}
