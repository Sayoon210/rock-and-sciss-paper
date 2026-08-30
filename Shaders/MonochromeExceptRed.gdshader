shader_type canvas_item;

// Drains the color out of the whole frame except where it is red, so blood is the only thing on
// screen with a hue. Everything the game is assembled from — a photoscanned wood table, a Mixamo
// character, a CC0 pair of scissors, hand-drawn card art — was authored by someone else against
// a different palette, and color is the axis they disagree on most. Taking it away is what makes
// them read as one scene.
//
// Done as a screen pass rather than by desaturating every texture at import (which ASSETS-3D.md
// originally proposed) because a screen pass can let one hue through and a per-texture bake
// cannot: red has to survive on ANY surface it lands on, blood over a grey character included.
//
// Environment.adjustment_saturation cannot do this either. It scales every hue by the same
// amount, so a value low enough to grey the wood greys the blood along with it.

uniform sampler2D screen_texture : hint_screen_texture, filter_linear;

// How far from pure red a hue may sit and still count, in hue units where the whole wheel is
// 1.0 — so 0.03 is roughly 11 degrees either side. The number that matters most here: the
// table's wood is a dark orange sitting only a little way round the wheel from red, and too
// generous a window leaves it brown while everything else goes grey.
uniform float red_hue_tolerance : hint_range(0.0, 0.2) = 0.03;

// How saturated a pixel must be before its hue is trusted at all. Dark, washed-out reds read as
// "reddish" to the math below without ever looking red on screen; requiring real saturation
// keeps the pass to actual blood.
uniform float minimum_saturation : hint_range(0.0, 1.0) = 0.45;

// Rec. 709 luma. Not a plain average of the channels: the eye reads green as far brighter than
// blue, so averaging turns a saturated green and a saturated blue into the same grey.
const vec3 LUMA_WEIGHTS = vec3(0.2126, 0.7152, 0.0722);

float hue_of(vec3 color, float value, float chroma) {
    if (chroma <= 0.0) {
        return 0.0;
    }

    float hue;
    if (value == color.r) {
        hue = (color.g - color.b) / chroma;
    } else if (value == color.g) {
        hue = 2.0 + (color.b - color.r) / chroma;
    } else {
        hue = 4.0 + (color.r - color.g) / chroma;
    }

    return fract(hue / 6.0);
}

void fragment() {
    vec3 color = texture(screen_texture, SCREEN_UV).rgb;

    float value = max(color.r, max(color.g, color.b));
    float chroma = value - min(color.r, min(color.g, color.b));
    float saturation = value <= 0.0 ? 0.0 : chroma / value;
    float hue = hue_of(color, value, chroma);

    // Red sits at hue 0, which is also hue 1 — the wheel wraps, so both ends are "near red".
    float distance_from_red = min(hue, 1.0 - hue);

    float within_hue =
        1.0 - smoothstep(red_hue_tolerance, red_hue_tolerance * 2.0, distance_from_red);
    float saturated_enough = smoothstep(minimum_saturation * 0.5, minimum_saturation, saturation);
    float keep_color = within_hue * saturated_enough;

    vec3 grey = vec3(dot(color, LUMA_WEIGHTS));
    COLOR = vec4(mix(grey, color, keep_color), 1.0);
}
