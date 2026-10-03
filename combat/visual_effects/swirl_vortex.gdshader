shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

// Spiral arms on a flat disc, turning as the swirl plays out and fading toward its end and its rim.
uniform float arms = 3.0;
// How many turns an arm makes from centre to rim.
uniform float twist = 0.8;
// How far the arms turn over the swirl's life.
uniform float spin = 1.5;
uniform float arm_width : hint_range(0.0, 1.0) = 0.35;
// Above 1 lets the level's glow bloom the arms.
uniform float glow = 1.5;

// Set per swirl through SetInstanceShaderParameter, so every swirl shares one material.
instance uniform float progress : hint_range(0.0, 1.0) = 0.0;
instance uniform vec4 element_color : source_color = vec4(1.0);

void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float r = length(p);

    if (r > 1.0) {
        discard;
    }

    float angle = atan(p.y, p.x) / TAU;
    float band = fract((angle + r * twist - progress * spin) * arms);

    if (band > arm_width) {
        discard;
    }

    ALBEDO = element_color.rgb * glow;
    ALPHA = (1.0 - progress) * (1.0 - r * 0.6);
}
