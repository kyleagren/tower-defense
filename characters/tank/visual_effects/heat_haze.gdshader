shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

// Heat shimmering up off Vent: the scene behind an open cylinder rippled by rising noise, strongest low
// down and where the column faces the camera, fading to nothing at its sides and top so it has no edge
// of its own. The mesh is a unit cylinder scaled into place.
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D noise : repeat_enable, filter_linear;
uniform float strength = 0.015;
uniform float rise_speed = 1.2;
uniform vec4 tint : source_color = vec4(1.0, 0.55, 0.2, 0.1);

varying float height;

void vertex() {
    height = VERTEX.y + 0.5;
}

void fragment() {
    float facing = clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0);
    vec2 uv = vec2(UV.x * 2.0, height * 1.5 - TIME * rise_speed);
    vec2 ripple = vec2(texture(noise, uv).r, texture(noise, uv + vec2(0.37, 0.61)).r) - 0.5;
    float amount = facing * facing * (1.0 - height) * smoothstep(0.0, 0.15, height);

    vec3 background = textureLod(screen_texture, SCREEN_UV + ripple * strength * amount, 0.0).rgb;

    ALBEDO = mix(background, tint.rgb, tint.a * amount);
    ALPHA = clamp(amount * 3.0, 0.0, 1.0);
}
