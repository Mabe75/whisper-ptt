"""Erzeugt voiceptt.ico (nur Build-Zeit, gehoert nicht zum Produkt)."""
from PIL import Image, ImageDraw

def draw(size):
    s = 1024
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 0, s - 1, s - 1), radius=int(s * 0.22), fill=(16, 22, 38, 255))
    blue = (86, 148, 255, 255)
    # Mikrofonkapsel
    d.rounded_rectangle((s * 0.395, s * 0.17, s * 0.605, s * 0.585), radius=int(s * 0.105), fill=blue)
    # Buegel
    d.arc((s * 0.275, s * 0.30, s * 0.725, s * 0.735), start=0, end=180, fill=blue, width=int(s * 0.055))
    # Staender + Fuss
    d.rectangle((s * 0.472, s * 0.70, s * 0.528, s * 0.83), fill=blue)
    d.rounded_rectangle((s * 0.345, s * 0.815, s * 0.655, s * 0.868), radius=int(s * 0.026), fill=blue)
    return img.resize((size, size), Image.LANCZOS)

sizes = [16, 24, 32, 48, 64, 128, 256]
imgs = [draw(n) for n in sizes]
imgs[-1].save("src/VoicePTT/voiceptt.ico", format="ICO",
              sizes=[(n, n) for n in sizes], append_images=imgs[:-1])
print("ok")
