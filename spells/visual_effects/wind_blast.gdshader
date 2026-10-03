shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, shadows_disabled;

// Wind Blast: a translucent block the size of the blast, there as it fires and fading out.
uniform vec4 wind_color : source_color = vec4(0.55, 0.92, 0.59, 0.25);

// 0 the instant it fires, 1 as it vanishes. Set per blast by Beam.
instance uniform float progress = 0.0;

void fragment() {
    ALBEDO = wind_color.rgb;
    ALPHA = wind_color.a * (1.0 - progress);
}
