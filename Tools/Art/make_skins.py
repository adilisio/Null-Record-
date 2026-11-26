"""Paints Null Record's placeholder character skins over Kenney's Animated Characters UV layout.

Run from the repo root:  python Tools/Art/make_skins.py
Inputs:  Assets/_Project/ThirdParty/Kenney/AnimatedCharacters/Skins/*.png (CC0, Kenney)
Outputs: Assets/_Project/Art/Characters/Skins/{Subject,Dace,Enforcer,Sentry}.png

UV regions (1024 x 1024), read off the source skins:
  head   (0,0)-(640,490)     hair on top, face around (320,220), neck below
  torso  (0,490)-(640,1024)  front panel x 152-490, sleeves either side
  shoes  (640,134)-(824,524)
  hands  (824,134)-(1024,524)
  arms   (640,524)-(1024,764)
  legs   (611,764)-(1024,1024)
"""
import os
from PIL import Image, ImageDraw

SRC = "Assets/_Project/ThirdParty/Kenney/AnimatedCharacters/Skins"
OUT = "Assets/_Project/Art/Characters/Skins"

HEAD = (0, 0, 640, 490)
TORSO = (0, 490, 640, 1024)
FRONT = (152, 490, 490, 1024)
SHOES = (640, 134, 824, 524)
HANDS = (824, 134, 1024, 524)
ARMS = (640, 524, 1024, 764)
LEGS = (611, 764, 1024, 1024)
TOPRIGHT = (640, 0, 1024, 134)


def shade(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c)


def uniform(d, body, accent, legs, boots, sleeves=True):
    d.rectangle(TORSO, fill=body)
    d.rectangle(FRONT, fill=shade(body, 1.08))
    # Side seams and a belt line.
    d.rectangle((140, 490, 152, 1024), fill=shade(body, 0.75))
    d.rectangle((490, 490, 502, 1024), fill=shade(body, 0.75))
    d.rectangle((152, 960, 490, 985), fill=shade(body, 0.6))
    if sleeves:
        d.rectangle(ARMS, fill=shade(body, 0.95))
    d.rectangle(LEGS, fill=legs)
    d.rectangle((611, 990, 1024, 1024), fill=shade(legs, 0.7))
    d.rectangle(SHOES, fill=boots)
    d.rectangle(TOPRIGHT, fill=shade(body, 0.9))
    return accent


def helmet(d, shell, visor):
    d.rectangle(HEAD, fill=shell)
    d.rectangle((0, 330, 640, 490), fill=shade(shell, 0.8))       # collar / neck guard
    d.rectangle((150, 180, 490, 250), fill=(20, 8, 8))           # visor recess
    d.rectangle((160, 192, 480, 238), fill=visor)                # glowing visor
    d.rectangle((160, 192, 480, 200), fill=shade(visor, 1.25))
    d.rectangle((300, 260, 340, 320), fill=shade(shell, 0.7))    # vent


def subject():
    im = Image.open(f"{SRC}/skaterMaleA.png").convert("RGBA")
    d = ImageDraw.Draw(im)
    body = (196, 202, 208)
    uniform(d, body, None, shade(body, 0.92), (60, 64, 70))
    # Cyan piping down the front and a blank ID tag (placeholder: no subject number yet).
    d.rectangle((310, 490, 332, 960), fill=(40, 200, 220))
    d.rectangle((370, 560, 450, 610), fill=(235, 238, 240))
    d.rectangle((370, 560, 450, 568), fill=(40, 200, 220))
    d.rectangle((640, 600, 1024, 618), fill=(40, 200, 220))      # sleeve piping
    # Close-cropped hair: darken the hair mass so it reads as a buzz cut.
    px = im.load()
    for y in range(0, 330):
        for x in range(0, 640):
            r, g, b, a = px[x, y]
            if r < 110 and g < 60:
                px[x, y] = (45, 38, 36, a)
    return im


def dace():
    im = Image.open(f"{SRC}/criminalMaleA.png").convert("RGBA")
    d = ImageDraw.Draw(im)
    body = (44, 58, 86)
    uniform(d, body, None, shade(body, 0.8), (22, 22, 26))
    # Orange armband, chest badge and shoulder straps (security uniform).
    d.rectangle((640, 640, 1024, 680), fill=(235, 130, 40))
    d.polygon([(390, 560), (440, 560), (440, 610), (415, 630), (390, 610)], fill=(235, 170, 60))
    d.rectangle((200, 490, 225, 960), fill=shade(body, 0.7))
    d.rectangle((417, 490, 442, 960), fill=shade(body, 0.7))
    return im


def enforcer():
    im = Image.open(f"{SRC}/criminalMaleA.png").convert("RGBA")
    d = ImageDraw.Draw(im)
    shell = (70, 24, 22)
    helmet(d, shell, (255, 60, 45))
    uniform(d, shell, None, (28, 22, 22), (18, 16, 16))
    # Armour plates.
    for y in (540, 660, 780):
        d.rectangle((180, y, 462, y + 90), fill=shade(shell, 1.25))
        d.rectangle((180, y + 82, 462, y + 90), fill=shade(shell, 0.6))
    d.rectangle(HANDS, fill=(22, 20, 20))                        # gloves
    return im


def sentry():
    im = Image.open(f"{SRC}/cyborgFemaleA.png").convert("RGBA")
    d = ImageDraw.Draw(im)
    shell = (82, 88, 96)
    helmet(d, shell, (255, 70, 50))
    uniform(d, shell, None, (40, 42, 48), (24, 24, 28))
    d.rectangle((152, 700, 490, 730), fill=(200, 50, 40))        # red hazard band
    d.rectangle((640, 700, 1024, 720), fill=(200, 50, 40))
    d.rectangle(HANDS, fill=(36, 38, 42))
    return im


def warden():
    im = Image.open(f"{SRC}/criminalMaleA.png").convert("RGBA")
    d = ImageDraw.Draw(im)
    shell = (38, 38, 42)
    helmet(d, shell, (255, 60, 45))
    uniform(d, shell, None, (24, 24, 26), (14, 14, 16))
    # Heavy plating with yellow hazard bands.
    d.rectangle((160, 520, 482, 940), fill=shade(shell, 1.35))
    for i, y in enumerate(range(540, 920, 60)):
        if i % 2 == 0:
            d.rectangle((160, y, 482, y + 22), fill=(230, 180, 30))
    d.rectangle((640, 560, 1024, 590), fill=(230, 180, 30))
    d.rectangle(HANDS, fill=(20, 20, 22))
    return im


def researcher():
    im = Image.open(f"{SRC}/skaterFemaleA.png").convert("RGBA")
    d = ImageDraw.Draw(im)
    coat = (226, 230, 232)
    uniform(d, coat, None, (52, 58, 70), (40, 40, 46))
    # Teal shirt showing at the collar and a coat pocket with a pen.
    d.polygon([(260, 490), (382, 490), (321, 640)], fill=(40, 150, 140))
    d.rectangle((370, 640, 450, 700), fill=shade(coat, 0.85))
    d.rectangle((388, 615, 396, 660), fill=(40, 90, 200))
    return im


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, fn in (("Subject", subject), ("Dace", dace), ("Enforcer", enforcer), ("Sentry", sentry), ("Warden", warden), ("Researcher", researcher)):
        fn().convert("RGB").save(f"{OUT}/{name}.png", optimize=True)
        print("wrote", f"{OUT}/{name}.png")


if __name__ == "__main__":
    main()
