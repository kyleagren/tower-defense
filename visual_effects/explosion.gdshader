shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

// Noise eats the blast away as it ages, while it cools from white-hot to dark red.
uniform sampler2D noise : repeat_enable;
uniform vec4 hot_color : source_color = vec4(1.0, 0.85, 0.4, 1.0);
uniform vec4 cool_color : source_color = vec4(0.6, 0.1, 0.02, 1.0);
uniform float hot_boost = 2.0;

// Set per explosion through SetInstanceShaderParameter, so every explosion shares one material.
instance uniform float progress : hint_range(0.0, 1.0) = 0.0;

void fragment() {
    float n = texture(noise, UV * 2.0).r;

    if (n < progress) {
        discard;
    }

    float rim = pow(1.0 - clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0), 2.0);
    ALBEDO = mix(hot_color.rgb * hot_boost, cool_color.rgb, progress);
    ALPHA = (1.0 - progress) * (0.4 + rim * 0.6);
}
