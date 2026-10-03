shader_type spatial;
render_mode unshaded, depth_test_disabled, shadows_disabled;
// Only where a dead body has marked the stencil, so the bones show through their own body and
// nothing else.
stencil_mode read, compare_equal, 1;

uniform vec4 bone_color : source_color = vec4(0.92, 0.88, 1.0, 1.0);

void fragment() {
    ALBEDO = bone_color.rgb;
    // Written, so the bones draw in the transparent pass - after the body has marked the stencil.
    ALPHA = bone_color.a;
}
