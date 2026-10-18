# JPEG XL test samples

Files extracted from the inline `data:image/jxl;base64` images on the jpegxl.info JXL Art galleries
(the galleries embed the images in the page rather than linking separate downloads).
All pieces are procedural 'JXL art' and are tiny (tens to hundreds of bytes); each decodes to a full-size image.
Copyright remains with the listed authors; each entry links to the original source. Used here as import test fixtures only.

## SDR

| File | Title | Author | Year | Bytes | Why it is here | Source |
|---|---|---|---|---|---|---|
| `merry_jxl_mas.jxl` | Merry jXl-mas! | Jon Sneyers | 2024 | 276 | 1920x1080, 8-bit, lossless modular, Rec.2100 primaries with sRGB transfer (wide-gamut SDR, 16:9 NPOT) | [gallery](https://jpegxl.info/art/big_gallery.html) · [jxl-art source](https://jxl-art.lucaversari.it/?zcode=3VXLitVAEN3XV9QPzFCP7q7qjQtBcKXgIK6Hua0TGO9cnbjx6z25yfUB7iIMSAh16pnuPqfJy2k%2BjNN8z0nvxp2pCL39Oo3jfDtPj0dWevM4PQ2Wa8dr27vgsmFloQ%2FTARO0m9DrMX26n1klhd6fnm4%2Fnx4GptDN6WE6DtLryrLn%2BQcTys5%2B4ytsve7bRens1shM2Qts76xeyYuwi5N3ge9UarDXQk07WzRYY%2FNG1Qtbwjc%2F5wPrsRoUAuvwG2zplOf6JW%2F4nlMk5lelzIb6Qhmos6BeMK8odazDHPne2LVSb1hnSQKf%2FVyo0hORQqqJkcVIEUbqHOlcOoC0XIEvY7CNV8fDJoBfStjNwvP2L3dgpwaqcaaSZnCWSgbOVGwJOEA1cjfuKtQiuUaCXVhtYLFxTbBXHH796TeopdiqgqoBlVQO5CvYD2mYJ4grhSt3M4paGMxT1s4BlSys56KCs%2B8gUoM7ZKmqawVsohU60FY4PVayE5JRrQtAl4muNZ6YD9UB1LXGoLO0voDAQFskUjiw6b9IxJ75ou%2Fv930CabitVjYQXX47pP%2FkiJJt%2FxF1uQCTP3X05dsY3wdNH%2FmOX7BdQNIV34yZr2wDVi8puQDdUrLZuIDlF0k%2FAA%3D%3D) |
| `hardware_alpha_4k.jxl` | Hardware | Jon Sneyers | 2022 | 48 | 3840x2160, 8-bit RGB + alpha, lossless modular (alpha channel, large decode) | [gallery](https://jpegxl.info/art/big_gallery.html) · [jxl-art source](https://jxl-art.lucaversari.it/?zcode=XY5PD4IwDMXv_RS9kyXAgODFBA2Jp2n8k51RJyxBJLDw-e0QxHlo0t9r32s32txVaypM4bg9YwJS34lWiQ87pcvKYBz5cGn74tnWCiPYd1o1pjD61WAAWd1WBYB-4A3X6AMitYdODdm1z7uONE4aIsOTMhjG8Ui0I2kU-iPZaTaU0hPUBMFXs44pUTAhyMACx85TZzfkE87zCT8Ck3JJ-DkrPJGjh64uqTgsbOP5fNt-koAbYQ3_n78B) |
| `sun_in_the_sky_lossy.jxl` | Sun in the sky | Tirr | 2024 | 61 | 512x512, 10-bit, lossy VarDCT, sRGB (lossy decode path, power-of-two size) | [gallery](https://jpegxl.info/art/big_gallery.html) · [jxl-art source](https://jxl-art.lucaversari.it/?zcode=i4h04gpyDlEw4HLKLElJLSjJUDA04ArPTAEyTA2NuDxSM9MzSsBMruCCnMy8VC4jBQNKIJehkQWFJlgYUOoEM0pNAAYIkDY1NFAw4nLNS4EGDVdmmkKygh1QXkEByjQEMnUVglNLFIxMDeBsXUsDLgjL0sAAAA%3D%3D) |

## HDR

| File | Title | Author | Year | Bytes | Why it is here | Source |
|---|---|---|---|---|---|---|
| `bt2111_test_pattern_pq.jxl` | ITU-R BT.2111-3 (2K, 10 bit, full range PQ) | dogelition | 2025 | 234 | 1920x1080, 10-bit, Rec.2100 primaries, PQ transfer (standard HDR test pattern) | [gallery](https://jpegxl.info/art/hdr.html) · [jxl-art source](https://jxl-art.lucaversari.it/?zcode=rVaxctswDN3xFfwB3wEgaIlLhgy9TLm0HTL3EiXxkGsu8ZD8fW2XlCUBsGRfNvGBJAi8B0D3m8ftS6DcCtx0m%2BeXbSDOAj%2Fe%2F7x2d38%2FwmotYcUkcL3ZPnZv%2B70Iv7oHJsRw9xNg8xSew1VgghDqN%2B%2B%2Bj6t4WB3Wn7u1xKYAPdQw9lAPtjgEj3A7hg%2BGh51hCvcGUoYQVuF3tw2pZdfGqM8VS15%2F2wuYk2uLMTkvSJJggX%2FXe41eHEuUFswTaZiv%2F9jRadmTZcw3SZzyzRw13xGjxXdqGh3W3rCWxsr3QU9kZbUaW8uow9E28XlEJ5OUYX77ZfTF5MpDRVje32SY8er4rNSyjbfjgijeMsEJX6anElmDYHmPoD3n2mq%2B9r1BBr1mD%2BSotIejS%2B5HDyicoTpEo2ZQVYxiCTaiI1gh9ASbKJ4QrBKRXW%2Bm%2FpCjK9rMnrZashtAQxbTaxLNDDG4URSBYB7m%2BUv1gB6UidPbgApZCWfNEU26%2BBxHFhGzTfxkkt2hgHBOD3I9XT57zny2c%2Biy5rX4%2BuGBSZWrGi8cDmU9rvCxb2Pu1NFCDLNDpf6PqFIxnuELf9L5lC7rz0gCM1mLx7EK1qg4s96m1aZqrVYO5kU5c8TiZWxWxXievhfIbOE0RFhwswKPCvgH) |
| `hdr_test_image_pq.jxl` | HDR test image | Jon Sneyers | 2025 | 113 | 1024x512, 10-bit, sRGB primaries, PQ transfer (PQ with non-Rec.2100 primaries) | [gallery](https://jpegxl.info/art/hdr.html) · [jxl-art source](https://jxl-art.lucaversari.it/?zcode=ZZK9TsQwEIT7PMXWB8ntrv8bCkRBCVzBA0Dg0sBJZwl4exwnu5HibuR86xnv5H7K7%2BMln4Gwe3ruHsfp85zBEXfd9AG%2FcAc4i78iDGLXwyuQfGEUFa0oIj0kyypD1CHWUw56lWGvMgaR1inrWFmXzCbV2Hu1CFaBuLnFlEr805ghBQ9wPIC3AeFrytdbuHFwzd%2BXKxyOIBRipQybKJRVaoUimwqRZ73K7KFgF79ILAzvGe%2FTjFh0gtCGlBe81SYWFreEqyp7mKcZzTp9eniBn%2FOUx82hbLAmRVqZnvYhyr5nxOEgL%2B6boKWeauUGSdo3Dy5tVisevDDN5kr5tYNBIveuQbjGMQMFYfyeKX9WtRqcWoWGsfUeHEISJjYM4cIYZVJTtl0QUqQM7Zito38%3D) |
| `heat_hlg.jxl` | Heat | Jon Sneyers | 2025 | 53 | 1024x1024, 10-bit, Rec.2100 primaries, HLG transfer (HLG path) | [gallery](https://jpegxl.info/art/hdr.html) · [jxl-art source](https://jxl-art.lucaversari.it/?zcode=TYuxCgIxEET7%2FYrpQ2Q3sRZU5Cxki0NILXdR03iiQfDvTfCIFjM83u5sUh7jPV8hTP32CPHUx8EJM%2FaHjnRKzwhesCslFVrV%2BJJlZSJKZwxYgQko%2BJ4RsFBYmTFAvvdg1GpoT9XYEIoyGn66TrrHaUzxlv%2FU%2BnVRoztYR03UIcR5%2BgA%3D) |

## Real

Encoded with cjxl 0.11.2 from generated or CC0 sources (phase 4 fixtures).

| File | Properties | Source |
|---|---|---|
| `photo_lossy.jxl` | 2048x1365, 8-bit, lossy VarDCT (distance 1.0), sRGB | CC0 photo "architecture air conditioner building" from avopix.com (https://avopix.com/photo/160011-architecture-air-conditioner-building), as shipped in the cocoon/jxl.Net example folder |
| `photo_jpeg_reconstruct.jxl` | 1024x683, losslessly recompressed JPEG (q90, 4:4:4) | same photo |
| `alpha_gradient.jxl` | 256x256 RGBA lossless; alpha = x/255 with a fully transparent 64x64 hole at (96,96); R = 255-y, G = x, B = 128 | generated |
| `gray_16bit.jxl` | 512x512, 16-bit grayscale lossless; value = (x*128 + (y%64)*7) & 0xFFFF | generated |
| `animated_4frames.jxl` | 128x128, 4 frames, 100 ms each, moving yellow square | generated GIF |
| `hdr_linear_float.jxl` | 256x128, 32-bit float lossless, linear sRGB; R = 16*x/255, G = y/127, B = 0.5 | generated PFM |
| `odd_123x77_lossy.jxl` | 123x77, lossy (distance 2), not a multiple of 4 | downscaled photo |
