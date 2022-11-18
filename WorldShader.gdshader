shader_type spatial;

render_mode async_visible,blend_mix,depth_draw_opaque,cull_back,diffuse_lambert,specular_disabled;
uniform vec4 albedo : hint_color;
uniform sampler2DArray texture_albedo : hint_albedo;
uniform float specular;
uniform float metallic;
uniform float alpha_scissor_threshold;
uniform float roughness : hint_range(0,1);

uniform vec2 tile_offset;

void fragment() {
    float tile_size = 16.0;
    vec2 uv_adj = UV;
    vec2 base_uv = uv_adj;
    vec2 uv2 = UV2 + tile_offset;
    vec4 albedo_tex = texture(texture_albedo, vec3(base_uv, uv2.x + uv2.y*16.0));
    ALBEDO = albedo.rgb * albedo_tex.rgb;
    METALLIC = metallic;
    ROUGHNESS = roughness;
    SPECULAR = specular;
    float alpha = albedo.a * albedo_tex.a;
    if(alpha < alpha_scissor_threshold)
        discard;
    //ALPHA_SCISSOR = alpha_scissor_threshold;
}
