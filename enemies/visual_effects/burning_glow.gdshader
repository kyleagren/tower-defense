shader_type spatial;
render_mode blend_add, unshaded, cull_back, depth_draw_never, shadows_disabled;

// Drawn as a material_overlay on a burning enemy's body: heat crawling up over it, brightest where the
// surface turns away from the camera, so the silhouette reads as alight. Sampled in world space, so the
// separate body-part meshes burn as one.
uniform sampler2D noise : repeat_enable, filter_linear;
uniform vec4 ember_color : source_color = vec4(0.85, 0.12, 0.0, 1.0);
uniform vec4 flame_color : source_color = vec4(1.0, 0.65, 0.15, 1.0);
uniform float strength = 1.0;
uniform float rim_strength = 1.2;
uniform float rise_speed = 1.0;
uniform float noise_scale = 0.7;
// Pushes the overlay just off the surface it covers, so it never fights that surface for depth.
uniform float grow = 0.015;

varying vec3 world_position;

void vertex() {
    VERTEX += NORMAL * grow;
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec2 uv = vec2(world_position.x + world_position.z, world_position.y) * noise_scale;
    float n = texture(noise, uv - vec2(0.0, TIME * rise_speed)).r;
    float detail = texture(noise, uv * 2.7 + vec2(0.41, -TIME * rise_speed * 1.6)).r;
    float heat = clamp((n + detail - 0.75) * 2.0, 0.0, 1.0);
    float rim = pow(1.0 - clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0), 2.0);

    ALBEDO = mix(ember_color.rgb, flame_color.rgb, heat) * strength;
    ALPHA = clamp(heat * (0.4 + rim * rim_strength), 0.0, 1.0);
}
