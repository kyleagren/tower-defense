shader_type spatial;
render_mode unshaded;

// A seamless NoiseTexture2D. Two samples at different scales, scrolling different ways, make the
// surface churn; low noise is cooled crust, high noise is glowing cracks.
uniform sampler2D noise : repeat_enable;
uniform vec4 hot_color : source_color = vec4(1.0, 0.85, 0.35, 1.0);
uniform vec4 mid_color : source_color = vec4(1.0, 0.4, 0.05, 1.0);
uniform vec4 crust_color : source_color = vec4(0.25, 0.05, 0.02, 1.0);
uniform float flow_speed = 0.4;
// How far the surface bulges in and out, as a share of the radius.
uniform float lumpiness = 0.12;
// Above 1 pushes the hottest streaks past white, so the level's glow blooms them.
uniform float hot_boost = 2.0;

void vertex() {
    float n = texture(noise, UV * 2.0 + vec2(TIME * 0.1, 0.0)).r;
    VERTEX += NORMAL * (n - 0.5) * lumpiness;
}

void fragment() {
    float coarse = texture(noise, UV * 3.0 + vec2(TIME * flow_speed, TIME * flow_speed * 0.6)).r;
    float fine = texture(noise, UV * 7.0 - vec2(TIME * flow_speed * 1.3, 0.0)).r;
    float heat = coarse * 0.6 + fine * 0.4;

    vec3 color = mix(crust_color.rgb, mid_color.rgb, smoothstep(0.35, 0.55, heat));
    color = mix(color, hot_color.rgb * hot_boost, smoothstep(0.6, 0.75, heat));

    // The edge glows brighter than the face, so the silhouette reads as burning.
    float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.0);
    color += hot_color.rgb * rim;

    ALBEDO = color;
}
