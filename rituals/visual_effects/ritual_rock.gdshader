shader_type spatial;

instance uniform vec4 glow_color : source_color = vec4(1.0);
instance uniform float vitality = 1.0;

uniform vec3 stone_color : source_color = vec3(0.22, 0.2, 0.21);
// How strongly the undersides glow, as if held up by the core.
uniform float underglow = 0.8;

void fragment() {
    vec3 world_normal = (INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
    float underside = pow(clamp(-world_normal.y, 0.0, 1.0), 2.0);

    ALBEDO = stone_color;
    ROUGHNESS = 0.9;
    EMISSION = glow_color.rgb * underside * underglow * vitality;
}
