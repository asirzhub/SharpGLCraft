#version 330 core

in vec2 texCoord;

uniform sampler2D albedoTexture;

out vec4 FragColor;

void main()
{
    vec4 texColor = texture(albedoTexture, texCoord);
    if(texColor.a < 0.5) discard;

    FragColor = texColor;
}