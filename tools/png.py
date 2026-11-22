"""PNG codec (standard library only) and the tint used on borrowed placeholder textures.

decode/encode: 8-bit grey, grey+alpha, RGB, RGBA and palette images; all filter types; no interlacing.
Written out as 8-bit RGBA. (Same codec as BDArmory Lite's tools/recolor.py.)

tint(): pulls every pixel towards a colour, keeping its brightness. Saturated pixels (paint, markings) take the
full colour; greys take a share of it, so bare metal reads as tinted gunmetal rather than staying neutral.
"""
import colorsys, struct, zlib

PNG_SIG = b'\x89PNG\r\n\x1a\n'
CHANNELS = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}


def _paeth(a, b, c):
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    return b if pb <= pc else c


def decode(data):
    """Return (width, height, rows) with rows a list of bytearrays of RGBA pixels."""
    if not data.startswith(PNG_SIG):
        raise ValueError('not a PNG')
    pos, idat, plte, trns = 8, [], None, None
    while pos < len(data):
        length, ctype = struct.unpack('>I4s', data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        pos += 12 + length
        if ctype == b'IHDR':
            w, h, depth, colour, _, _, interlace = struct.unpack('>IIBBBBB', body)
        elif ctype == b'PLTE':
            plte = body
        elif ctype == b'tRNS':
            trns = body
        elif ctype == b'IDAT':
            idat.append(body)
        elif ctype == b'IEND':
            break
    if depth != 8 or colour not in CHANNELS or interlace:
        raise ValueError(f'unsupported PNG (bit depth {depth}, colour type {colour}, interlace {interlace})')
    bpp = CHANNELS[colour]
    stride = w * bpp
    raw = zlib.decompress(b''.join(idat))
    rows, prev = [], bytearray(stride)
    for y in range(h):
        off = y * (stride + 1)
        ftype, line = raw[off], bytearray(raw[off + 1:off + 1 + stride])
        if ftype == 1:
            for i in range(bpp, stride):
                line[i] = (line[i] + line[i - bpp]) & 255
        elif ftype == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 255
        elif ftype == 3:
            for i in range(stride):
                line[i] = (line[i] + (((line[i - bpp] if i >= bpp else 0) + prev[i]) >> 1)) & 255
        elif ftype == 4:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                c = prev[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + _paeth(a, prev[i], c)) & 255
        elif ftype != 0:
            raise ValueError(f'bad filter type {ftype}')
        prev = line
        rgba = bytearray(w * 4)
        for x in range(w):
            if colour == 6:
                rgba[x * 4:x * 4 + 4] = line[x * 4:x * 4 + 4]
            elif colour == 2:
                r, g, b = line[x * 3:x * 3 + 3]
                a = 0 if trns and len(trns) >= 6 and (r, g, b) == struct.unpack('>HHH', trns[:6]) else 255
                rgba[x * 4:x * 4 + 4] = bytes((r, g, b, a))
            elif colour == 0:
                v = line[x]
                a = 0 if trns and len(trns) >= 2 and v == struct.unpack('>H', trns[:2])[0] else 255
                rgba[x * 4:x * 4 + 4] = bytes((v, v, v, a))
            elif colour == 4:
                v, a = line[x * 2:x * 2 + 2]
                rgba[x * 4:x * 4 + 4] = bytes((v, v, v, a))
            else:  # palette
                i = line[x]
                r, g, b = plte[i * 3:i * 3 + 3]
                a = trns[i] if trns and i < len(trns) else 255
                rgba[x * 4:x * 4 + 4] = bytes((r, g, b, a))
        rows.append(rgba)
    return w, h, rows


def encode(w, h, rows):
    def chunk(ctype, body):
        return struct.pack('>I', len(body)) + ctype + body + struct.pack('>I', zlib.crc32(ctype + body) & 0xffffffff)
    raw = b''.join(b'\x00' + bytes(r) for r in rows)
    return (PNG_SIG + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


def parse_hex(colour):
    colour = colour.lstrip('#')
    return tuple(int(colour[i:i + 2], 16) / 255 for i in (0, 2, 4))


def tint_pixel(r, g, b, colour, grey_share):
    """colour: (r, g, b) 0-1. grey_share: how much of the tint a fully grey pixel takes (0-1)."""
    h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    th, ts, _ = colorsys.rgb_to_hsv(*colour)
    amount = grey_share + (1 - grey_share) * min(1.0, s * 2)
    nr, ng, nb = colorsys.hsv_to_rgb(th, ts, v)
    gr = gg = gb = v
    return (round((gr + (nr - gr) * amount) * 255), round((gg + (ng - gg) * amount) * 255),
            round((gb + (nb - gb) * amount) * 255))


def tint(data, colour, grey_share=0.3):
    """Tint PNG bytes towards colour ('#rrggbb'); returns new PNG bytes."""
    rgb_t = parse_hex(colour)
    w, h, rows = decode(data)
    cache = {}
    for row in rows:
        for x in range(0, w * 4, 4):
            if row[x + 3] == 0:
                continue
            rgb = (row[x], row[x + 1], row[x + 2])
            new = cache.get(rgb)
            if new is None:
                new = cache[rgb] = tint_pixel(*rgb, rgb_t, grey_share)
            row[x:x + 3] = bytes(new)
    return encode(w, h, rows)
