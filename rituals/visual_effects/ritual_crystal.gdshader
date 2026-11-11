shader_type spatial;
render_mode cull_back;

instance uniform vec4 crystal_color : source_color = vec4(1.0);
instance uniform float vitality = 1.0;

uniform float rim_power = 3.0;
uniform float rim_glow = 1.5;
uniform vec3 broken_color : source_color = vec3(0.25);

void fragment() {
    float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), rim_power);
    vec3 tint = mix(broken_color, crystal_color.rgb, vitality);

    ALBEDO = tint * 0.35;
    ROUGHNESS = 0.15;
    SPECULAR = 0.8;
    EMISSION = tint * (0.25 + rim * rim_glow) * vitality;
}
