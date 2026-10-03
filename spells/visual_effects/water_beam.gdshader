shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

// Noise streaming along the beam's length reads as water rushing outward; its peaks are foam.
uniform sampler2D noise : repeat_enable;
uniform vec4 water_color : source_color = vec4(0.2, 0.55, 1.0, 0.45);
uniform vec4 foam_color : source_color = vec4(0.85, 0.95, 1.0, 0.9);
// Flip the sign if the water flows back toward the Ritual.
uniform float flow_speed = 3.0;

void fragment() {
    float n = texture(noise, vec2(UV.x * 2.0, UV.y * 4.0 - TIME * flow_speed)).r;
    float foam = smoothstep(0.62, 0.72, n);
    // Brighter where the surface turns away from the camera, so the beam has a rounded edge.
    float edge = pow(1.0 - clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0), 1.5);
    vec4 color = mix(water_color, foam_color, foam);

    ALBEDO = color.rgb + edge * 0.3;
    ALPHA = clamp(color.a + edge * 0.3, 0.0, 1.0);
}
