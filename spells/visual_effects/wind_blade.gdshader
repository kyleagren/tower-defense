shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

uniform vec4 wind_color : source_color = vec4(0.55, 0.92, 0.59, 0.6);
// How much brighter the blade is where its surface turns edge-on to the camera.
uniform float rim_glow = 0.6;

// Set per blade through SetInstanceShaderParameter once it picks up an element.
instance uniform float infused = 0.0;
instance uniform vec4 infusion_color : source_color = vec4(1.0);

void fragment() {
    float rim = 1.0 - abs(dot(NORMAL, VIEW));
    vec3 color = infused > 0.5 ? mix(wind_color.rgb, infusion_color.rgb, 0.7) : wind_color.rgb;

    ALBEDO = color + rim * rim_glow;
    ALPHA = clamp(wind_color.a + rim * 0.3, 0.0, 1.0);
}
