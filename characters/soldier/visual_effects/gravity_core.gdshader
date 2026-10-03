shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, shadows_disabled;

// A field's core: a sphere bending the scene behind it - drawn in toward the middle for Converge's black
// hole (bend above 0), pushed out for Repel (below 0) - around a solid center with a glowing edge. The
// bending eases off toward the silhouette, so the sphere has no outline of its own.
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
uniform vec4 center_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform vec4 glow_color : source_color = vec4(0.5, 0.35, 1.0, 1.0);
uniform float glow_strength = 2.0;
uniform float bend = 0.06;
// The solid center's size, as a share of the sphere's apparent radius.
uniform float center_radius : hint_range(0.0, 1.0) = 0.45;
uniform float glow_width = 0.12;
uniform float pulse_speed = 2.0;

void fragment() {
    float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
    // How far out from the middle of the sphere this point appears, 0 to 1.
    float spread = sqrt(1.0 - facing * facing);

    // NORMAL is in view space, where y runs up; screen UVs run down.
    vec2 outward = normalize(vec2(NORMAL.x, -NORMAL.y) + vec2(0.0001));
    float lens = spread * (1.0 - spread) * 4.0;
    vec3 background = textureLod(screen_texture, SCREEN_UV + outward * lens * bend, 0.0).rgb;

    float pulse = 0.85 + 0.15 * sin(TIME * pulse_speed);
    float center = 1.0 - smoothstep(center_radius - 0.04, center_radius + 0.04, spread);
    float glow = (1.0 - smoothstep(0.0, glow_width, abs(spread - center_radius))) * pulse;

    vec3 color = mix(background, center_color.rgb, center * center_color.a);
    color += glow_color.rgb * glow * glow_strength;

    ALBEDO = color;
    ALPHA = 1.0;
}
