shader_type canvas_item;

// The red that closes in from the edges of the screen when the player takes a hit. A pure
// how-something-is-drawn effect with no rule in it: what to draw and how hard is decided by
// DamageVignetteUI, which reads the health the round already settled and pushes one number in.
//
// A shader rather than a pre-drawn PNG because the whole thing is one falloff curve and a
// colour. As an image it would be an asset with a provenance row to keep (ATTRIBUTIONS.md), it
// would have to be authored at some fixed resolution, and it would resample on every aspect
// ratio it did not happen to be drawn for. Here the falloff is evaluated per pixel at whatever
// size the window is.

// How red the edges go at full strength, before the falloff below thins it out. Pushed by
// DamageVignetteUI every frame as the flash decays; 0 leaves the frame untouched.
uniform float intensity : hint_range(0.0, 1.0) = 0.0;

// Dark and desaturated on purpose. A bright primary red reads as a UI warning light; blood on
// glass is nearly brown where it is thin.
uniform vec4 blood_color : source_color = vec4(0.42, 0.02, 0.02, 1.0);

// Where the red starts and where it reaches full, measured from the centre in units where 1.0
// is the middle of an edge -- so a corner sits at about 1.41 and saturates well before the
// outer stop, which is what keeps the corners the heaviest part of the frame.
uniform float inner_radius : hint_range(0.0, 2.0) = 0.45;
uniform float outer_radius : hint_range(0.0, 2.0) = 1.15;

void fragment() {
    // UV, not SCREEN_UV: this rect is stretched over the whole viewport so the two agree today,
    // and UV keeps the effect correct if the rect is ever made smaller than the screen.
    //
    // Distance measured in UV space, which is stretched to the window's aspect. Deliberate --
    // correcting for aspect would draw a circle on a 16:9 screen, leaving the left and right
    // edges clear while the top and bottom went solid. An ellipse is what hugs all four edges
    // evenly.
    vec2 from_center = (UV - vec2(0.5)) * 2.0;
    float falloff = smoothstep(inner_radius, outer_radius, length(from_center));

    COLOR = vec4(blood_color.rgb, falloff * intensity);
}
