shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, shadows_disabled;

// The meteor's landing marker on a disc of radius 1 scaled to the blast: the blast's edge from the
// start, and a fill growing out to meet it as the impact nears, brightest at its front.
uniform vec4 color : source_color = vec4(1.0, 0.45, 0.05, 1.0);
uniform float edge_width = 0.06;
uniform float edge_alpha = 0.85;
uniform float fill_alpha = 0.4;
// How much of the fall the marker takes to fade in.
uniform float fade_in = 0.15;
// Above 1 lets the level's glow bloom it.
uniform float brightness = 1.5;

instance uniform float progress = 0.0;

varying vec2 local_xz;

void vertex() {
    local_xz = VERTEX.xz;
}

void fragment() {
    float r = length(local_xz);

    if (r > 1.0) {
        discard;
    }

    float edge = smoothstep(1.0 - edge_width, 1.0 - edge_width * 0.5, r);
    float inside = 1.0 - smoothstep(progress - 0.02, progress, r);
    float fill = inside * mix(0.4, 1.0, r / max(progress, 0.001));
    float alpha = max(edge * edge_alpha, fill * fill_alpha) * smoothstep(0.0, fade_in, progress);

    ALBEDO = color.rgb * brightness;
    ALPHA = alpha;
}
