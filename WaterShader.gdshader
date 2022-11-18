// NOTE: Shader automatically converted from Godot Engine 3.5.1.stable.mono's SpatialMaterial.

shader_type spatial;
render_mode async_visible,blend_mix,depth_draw_always,cull_back,diffuse_lambert,specular_disabled,world_vertex_coords;
uniform vec4 albedo : hint_color;
uniform sampler2D texture_reflection : hint_albedo;
uniform sampler2D texture_albedo : hint_albedo;
uniform float specular;
uniform float metallic;
uniform float roughness : hint_range(0,1);
uniform float point_size : hint_range(0,128);
uniform sampler2D texture_refraction;
uniform float refraction : hint_range(-16,16);
uniform vec4 refraction_texture_channel;
uniform sampler2D texture_normal : hint_normal;
uniform float normal_scale : hint_range(-64,64);
varying vec3 uv1;

uniform vec3 uv1_scale;
uniform vec3 uv1_offset;
uniform vec3 uv2_scale;
uniform vec3 uv2_offset;

void vertex()
{
    TANGENT = vec3(0.0,0.0,-1.0) * abs(NORMAL.x);
    TANGENT+= vec3(1.0,0.0,0.0) * abs(NORMAL.y);
    TANGENT+= vec3(1.0,0.0,0.0) * abs(NORMAL.z);
    TANGENT = normalize(TANGENT);
    BINORMAL = vec3(0.0,1.0,0.0) * abs(NORMAL.x);
    BINORMAL+= vec3(0.0,0.0,-1.0) * abs(NORMAL.y);
    BINORMAL+= vec3(0.0,1.0,0.0) * abs(NORMAL.z);
    BINORMAL = normalize(BINORMAL);
    uv1 = VERTEX;
}

float fresnel(float ior, vec3 normal, vec3 view)
{
    float r_0_sqrt = (1.0 - ior)/(1.0 + ior);
    float r_0 = r_0_sqrt*r_0_sqrt;
    return clamp(r_0 + (1.0 - r_0) * pow(1.0 - dot(normalize(normal), normalize(view)), 5.0), 0.0, 1.0);
}

uniform float water_normal_power;
uniform float water_ior;

void fragment()
{
    vec2 uv = vec2(uv1.x, uv1.z);
    vec4 albedo_tex = texture(texture_albedo, uv);
    ALBEDO = albedo.rgb * albedo_tex.rgb;
    METALLIC = metallic;
    ROUGHNESS = roughness;
    SPECULAR = specular;
    //NORMALMAP = texture(texture_normal, uv).rgb;
    //NORMALMAP_DEPTH = normal_scale;
    vec3 unpacked_normal = NORMALMAP;
    unpacked_normal.xy = unpacked_normal.xy * 2.0 - 1.0;
    unpacked_normal.z = sqrt(max(0.0, 1.0 - dot(unpacked_normal.xy, unpacked_normal.xy)));
    vec3 ref_normal = normalize( mix(NORMAL,TANGENT * unpacked_normal.x + BINORMAL * unpacked_normal.y + NORMAL * unpacked_normal.z,NORMALMAP_DEPTH) );
    float r_a = texture(texture_refraction,uv/2.0 + vec2(-uv1_offset.x*2.0, -uv1_offset.x)).r - 0.5;
    float r_b = texture(texture_refraction,uv     + uv1_offset.xy*2.0).r - 0.5;
    vec2 ref_ofs = SCREEN_UV - ref_normal.xy * (r_a+r_b*0.5) * refraction;
    
    float depth = FRAGCOORD.z;
    
    vec3 refr = textureLod(SCREEN_TEXTURE, ref_ofs, 0.0).rgb;
    float ref_depth = textureLod(DEPTH_TEXTURE, ref_ofs, 0.0).r;
    // for MSAA artifacts
    float ref_depth2 = textureLod(DEPTH_TEXTURE, ref_ofs + vec2( 1.0,  0.0)*1.0/vec2(VIEWPORT_SIZE), 0.0).r;
    float ref_depth3 = textureLod(DEPTH_TEXTURE, ref_ofs + vec2( 0.0,  1.0)*1.0/vec2(VIEWPORT_SIZE), 0.0).r;
    float ref_depth4 = textureLod(DEPTH_TEXTURE, ref_ofs + vec2(-1.0, 0.0)*1.0/vec2(VIEWPORT_SIZE), 0.0).r;
    float ref_depth5 = textureLod(DEPTH_TEXTURE, ref_ofs + vec2(0.0, -1.0)*1.0/vec2(VIEWPORT_SIZE), 0.0).r;
    
    if(ref_depth < depth || ref_depth2 < depth || ref_depth3 < depth || ref_depth4 < depth || ref_depth5 < depth)
        refr = texture(SCREEN_TEXTURE, SCREEN_UV).rgb;
    
    float t = TIME/32.0;
    vec3 n_a = texture(texture_normal, uv/2.0 + vec2(-t*8.0, -t    )).xyz - vec3(0.5);
    vec3 n_b = texture(texture_normal, uv     + vec2( t*4.0)        ).xyz - vec3(0.5);
    vec3 n_c = texture(texture_normal, uv/8.0 + vec2( t,     -t*2.0)).xyz - vec3(0.5);
    vec3 n_full = n_a + n_b*0.75 + n_c;
    n_full.b /= normal_scale;
    n_full = normalize(n_full);
    
    mat3 TBN = mat3(TANGENT, BINORMAL, NORMAL);
    vec3 water_normal = normalize(TBN * n_full);
    float reflection_amount = fresnel(water_ior, water_normal, VIEW);
    
    vec3 refl = vec3(0.0);
    if(reflection_amount > 0.0)
    {
        vec2 suv = ref_ofs;
        suv.y = 1.0 - suv.y;
        vec2 distortion = n_full.xy*water_normal_power;
        distortion.x *= VIEWPORT_SIZE.y/VIEWPORT_SIZE.x;
        vec2 duv = suv + distortion;
        duv = clamp(duv, vec2(0.0), vec2(1.0));
        refl = texture(texture_reflection, duv).rgb;
    }
    
    float ref_amount = 1.0 - albedo.a * albedo_tex.a;
    EMISSION = mix(ALBEDO, refr, ref_amount);
    EMISSION = mix(EMISSION, refl, reflection_amount);
    ALBEDO.rgb *= 0.0;
    
    //EMISSION += refr;
    //ALBEDO *= 1.0 - ref_amount;
    //EMISSION = n_full/2.0 + vec3(0.5);
    //EMISSION = pow(EMISSION, vec3(2.0));
    //ALBEDO = vec3(0.0);
    //ALPHA = 1.0;
}
