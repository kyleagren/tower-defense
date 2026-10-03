shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

// A short curved stroke on a flat card, drawn from one end (UV.x 0) to the other and bowed across the
// card (UV.y), tapering to a point at each end of what's showing so far.
// Bow, as a share of the card's width. Flip the sign if the strokes curve the wrong way around the
// vortex.
uniform float bend = 0.3;
uniform float thickness = 0.08;
// Above 1 lets the level's glow bloom the strokes.
uniform float glow = 1.5;

// Set per stroke through SetInstanceShaderParameter, so every stroke shares one material.
instance uniform float head : hint_range(0.0, 1.0) = 1.0;
instance uniform float opacity : hint_range(0.0, 1.0) = 1.0;
instance uniform vec4 element_color : source_color = vec4(1.0);

void fragment() {
    if (head <= 0.0 || UV.x > head) {
        discard;
    }

    // The bow follows the whole stroke, so its shape holds steady while it draws.
    float centre = 0.5 + bend * (0.5 - sin(UV.x * PI));
    float taper = sin(UV.x / head * PI);

    if (abs(UV.y - centre) > thickness * taper) {
        discard;
    }

    ALBEDO = element_color.rgb * glow;
    ALPHA = opacity;
}
