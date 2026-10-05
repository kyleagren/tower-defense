shader_type spatial;
// Drawn on the inside faces of a box around the range, with no depth test, so it covers each
// pixel the volume touches exactly once - camera inside it or not - and tints whatever opaque
// surface the depth buffer says that pixel shows.
render_mode unshaded, blend_mix, cull_front, depth_test_disabled, depth_draw_never, shadows_disabled;

uniform sampler2D depth_texture : hint_depth_texture, filter_nearest;
// GameRules.RitualRangeUpWeight / DownWeight - how much a metre of height counts against range.
uniform float up_weight = 1.0;
uniform float down_weight = 0.5;
uniform vec4 fill_color : source_color = vec4(0.3, 0.6, 1.0, 0.2);
uniform vec4 edge_color : source_color = vec4(0.5, 0.8, 1.0, 0.9);
// How far inside the range's edge the border starts, in weighted metres.
uniform float edge_width = 0.15;

instance uniform float range = 10.0;
// The box sits off-centre around the Ritual (it reaches further down than up); this is how far
// the Ritual's base is from the box's centre.
instance uniform float base_offset = 0.0;
// 0 draws only the border, for an Anchor Ritual whose spot reads inside it.
instance uniform float fill_strength = 1.0;

void fragment() {
    float depth = texture(depth_texture, SCREEN_UV).r;
    vec4 world = INV_VIEW_MATRIX * INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
    vec3 offset = world.xyz / world.w - (NODE_POSITION_WORLD + vec3(0.0, base_offset, 0.0));
    offset.y *= offset.y < 0.0 ? down_weight : up_weight;
    float reach = length(offset);

    if (reach > range) {
        discard;
    }

    float edge = step(range - edge_width, reach);
    vec4 color = mix(vec4(fill_color.rgb, fill_color.a * fill_strength), edge_color, edge);
    ALBEDO = color.rgb;
    ALPHA = color.a;
}
