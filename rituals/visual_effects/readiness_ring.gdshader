shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

// Set per Ritual through SetInstanceShaderParameter, so every Ritual shares one material.
instance uniform float fill : hint_range(0.0, 1.0) = 1.0;
instance uniform vec4 lit_color : source_color = vec4(1.0);
uniform vec4 unlit_color : source_color = vec4(0.0, 0.0, 0.0, 0.35);
// The ring's inner edge, as a fraction of the plane's half-width.
uniform float inner_fraction = 0.82;

void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float r = length(p);

    if (r > 1.0 || r < inner_fraction) {
        discard;
    }

    // 0 at -Z, running clockwise seen from above.
    float angle = fract(atan(p.x, -p.y) / TAU);
    vec4 color = angle <= fill ? lit_color : unlit_color;
    ALBEDO = color.rgb;
    ALPHA = color.a;
}
