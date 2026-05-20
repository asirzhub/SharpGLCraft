#version 330 core

// vertex data
layout(location = 0) in uint inPosNorBright;
layout(location = 1) in vec2 inTex;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

uniform float u_waterOffset;
uniform float u_waveAmplitude;
uniform float u_waveScale;
uniform float u_time;
uniform float u_waveSpeed;

out vec2 texCoord;
out vec3 vNormal;
out vec4 worldPos;
out float vertexBrightness;
out float isWater;
out float isFoliage;

vec3 DecodePos(uint p) {
    float x = ((p >> 0) & 0x7Fu);
    float y = ((p >> 7) & 0x7Fu);
    float z = ((p >> 14) & 0x7Fu);

    return vec3(x, y, z);
}

vec3 DecodeNormal(uint p) {
    uint n = (p >> 21) & 0x7u;
    if (n == 0u) return vec3( 0, 0, 1);
    if (n == 1u) return vec3( 0, 0,-1);
    if (n == 2u) return vec3(-1, 0, 0);
    if (n == 3u) return vec3( 1, 0, 0);
    if (n == 4u) return vec3( 0, 1, 0);
                 return vec3( 0,-1, 0);
}

void main()
{
    texCoord = inTex;

    vertexBrightness = uint((inPosNorBright >> 24) & 0xFu);
    vNormal = DecodeNormal(inPosNorBright);
    
    uint wigX = (inPosNorBright >> 28) & 1u;  // omnidirectional / foliage
    uint wigY = (inPosNorBright >> 29) & 1u;  // vertical / water

    isWater   = int(wigY);
    isFoliage = int(wigX);

    vec4 position = vec4(DecodePos(inPosNorBright), 1.0);
    worldPos = model * position;

    float q_time = floor(u_time * 60.0)/60.0;

    if(isWater == 1)
    {
        position.y -= u_waterOffset * ((inPosNorBright >> 29) & 0x1u);
        position.y += u_waveAmplitude * sin(((worldPos.x + worldPos.z - 5 * worldPos.y) * u_waveScale + q_time * u_waveSpeed)*6.28318);
    }
    if(isFoliage >= 1){
        position.x += sin(((worldPos.x + worldPos.z ) + u_time * u_waveSpeed)*6.28318) * u_waveAmplitude * cos(((worldPos.x + worldPos.z - 5 * worldPos.y) * u_waveScale + u_time/2 * u_waveSpeed)*6.28318);
        position.z += u_waveAmplitude * sin(((worldPos.x + worldPos.z + 5 * worldPos.y) * -u_waveScale/2 + q_time * u_waveSpeed)*6.28318);
    }

    gl_Position = projection * view * model * position;
}