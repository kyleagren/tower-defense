shader_type spatial;
render_mode blend_add, unshaded, cull_back, depth_draw_never, shadows_disabled;

// A Harvest wisp's head: white-hot in the middle, its Status's color toward the edge, fading out at the
// silhouette so it reads as a glow rather than a ball.
instance uniform vec4 wisp_color : source_color = vec4(1.0);
uniform float strength = 2.0;

void fragment() {
    float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);

    ALBEDO = mix(wisp_color.rgb, vec3(1.0), pow(facing, 3.0) * 0.6) * strength;
    ALPHA = pow(facing, 1.5);
}
