#version 330 core

in vec2 texCoord;
in vec3 vNormal;
in vec4 worldPos;
in float vertexBrightness;

uniform sampler2D albedoTexture;
uniform sampler2DShadow shadowMap;

uniform mat4 lightProjMat;
uniform mat4 lightViewMat;

uniform vec3 cameraPos;

uniform vec3 u_sunColor;
uniform vec3 u_sunsetColor;
uniform vec3 u_nightLight;
uniform vec3 u_sunDirection;

uniform float u_minLight;
uniform float u_maxLight;

uniform vec3 u_horizonColor;
uniform vec3 u_zenithColor;
uniform float u_hzLightMix;

uniform float u_fogStartDistance;
uniform float u_fogEndDistance;

uniform float u_fogSampleSpacing; 
uniform int u_fogSamples;

//uniform float u_seaLevel;

in float isWater;
in float isFoliage;

out vec4 FragColor;

float rand(vec2 co) { return fract(sin(dot(co, vec2(12.9898, 78.233))) * 43758.5453); }

float noise(vec2 p){
	vec2 ip = floor(p);
	vec2 u = fract(p);
	u = u*u*(3.0-2.0*u);
	
	float res = mix(
		mix(rand(ip),rand(ip+vec2(1.0,0.0)),u.x),
		mix(rand(ip+vec2(0.0,1.0)),rand(ip+vec2(1.0,1.0)),u.x),u.y);
	return res * res;
}


float fbm(vec2 p, int octaves, float lacunarity, float gain) {
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    float ampSum = 0.0;

    for (int i = 0; i < octaves; i++) {
        sum += amp * noise(p * freq);
        ampSum += amp;

        freq *= lacunarity;
        amp *= gain;
    }

    return sum / ampSum;
}

float ShadowAtQuantPos(vec4 qpos, float bias)
{
    vec4 lightSpacePos = lightProjMat * lightViewMat * qpos;
    vec3 projCoord = (lightSpacePos.xyz / lightSpacePos.w) * 0.5 + 0.5;

    // outside the shadow map = treat as lit
    if (projCoord.x < 0.0 || projCoord.x > 1.0 ||
        projCoord.y < 0.0 || projCoord.y > 1.0)
        return 0.0; // 0 shadow

    // hardware compare: returns 1.0 when lit, 0.0 when shadowed (with LEQUAL/LESS depending)
    float visibility = texture(shadowMap, vec3(projCoord.xy, projCoord.z - bias));

    // convert to "shadow amount" (1 = shadowed, 0 = lit)
    return (1.0 - visibility);
}


void main()
{
    const float oneTexel = 1.0/16;
    float bias = 0.00005;

    vec4 texColor = texture(albedoTexture, texCoord); 
    if(texColor.a < 0.1) discard;

    float shadowAmount = 0.0;
    float daylight = smoothstep(-0.2, 0.2, clamp(u_sunDirection.y + 0.2, 0.0, 1.0));
    float sqrtDaylight = sqrt(daylight);
    float sunNormalDot = clamp(dot(vNormal, u_sunDirection), 0.0, 1.0);

    if (dot(u_sunDirection, vNormal) >= 0.0)
    {
        float fade = 0.1;
        vec4 centerOffset = vec4(vec3(oneTexel/2.0), 0.0);
        vec4 quantPos = (floor((worldPos + centerOffset)/oneTexel)-centerOffset)*oneTexel;

        vec4 lightSpacePos = lightProjMat * lightViewMat * quantPos;
        vec3 projCoord = (lightSpacePos.xyz / lightSpacePos.w) * 0.5 + 0.5;

        float edgeFadeX = smoothstep(0.0, fade, projCoord.x) *
                          smoothstep(1.0, 1.0 - fade, projCoord.x);
        float edgeFadeY = smoothstep(0.0, fade, projCoord.y) *
                          smoothstep(1.0, 1.0 - fade, projCoord.y);

        // 6 axis-adjacent offsets in the grid
        vec4 dx = vec4(oneTexel, 0.0,     0.0,     0.0);
        vec4 dy = vec4(0.0,     oneTexel, 0.0,     0.0);
        vec4 dz = vec4(0.0,     0.0,     oneTexel, 0.0);

        shadowAmount = edgeFadeX * edgeFadeY * (ShadowAtQuantPos(quantPos, bias) );
    }

    // Fog
    vec3 cameraToFragWorld = worldPos.xyz - cameraPos;
    float camToFragDist = length(cameraToFragWorld);    
    vec3 camToFragDir = normalize(cameraToFragWorld);

    float fogginess = 0.0;
    float fade = 0.2;

    for (int i = 1; i < u_fogSamples+1; i++) {
        float t = (float(i) + rand(worldPos.xz)) / float(u_fogSamples);
        t = sqrt(t);
        vec4 samplePoint = vec4(cameraPos + cameraToFragWorld * t, 1.0);

        vec4 lightSpacePos = lightProjMat * lightViewMat * samplePoint;
        vec3 projCoord = (lightSpacePos.xyz / lightSpacePos.w) * 0.5 + 0.5;

        float edgeFadeX = smoothstep(0.0, fade, projCoord.x) *
                          smoothstep(1.0, 1.0 - fade, projCoord.x);
        float edgeFadeY = smoothstep(0.0, fade, projCoord.y) *
                          smoothstep(1.0, 1.0 - fade, projCoord.y);

        float visibility = texture(shadowMap, vec3(projCoord.xy, projCoord.z - bias));

        float shadowAmt = (1.0 - visibility);
        float litAmt = 1.0 - shadowAmt;
        
        fogginess += litAmt/u_fogSamples * (1.0-exp(-0.01 * camToFragDist - 0.1));
    }
    fogginess = smoothstep(0.0, 1.5, fogginess);

    // combination of horizon or zenith light (fixed), sunlight * (dynamic), fake SSS, clamped by vertex ambient occlusion
    float isTopFace = clamp(vNormal.y, 0.0, 1.0);

    float SSS = clamp(isFoliage + isWater, 0.0, 1.0) * smoothstep(0.0, 2.0, dot(normalize(cameraToFragWorld), u_sunDirection));

    vec3 faceLight = clamp(vertexBrightness*vertexBrightness/(16.0*16.0) * (
    (
    (1.0 + u_hzLightMix - isTopFace) * u_horizonColor
    + u_hzLightMix + isTopFace * u_zenithColor ) 
    * (1.0/(1.0+u_hzLightMix)) * (0.5 + sunNormalDot)/1.5

    + (clamp(sunNormalDot + SSS, 0, 1.0) * sqrtDaylight) 
    * (1 - shadowAmount) * u_sunColor * vec3(2.0-daylight, 1.0, 2.0 - sqrtDaylight))
    , 0.0, 1.5);

    //vec4 fogColor = vec4(mix(vec3(1.0, 0.0, 0.0), mix(u_horizonColor/1.3, u_sunColor, clamp(dot(camToFragDir, u_sunDirection), 0.0, 1.0) ), 1.0-abs(camToFragDir.y)), 1.0);
        
    float c = max(dot(camToFragDir, u_sunDirection), 0.0);
    float h = clamp(u_sunDirection.y, -1.0, 1.0);       
    float up = clamp(camToFragDir.y, 0.0, 1.0);
    float bz = pow(up, 0.7);
    vec3 sky = mix(
    mix((u_horizonColor + u_sunsetColor * c * (1.0 - sqrt(clamp(h, 0.0, 1.0)))), u_zenithColor, bz), 
    u_sunColor, c*c*c);
        
    FragColor  = mix(texColor * vec4(faceLight, 1.0), vec4(sky, 1.0), fogginess);
}